using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GearHeim
{
    /// <summary>
    /// The buildable Loadout Chest. Loadouts are kept in the piece's ZDO (not a normal
    /// container inventory), so they can only change through the loadout menu.
    ///
    /// Every change goes through <see cref="WithAccess"/>, which mirrors how vanilla chests
    /// open: ask the current owner, who hands over ownership (with fresh data) to one player
    /// at a time. That keeps two players from overwriting each other's changes.
    /// </summary>
    public class LoadoutChest : MonoBehaviour, Hoverable, Interactable
    {
        private const string DataKey = "GearHeim.Loadouts";
        private const string LockKey = "GearHeim.LockHolder";
        private const string LockTimeKey = "GearHeim.LockTime";
        private const string RpcRequest = "GearHeim_RequestAccess";
        private const string RpcResponse = "GearHeim_AccessResponse";
        private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(15);
        private const float ResponseTimeout = 10f;

        internal static readonly List<LoadoutChest> All = new List<LoadoutChest>();

        private ZNetView _nview;
        private Action _pending;
        private float _pendingSince;
        private bool _granted;
        private uint _grantedRevision;
        private bool _corrupt;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            if (_nview == null || _nview.GetZDO() == null) return; // placement ghost

            All.Add(this);
            _nview.Register<long>(RpcRequest, RPC_RequestAccess);
            _nview.Register<bool, long>(RpcResponse, RPC_AccessResponse);

            var wearNTear = GetComponent<WearNTear>();
            if (wearNTear != null) wearNTear.m_onDestroyed += OnDestroyed;
        }

        private void OnDestroy()
        {
            All.Remove(this);
        }

        private void Update()
        {
            if (_pending == null) return;

            // Granted: wait until the ownership hand-off and the owner's latest data have arrived.
            // (On a dedicated server they travel separately from the response.)
            if (_granted && IsValid && _nview.IsOwner() && _nview.GetZDO().DataRevision >= _grantedRevision)
            {
                RunPending();
                return;
            }

            if (Time.time - _pendingSince > ResponseTimeout)
            {
                _pending = null;
                _granted = false;
                if (Player.m_localPlayer != null)
                    LoadoutService.Message(Player.m_localPlayer, "The Loadout Chest didn't respond. Try again.");
            }
        }

        internal bool IsValid => _nview != null && _nview.IsValid();

        internal Vector3 Position => transform.position;

        // ---------------------------------------------------------------- exclusive access

        /// <summary>
        /// Runs <paramref name="action"/> once this client owns the chest exclusively, then releases it.
        /// Tells the player if someone else is using it.
        /// </summary>
        internal void WithAccess(Action action)
        {
            if (!IsValid) return;
            if (_pending != null)
            {
                LoadoutService.Message(Player.m_localPlayer, "Still waiting on the Loadout Chest...");
                return;
            }
            _pending = action;
            _pendingSince = Time.time;
            _granted = false;
            _nview.InvokeRPC(RpcRequest, Game.instance.GetPlayerProfile().GetPlayerID());
        }

        // Runs on the chest's current owner.
        private void RPC_RequestAccess(long sender, long playerId)
        {
            if (!_nview.IsOwner()) return;

            var zdo = _nview.GetZDO();
            long holder = zdo.GetLong(LockKey);
            // Network time, so every machine agrees on when a lock expires.
            var now = ZNet.instance.GetTime();
            var since = new DateTime(zdo.GetLong(LockTimeKey));
            bool lockedByOther = holder != 0 && holder != sender && now - since < LockTimeout;
            if (lockedByOther)
            {
                _nview.InvokeRPC(sender, RpcResponse, false, 0L);
                return;
            }

            zdo.Set(LockKey, sender);
            zdo.Set(LockTimeKey, now.Ticks);
            // Same hand-off as Container.RPC_RequestOpen: send fresh data, then ownership.
            ZDOMan.instance.ForceSendZDO(sender, zdo.m_uid);
            zdo.SetOwner(sender);
            _nview.InvokeRPC(sender, RpcResponse, true, (long)zdo.DataRevision);
        }

        private void RPC_AccessResponse(long sender, bool granted, long revision)
        {
            if (_pending == null) return;

            if (!granted || !IsValid)
            {
                _pending = null;
                if (Player.m_localPlayer != null)
                    LoadoutService.Message(Player.m_localPlayer, "Someone else is using that Loadout Chest.");
                return;
            }

            _granted = true;
            _grantedRevision = (uint)revision;
            Update(); // runs now if ownership has already arrived (always true when we were the owner)
        }

        private void RunPending()
        {
            var action = _pending;
            _pending = null;
            _granted = false;

            try
            {
                action();
            }
            catch (Exception e)
            {
                GearHeimPlugin.Log.LogError($"Loadout action failed: {e}");
            }
            finally
            {
                if (IsValid && _nview.IsOwner()) _nview.GetZDO().Set(LockKey, 0L);
                LoadoutMenu.Refresh();
            }
        }

        // ---------------------------------------------------------------- data

        /// <summary>All loadouts with their items. Only call this while holding access.</summary>
        internal List<StoredLoadout> Load() => Read(withItems: true);

        /// <summary>Loadout names and item counts only; cheap enough for hover text and listings.</summary>
        internal List<StoredLoadout> Peek() => Read(withItems: false);

        private List<StoredLoadout> Read(bool withItems)
        {
            if (!IsValid) return new List<StoredLoadout>();
            try
            {
                return LoadoutStorage.Read(_nview.GetZDO().GetByteArray(DataKey), withItems);
            }
            catch (Exception e)
            {
                if (!_corrupt) GearHeimPlugin.Log.LogError($"Loadout Chest data is unreadable; leaving it untouched. {e}");
                _corrupt = true;
                return new List<StoredLoadout>();
            }
        }

        /// <summary>Writes the loadouts. Returns false (and writes nothing) if the chest can't be written safely.</summary>
        internal bool Save(List<StoredLoadout> loadouts)
        {
            if (!IsValid || !_nview.IsOwner() || _corrupt) return false;
            _nview.GetZDO().Set(DataKey, LoadoutStorage.Write(loadouts));
            return true;
        }

        internal bool IsCorrupt => _corrupt;

        internal bool HasStoredItems() => _corrupt || Peek().Any(l => l.ItemCount > 0);

        internal bool CanAccess(bool flash) => PrivateArea.CheckAccess(Position, 0f, flash);

        /// <summary>Loadout chests inside an area, nearest first.</summary>
        internal static List<LoadoutChest> InArea(BaseArea area) =>
            All.Where(c => c != null && c.IsValid && area.Contains(c.Position))
               .OrderBy(c => area.DistanceFrom(c.Position))
               .ToList();

        // ---------------------------------------------------------------- Hoverable / Interactable

        public string GetHoverText()
        {
            if (!IsValid) return "";
            if (!CanAccess(false)) return Localization.instance.Localize("Loadout Chest\n$piece_noaccess");

            int count = Peek().Count;
            string summary = _corrupt ? "data unreadable" : count == 1 ? "1 loadout" : $"{count} loadouts";
            return Localization.instance.Localize(
                $"Loadout Chest ( {summary} )\n[<color=yellow><b>$KEY_Use</b></color>] Manage loadouts");
        }

        public string GetHoverName() => "Loadout Chest";

        // Same as the game's chests (Container.m_hoverOffset defaults to 0).
        public float GetHoverOffset() => 0f;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !IsValid) return false;
            if (!CanAccess(true)) return true;
            LoadoutMenu.Open(this);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        /// <summary>When the chest is destroyed, spill every stored loadout item so nothing is lost.</summary>
        private void OnDestroyed()
        {
            if (!IsValid || !_nview.IsOwner() || _corrupt) return;

            foreach (var loadout in Load())
            {
                foreach (var item in loadout.Items)
                {
                    Tags.Clear(item);
                    var offset = Random.insideUnitSphere * 0.5f;
                    offset.y = Mathf.Abs(offset.y) + 0.5f;
                    ItemDrop.DropItem(item, item.m_stack, Position + offset, Quaternion.identity);
                }
            }
            _nview.GetZDO().Set(DataKey, new byte[0]);
        }
    }
}
