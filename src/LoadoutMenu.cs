using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace GearHeim
{
    /// <summary>The loadout list and actions, opened at a Loadout Chest or with the menu hotkey.</summary>
    internal static class LoadoutMenu
    {
        private const float Width = 600f;
        private const float RowHeight = 40f;
        private const int MaxRows = 8;

        private static GameObject _panel;
        private static InputField _nameField;
        private static LoadoutChest _chest;
        private static string _confirmDeleteId;

        public static bool IsOpen => _panel != null && _panel.activeSelf;

        public static void Toggle()
        {
            if (IsOpen) Close();
            else Open(null);
        }

        /// <summary>Opens the menu. With no chest, uses the nearest Loadout Chest in the base.</summary>
        public static void Open(LoadoutChest chest)
        {
            var player = Player.m_localPlayer;
            if (player == null || GUIManager.CustomGUIFront == null) return;

            var area = BaseArea.For(player.transform.position);
            if (chest == null)
            {
                if (!area.IsValid)
                {
                    LoadoutService.Message(player, "You need to be in your base (near a workbench) to change loadouts.");
                    return;
                }
                var chests = LoadoutChest.InArea(area);
                if (chests.Count == 0)
                {
                    LoadoutService.Message(player, "No Loadout Chest in this base.");
                    return;
                }
                chest = chests[0];
            }

            _chest = chest;
            _confirmDeleteId = null;
            Rebuild();
            GUIManager.BlockInput(true);
        }

        public static void Close()
        {
            if (_panel != null) Object.Destroy(_panel);
            _panel = null;
            _nameField = null;
            _chest = null;
            GUIManager.BlockInput(false);
        }

        /// <summary>Redraws the menu if it's open (e.g. after a chest action finishes).</summary>
        public static void Refresh()
        {
            if (IsOpen) Rebuild();
        }

        public static void ClearName()
        {
            if (_nameField != null) _nameField.text = "";
        }

        public static void Update()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Player.m_localPlayer == null || _chest == null || !_chest.IsValid)
                Close();
        }

        /// <summary>True when a text box (chat, console, our name field...) has keyboard focus.</summary>
        public static bool IsTyping() =>
            (_nameField != null && _nameField.isFocused)
            || (Chat.instance != null && Chat.instance.HasFocus())
            || global::Console.IsVisible()
            || TextInput.IsVisible()
            || Menu.IsVisible();

        private static void Rebuild()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            string typedName = _nameField != null ? _nameField.text : "";
            if (_panel != null) Object.Destroy(_panel);

            var area = BaseArea.For(player.transform.position);
            var listings = area.IsValid ? LoadoutService.List(player, area) : new List<LoadoutListing>();
            var worn = LoadoutService.GetWorn(player);

            int rows = Mathf.Max(1, Mathf.Min(listings.Count, MaxRows));
            float height = 190f + rows * RowHeight + 150f;

            var gui = GUIManager.Instance;
            _panel = gui.CreateWoodpanel(GUIManager.CustomGUIFront.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Width, height, false);

            float y = -30f;
            Text("Loadouts", y, 28, gui.ValheimOrange);
            y -= 34f;
            Text(worn != null ? $"Wearing: {worn.Name}" : "Not wearing a saved loadout", y, 18, gui.ValheimBeige);
            y -= 44f;

            if (listings.Count == 0)
            {
                Text("No loadouts yet. Name one below and save your current gear.", y, 16, Color.white);
                y -= RowHeight;
            }

            for (int i = 0; i < listings.Count && i < MaxRows; i++)
            {
                var listing = listings[i];
                string key = i < GearHeimPlugin.QuickKeys.Length ? GearHeimPlugin.QuickKeys[i].Value.ToString() : "";
                string status = listing.Worn ? "worn" : listing.Stored ? $"{listing.ItemCount} items" : "elsewhere";
                string label = $"{listing.Def.Name}  <color=#a0a0a0>({status}{(key.Length > 0 ? ", " + key : "")})</color>";
                RowText(label, y);

                if (listing.Stored)
                {
                    Button("Equip", -Width / 2 + 410f, y, 90f, () =>
                        LoadoutService.Switch(Player.m_localPlayer, listing.Def.Id));
                }

                bool confirming = _confirmDeleteId == listing.Def.Id;
                Button(confirming ? "Sure?" : "Delete", -Width / 2 + 510f, y, 90f, () =>
                {
                    if (_confirmDeleteId != listing.Def.Id)
                    {
                        _confirmDeleteId = listing.Def.Id;
                    }
                    else
                    {
                        _confirmDeleteId = null;
                        LoadoutService.Delete(Player.m_localPlayer, listing);
                    }
                    Rebuild();
                });
                y -= RowHeight;
            }

            if (listings.Count > MaxRows)
            {
                Text($"+{listings.Count - MaxRows} more (use the hotkeys or delete some)", y, 14, Color.gray);
                y -= RowHeight;
            }

            y -= 16f;
            var field = gui.CreateInputField(_panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-95f, y), InputField.ContentType.Standard, "New loadout name", 18, 360f, 34f);
            _nameField = field.GetComponent<InputField>();
            _nameField.characterLimit = 32;
            _nameField.text = typedName;
            Button("Save gear as new", 195f, y, 180f, () =>
                LoadoutService.SaveNew(Player.m_localPlayer, _chest, _nameField.text));
            y -= 48f;

            if (worn != null)
            {
                Button($"Save changes to '{Shorten(worn.Name)}'", -140f, y, 270f, () =>
                    LoadoutService.UpdateWorn(Player.m_localPlayer, _chest));
                Button("Store it", 120f, y, 150f, () =>
                    LoadoutService.StoreWorn(Player.m_localPlayer, _chest));
            }
            Button("Close", 235f, y, 100f, Close);
        }

        private static string Shorten(string s) => s.Length <= 14 ? s : s.Substring(0, 13) + "…";

        private static void Text(string text, float y, int size, Color color)
        {
            GUIManager.Instance.CreateText(text, _panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, y), GUIManager.Instance.AveriaSerifBold, size, color, true, Color.black,
                Width - 40f, 30f, false).GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
        }

        private static void RowText(string text, float y)
        {
            var t = GUIManager.Instance.CreateText(text, _panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-Width / 2 + 200f, y), GUIManager.Instance.AveriaSerifBold, 18, Color.white, true, Color.black,
                360f, 32f, false).GetComponent<Text>();
            t.alignment = TextAnchor.MiddleLeft;
            t.supportRichText = true;
        }

        private static void Button(string label, float x, float y, float width, System.Action onClick)
        {
            var go = GUIManager.Instance.CreateButton(label, _panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x, y), width, 34f);
            go.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => onClick());
        }
    }
}
