using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GearHeim
{
    /// <summary>One loadout as shown in the menu.</summary>
    internal class LoadoutListing
    {
        public LoadoutDef Def;
        public LoadoutChest Chest;   // chest that holds it (null if worn and not listed in any chest here)
        public bool Stored;          // its items are in Chest, ready to equip
        public bool Worn;            // you're wearing it now
        public int ItemCount;
    }

    /// <summary>
    /// Loadout operations. Each one that changes a chest runs inside <see cref="LoadoutChest.WithAccess"/>,
    /// writes the chest before taking anything from the player, and only pulls from other chests
    /// once every check has passed.
    /// </summary>
    internal static class LoadoutService
    {
        private const string WornKey = "GearHeim.Worn";
        private const string LastWornName = "Last worn";

        // ---------------------------------------------------------------- worn state

        public static LoadoutDef GetWorn(Player player) =>
            player.m_customData.TryGetValue(WornKey, out var s) ? LoadoutDef.FromBase64(s) : null;

        private static void SetWorn(Player player, LoadoutDef def)
        {
            if (def == null) player.m_customData.Remove(WornKey);
            else player.m_customData[WornKey] = def.ToBase64();
        }

        // ---------------------------------------------------------------- listing

        /// <summary>Your loadouts in the current base, alphabetical. This is the order hotkeys use.</summary>
        public static List<LoadoutListing> List(Player player, BaseArea area)
        {
            long me = player.GetPlayerID();
            var byId = new Dictionary<string, LoadoutListing>();

            foreach (var chest in LoadoutChest.InArea(area))
            {
                foreach (var stored in chest.Peek())
                {
                    if (stored.Def.Owner != me) continue;
                    bool ready = !stored.CheckedOut;
                    // The same loadout can be listed in two chests (worn from one, stored in another);
                    // prefer the copy that actually holds the items.
                    if (byId.TryGetValue(stored.Def.Id, out var existing) && (existing.Stored || !ready)) continue;
                    byId[stored.Def.Id] = new LoadoutListing
                    {
                        Def = stored.Def,
                        Chest = chest,
                        Stored = ready,
                        ItemCount = stored.ItemCount,
                    };
                }
            }

            var worn = GetWorn(player);
            if (worn != null)
            {
                if (!byId.TryGetValue(worn.Id, out var listing))
                    byId[worn.Id] = listing = new LoadoutListing();
                listing.Def = worn;
                listing.Worn = true;
                listing.Stored = false;
            }

            return byId.Values
                .OrderBy(l => l.Def.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static void SwitchToListed(Player player, int index)
        {
            var area = BaseArea.For(player.transform.position);
            if (!RequireArea(player, area)) return;

            var list = List(player, area);
            if (index < 0 || index >= list.Count)
            {
                Message(player, $"No loadout #{index + 1} here.");
                return;
            }
            Switch(player, list[index].Def.Id);
        }

        // ---------------------------------------------------------------- switching

        public static void Switch(Player player, string targetId)
        {
            if (!CanAct(player)) return;
            var area = BaseArea.For(player.transform.position);
            if (!RequireArea(player, area)) return;

            var worn = GetWorn(player);
            if (worn != null && worn.Id == targetId)
            {
                Message(player, $"You're already wearing '{worn.Name}'.");
                return;
            }

            var target = List(player, area).FirstOrDefault(l => l.Def.Id == targetId && l.Stored);
            if (target == null)
            {
                Message(player, "That loadout isn't stored in a Loadout Chest in this base.");
                return;
            }

            var chest = target.Chest;
            chest.WithAccess(() => DoSwitch(player, chest, targetId));
        }

        private static void DoSwitch(Player player, LoadoutChest chest, string targetId)
        {
            if (!CanAct(player)) return;
            var area = BaseArea.For(player.transform.position);
            if (!area.IsValid || !area.Contains(chest.Position))
            {
                Message(player, "You've left the base.");
                return;
            }

            var loadouts = chest.Load();
            var stored = loadouts.FirstOrDefault(l =>
                l.Def.Id == targetId && !l.CheckedOut && l.Def.Owner == player.GetPlayerID());
            if (chest.IsCorrupt || stored == null)
            {
                Message(player, "That loadout is no longer in the chest.");
                return;
            }

            var worn = GetWorn(player);
            ClearOrphanTags(player, worn);
            var inventory = player.GetInventory();
            var incoming = stored.Items;

            // What comes off: your current loadout's items, or (if you aren't wearing one)
            // your equipped gear and hotbar, saved as "Last worn".
            LoadoutDef outDef;
            List<ItemDrop.ItemData> outgoing;
            if (worn != null)
            {
                outDef = worn;
                outgoing = inventory.m_inventory.Where(i => Tags.LoadoutOf(i) == worn.Id).ToList();
            }
            else
            {
                outgoing = CurrentGear(player);
                outDef = outgoing.Count > 0 ? NewDef(player, UniqueName(player, area, LastWornName)) : null;
            }

            // Space check before anything changes, reserving a slot for every stack restocking might add.
            if (!PlanPlacement(player, stored.Def, incoming, outgoing, MissingStackEntries(stored.Def, incoming), out _, out string why))
            {
                Message(player, why);
                return;
            }

            // ---- commit point: everything below is expected to succeed.
            if (outDef != null && worn == null) Capture(outDef, outgoing);

            var created = new List<ItemDrop.ItemData>();
            var outMissing = new List<string>();
            // Outgoing top-ups land on your own stacks, so only new stacks need returning on failure;
            // the incoming copies are discarded on failure, so their top-ups are tracked too.
            if (outDef != null) Restock(player, area, outDef, outgoing, outMissing, created, trackTopUps: false);
            var inMissing = new List<string>();
            Restock(player, area, stored.Def, incoming, inMissing, created, trackTopUps: true);

            Placement plan = null;
            bool planned = PlanPlacement(player, stored.Def, incoming, outgoing, 0, out plan, out why);

            stored.CheckedOut = true;
            stored.Items = new List<ItemDrop.ItemData>();
            if (outDef != null) Upsert(loadouts, outDef, outgoing);

            if (!planned || !chest.Save(loadouts))
            {
                // Shouldn't happen (slots were reserved and access is held), but never lose items.
                if (outDef != null && worn == null) foreach (var item in outgoing) Tags.Clear(item);
                GiveOrDrop(player, created);
                Message(player, planned ? "Couldn't write to the Loadout Chest; nothing was changed." : why);
                return;
            }

            // The chest now says the target is checked out, so record it as worn straight away;
            // otherwise a later switch would store an empty copy over it.
            SetWorn(player, stored.Def);
            try
            {
                RemoveFromPlayer(player, outgoing);
                player.ClearActionQueue(); // drop queued equips that could undo the switch
                Apply(player, stored.Def, plan);
            }
            catch (Exception e)
            {
                GearHeimPlugin.Log.LogError($"Switch failed after saving: {e}");
                GiveOrDrop(player, incoming.Where(i => !player.GetInventory().m_inventory.Contains(i)).ToList());
            }

            Message(player, $"Switched to '{stored.Def.Name}'.");
            Report(player, outDef, outMissing, "stored");
            Report(player, stored.Def, inMissing, "equipped");
        }

        // ---------------------------------------------------------------- menu actions at a chest

        /// <summary>Saves your equipped gear and hotbar as a new loadout in this chest.</summary>
        public static void SaveNew(Player player, LoadoutChest chest, string name)
        {
            if (!CanAct(player)) return;
            if (GetWorn(player) is LoadoutDef worn)
            {
                Message(player, $"You're wearing '{worn.Name}'. Store it or save your changes to it first.");
                return;
            }

            name = (name ?? "").Trim();
            if (name.Length == 0)
            {
                Message(player, "Give the loadout a name first.");
                return;
            }

            chest.WithAccess(() =>
            {
                if (!CanAct(player)) return;
                ClearOrphanTags(player, null);

                var gear = CurrentGear(player);
                if (gear.Count == 0)
                {
                    Message(player, "Equip some gear or fill your hotbar first.");
                    return;
                }

                var area = BaseArea.For(player.transform.position);
                var def = NewDef(player, UniqueName(player, area, name));
                Capture(def, gear);

                var loadouts = chest.Load();
                Upsert(loadouts, def, gear);
                if (!chest.Save(loadouts))
                {
                    foreach (var item in gear) Tags.Clear(item);
                    Message(player, "Couldn't write to the Loadout Chest; nothing was changed.");
                    return;
                }

                RemoveFromPlayer(player, gear);
                LoadoutMenu.ClearName();
                Message(player, $"Saved '{def.Name}' ({gear.Count} items).");
            });
        }

        /// <summary>Puts the loadout you're wearing back into this chest.</summary>
        public static void StoreWorn(Player player, LoadoutChest chest)
        {
            if (!CanAct(player)) return;
            if (GetWorn(player) == null)
            {
                Message(player, "You aren't wearing a saved loadout.");
                return;
            }

            chest.WithAccess(() =>
            {
                if (!CanAct(player)) return;
                var worn = GetWorn(player);
                if (worn == null) return;
                ClearOrphanTags(player, worn);

                var area = BaseArea.For(player.transform.position);
                var items = player.GetInventory().m_inventory.Where(i => Tags.LoadoutOf(i) == worn.Id).ToList();
                var missing = new List<string>();
                var created = new List<ItemDrop.ItemData>();
                Restock(player, area, worn, items, missing, created, trackTopUps: false);

                var loadouts = chest.Load();
                Upsert(loadouts, worn, items);
                if (!chest.Save(loadouts))
                {
                    GiveOrDrop(player, created);
                    Message(player, "Couldn't write to the Loadout Chest; nothing was changed.");
                    return;
                }

                RemoveFromPlayer(player, items);
                SetWorn(player, null);
                Message(player, $"Stored '{worn.Name}'.");
                Report(player, worn, missing, "stored");
            });
        }

        /// <summary>Replaces the worn loadout with what you have equipped and in your hotbar now, and stores it.</summary>
        public static void UpdateWorn(Player player, LoadoutChest chest)
        {
            if (!CanAct(player)) return;
            if (GetWorn(player) == null)
            {
                Message(player, "Equip a loadout first, change your gear, then save the changes here.");
                return;
            }

            chest.WithAccess(() =>
            {
                if (!CanAct(player)) return;
                var worn = GetWorn(player);
                if (worn == null) return;
                ClearOrphanTags(player, worn);

                var gear = CurrentGear(player);
                var previous = gear.ToDictionary(i => i, i => new Dictionary<string, string>(i.m_customData));
                var def = new LoadoutDef { Id = worn.Id, Name = worn.Name, Owner = worn.Owner };
                Capture(def, gear);

                var loadouts = chest.Load();
                Upsert(loadouts, def, gear);
                if (!chest.Save(loadouts))
                {
                    foreach (var pair in previous) pair.Key.m_customData = pair.Value;
                    Message(player, "Couldn't write to the Loadout Chest; nothing was changed.");
                    return;
                }

                // Loadout items you've moved out of your hotbar/equipment are no longer part of it.
                foreach (var item in player.GetInventory().m_inventory)
                    if (Tags.LoadoutOf(item) == worn.Id && !gear.Contains(item)) Tags.Clear(item);

                RemoveFromPlayer(player, gear);
                SetWorn(player, null);
                Message(player, $"Updated and stored '{def.Name}' ({gear.Count} items).");
            });
        }

        /// <summary>Deletes a loadout, giving its stored items back to you.</summary>
        public static void Delete(Player player, LoadoutListing listing)
        {
            if (!CanAct(player)) return;

            if (listing.Chest == null)
            {
                ForgetWorn(player, listing.Def);
                Message(player, $"Deleted '{listing.Def.Name}'.");
                return;
            }

            var chest = listing.Chest;
            chest.WithAccess(() =>
            {
                var inventory = player.GetInventory();
                var loadouts = chest.Load();
                var stored = loadouts.FirstOrDefault(l => l.Def.Id == listing.Def.Id && l.Def.Owner == player.GetPlayerID());
                if (stored == null)
                {
                    ForgetWorn(player, listing.Def);
                    return;
                }

                var free = FreeBagSlots(inventory, new HashSet<ItemDrop.ItemData>());
                if (free.Count < stored.Items.Count)
                {
                    Message(player, $"Need {stored.Items.Count - free.Count} more free inventory slots to take '{listing.Def.Name}' apart.");
                    return;
                }

                var items = stored.Items;
                loadouts.Remove(stored);
                if (!chest.Save(loadouts))
                {
                    Message(player, "Couldn't write to the Loadout Chest; nothing was changed.");
                    return;
                }

                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    Tags.Clear(item);
                    item.m_equipped = false;
                    item.m_gridPos = free[i];
                    inventory.m_inventory.Add(item);
                }
                inventory.Changed();
                ForgetWorn(player, listing.Def);
                Message(player, $"Deleted '{listing.Def.Name}'.");
            });
        }

        private static void ForgetWorn(Player player, LoadoutDef def)
        {
            var worn = GetWorn(player);
            if (worn == null || worn.Id != def.Id) return;
            foreach (var item in player.GetInventory().m_inventory)
                if (Tags.LoadoutOf(item) == def.Id) Tags.Clear(item);
            SetWorn(player, null);
        }

        // ---------------------------------------------------------------- helpers: gear

        private static IEnumerable<ItemDrop.ItemData> EquippedItems(Player p) =>
            new[]
            {
                p.m_rightItem, p.m_leftItem, p.m_hiddenRightItem, p.m_hiddenLeftItem,
                p.m_helmetItem, p.m_chestItem, p.m_legItem, p.m_shoulderItem,
                p.m_utilityItem, p.m_trinketItem, p.m_ammoItem,
            }.Where(i => i != null).Distinct();

        /// <summary>Equipped items plus everything in the hotbar row (skipping items the game can't save).</summary>
        private static List<ItemDrop.ItemData> CurrentGear(Player player)
        {
            var gear = EquippedItems(player).ToList();
            foreach (var item in player.GetInventory().m_inventory)
                if (item.m_gridPos.y == 0 && !gear.Contains(item)) gear.Add(item);
            // Items without a prefab would be dropped by Inventory.Save, so they stay with the player.
            gear.RemoveAll(i => i.m_dropPrefab == null);
            return gear;
        }

        /// <summary>
        /// Clears tags from items belonging to a loadout you're not wearing (e.g. gear recovered from
        /// a gravestone after switching), so they behave like normal items again.
        /// </summary>
        private static void ClearOrphanTags(Player player, LoadoutDef worn)
        {
            foreach (var item in player.GetInventory().m_inventory)
            {
                var id = Tags.LoadoutOf(item);
                if (id != null && (worn == null || id != worn.Id)) Tags.Clear(item);
            }
        }

        private static LoadoutDef NewDef(Player player, string name) =>
            new LoadoutDef { Id = LoadoutDef.NewId(), Name = name, Owner = player.GetPlayerID() };

        /// <summary>Records each item in the definition (slot, quantity) and tags it.</summary>
        private static void Capture(LoadoutDef def, List<ItemDrop.ItemData> items)
        {
            def.Entries.Clear();
            foreach (var item in items)
            {
                var entry = new LoadoutEntry
                {
                    Prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "",
                    Quality = item.m_quality,
                    Variant = item.m_variant,
                    Equipped = item.m_equipped || IsHidden(item),
                    HotbarX = item.m_gridPos.y == 0 ? item.m_gridPos.x : -1,
                    Target = item.m_stack,
                    Stackable = item.m_shared.m_maxStackSize > 1,
                };
                Tags.Set(item, def.Id, def.Entries.Count);
                def.Entries.Add(entry);
            }
        }

        private static bool IsHidden(ItemDrop.ItemData item)
        {
            var p = Player.m_localPlayer;
            return p != null && (item == p.m_hiddenLeftItem || item == p.m_hiddenRightItem);
        }

        private static void RemoveFromPlayer(Player player, List<ItemDrop.ItemData> items)
        {
            if (items.Count == 0) return;
            var inventory = player.GetInventory();
            foreach (var item in items)
            {
                if (!inventory.m_inventory.Contains(item)) continue; // e.g. a stack restocking just created
                if (player.IsItemEquiped(item)) player.UnequipItem(item, false);
                if (item == player.m_hiddenLeftItem) player.m_hiddenLeftItem = null;
                if (item == player.m_hiddenRightItem) player.m_hiddenRightItem = null;
                item.m_equipped = false;
                inventory.m_inventory.Remove(item);
            }
            inventory.Changed();
        }

        private static void Upsert(List<StoredLoadout> loadouts, LoadoutDef def, List<ItemDrop.ItemData> items)
        {
            var existing = loadouts.FirstOrDefault(l => l.Def.Id == def.Id);
            if (existing == null) loadouts.Add(existing = new StoredLoadout());
            existing.Def = def;
            existing.Items = items;
            existing.CheckedOut = false;
        }

        private static string UniqueName(Player player, BaseArea area, string name)
        {
            var taken = new HashSet<string>(List(player, area).Select(l => l.Def.Name), StringComparer.CurrentCultureIgnoreCase);
            if (!taken.Contains(name)) return name;
            for (int n = 2; ; n++)
                if (!taken.Contains($"{name} {n}")) return $"{name} {n}";
        }

        /// <summary>Last-resort safety net: put items in the player's inventory, or at their feet.</summary>
        private static void GiveOrDrop(Player player, List<ItemDrop.ItemData> items)
        {
            foreach (var item in items)
            {
                Tags.Clear(item);
                if (!player.GetInventory().AddItem(item))
                    ItemDrop.DropItem(item, item.m_stack, player.transform.position + Vector3.up, Quaternion.identity);
            }
        }

        // ---------------------------------------------------------------- helpers: restocking

        private static int MissingStackEntries(LoadoutDef def, List<ItemDrop.ItemData> items)
        {
            if (!GearHeimPlugin.RefillFromChests.Value) return 0;
            var present = new HashSet<int>(items.Select(Tags.EntryOf));
            return def.Entries.Where((e, i) => e.Stackable && !present.Contains(i)).Count();
        }

        /// <summary>
        /// Tops stackable entries up to their saved amounts from chests in the base, and lists
        /// anything still short (including lost gear) in <paramref name="missing"/>. New stacks it
        /// creates are added to <paramref name="items"/> and also to <paramref name="created"/>; with
        /// <paramref name="trackTopUps"/>, top-ups to existing stacks are recorded there too (as
        /// untagged copies), so everything pulled can be handed back if the operation is abandoned.
        /// </summary>
        private static void Restock(Player player, BaseArea area, LoadoutDef def, List<ItemDrop.ItemData> items,
            List<string> missing, List<ItemDrop.ItemData> created, bool trackTopUps)
        {
            for (int index = 0; index < def.Entries.Count; index++)
            {
                var entry = def.Entries[index];
                var stacks = items.Where(i => Tags.EntryOf(i) == index).ToList();

                if (!entry.Stackable)
                {
                    if (stacks.Count == 0) missing.Add(DisplayName(entry.Prefab));
                    continue;
                }

                int need = entry.Target - stacks.Sum(i => i.m_stack);
                if (need <= 0) continue;

                if (GearHeimPlugin.RefillFromChests.Value)
                {
                    // Match the existing stack's world level so stacks never mix levels.
                    int worldLevel = stacks.Count > 0 ? stacks[0].m_worldLevel : -1;
                    int got = ChestSupply.Take(player, area, entry, worldLevel, need, out var template);
                    if (got > 0)
                    {
                        if (stacks.Count > 0)
                        {
                            stacks[0].m_stack += got;
                            if (trackTopUps)
                            {
                                var refund = stacks[0].Clone();
                                refund.m_stack = got;
                                created.Add(refund);
                            }
                        }
                        else
                        {
                            var stack = template.Clone();
                            stack.m_stack = got;
                            stack.m_equipped = false;
                            Tags.Set(stack, def.Id, index);
                            items.Add(stack);
                            created.Add(stack);
                        }
                        need -= got;
                    }
                }

                if (need > 0) missing.Add($"{need} {DisplayName(entry.Prefab)}");
            }
        }

        private static string DisplayName(string prefab)
        {
            var go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            var drop = go != null ? go.GetComponent<ItemDrop>() : null;
            return drop != null ? Localization.instance.Localize(drop.m_itemData.m_shared.m_name) : prefab;
        }

        private static void Report(Player player, LoadoutDef def, List<string> missing, string verb)
        {
            if (def == null || missing.Count == 0) return;
            player.Message(MessageHud.MessageType.TopLeft,
                $"'{def.Name}' {verb} incomplete. Missing: {string.Join(", ", missing)}");
        }

        // ---------------------------------------------------------------- helpers: placement

        private class Placement
        {
            public readonly List<(ItemDrop.ItemData item, Vector2i pos)> Moves = new List<(ItemDrop.ItemData, Vector2i)>();
            public readonly List<(ItemDrop.ItemData item, Vector2i pos)> Incoming = new List<(ItemDrop.ItemData, Vector2i)>();
        }

        /// <summary>
        /// Works out where every incoming item goes once the outgoing items are gone: hotbar items
        /// return to their hotbar slot (bumping whatever is there into the bag), everything else
        /// takes a free bag slot.
        /// </summary>
        private static bool PlanPlacement(Player player, LoadoutDef def, List<ItemDrop.ItemData> incoming,
            List<ItemDrop.ItemData> outgoing, int extraStacks, out Placement plan, out string why)
        {
            plan = new Placement();
            why = null;
            var inventory = player.GetInventory();
            var leaving = new HashSet<ItemDrop.ItemData>(outgoing);
            var staying = inventory.m_inventory.Where(i => !leaving.Contains(i)).ToList();

            var hotbarClaims = new Dictionary<int, ItemDrop.ItemData>();
            var toBag = new List<ItemDrop.ItemData>();
            foreach (var item in incoming)
            {
                int index = Tags.EntryOf(item);
                int x = index >= 0 && index < def.Entries.Count ? def.Entries[index].HotbarX : -1;
                if (x >= 0 && x < inventory.GetWidth() && !hotbarClaims.ContainsKey(x)) hotbarClaims[x] = item;
                else toBag.Add(item);
            }

            var bumped = staying.Where(i => i.m_gridPos.y == 0 && hotbarClaims.ContainsKey(i.m_gridPos.x)).ToList();
            var free = FreeBagSlots(inventory, leaving);
            int needed = toBag.Count + bumped.Count + extraStacks;
            if (free.Count < needed)
            {
                why = $"Not enough inventory space: free up {needed - free.Count} more slot(s).";
                return false;
            }

            int next = 0;
            foreach (var item in bumped) plan.Moves.Add((item, free[next++]));
            foreach (var pair in hotbarClaims) plan.Incoming.Add((pair.Value, new Vector2i(pair.Key, 0)));
            foreach (var item in toBag) plan.Incoming.Add((item, free[next++]));
            return true;
        }

        /// <summary>Empty slots below the hotbar row, treating <paramref name="leaving"/> items as already gone.</summary>
        private static List<Vector2i> FreeBagSlots(Inventory inventory, HashSet<ItemDrop.ItemData> leaving)
        {
            var taken = new HashSet<Vector2i>(inventory.m_inventory.Where(i => !leaving.Contains(i)).Select(i => i.m_gridPos));
            var free = new List<Vector2i>();
            for (int y = 1; y < inventory.GetHeight(); y++)
                for (int x = 0; x < inventory.GetWidth(); x++)
                {
                    var pos = new Vector2i(x, y);
                    if (!taken.Contains(pos)) free.Add(pos);
                }
            return free;
        }

        private static void Apply(Player player, LoadoutDef def, Placement plan)
        {
            var inventory = player.GetInventory();
            foreach (var (item, pos) in plan.Moves) item.m_gridPos = pos;
            foreach (var (item, pos) in plan.Incoming)
            {
                item.m_equipped = false;
                item.m_gridPos = pos;
                inventory.m_inventory.Add(item);
            }
            inventory.Changed();

            foreach (var (item, _) in plan.Incoming)
            {
                int index = Tags.EntryOf(item);
                if (index >= 0 && index < def.Entries.Count && def.Entries[index].Equipped)
                    player.EquipItem(item, true);
            }
        }

        // ---------------------------------------------------------------- helpers: checks & messages

        private static bool CanAct(Player player)
        {
            if (player == null || player.IsDead() || player.IsTeleporting()) return false;
            if (player.InAttack() || player.InDodge())
            {
                Message(player, "Can't change loadouts while fighting.");
                return false;
            }
            if (player.IsSwimming())
            {
                Message(player, "Can't change loadouts while swimming.");
                return false;
            }
            if (InventoryGui.instance != null && InventoryGui.instance.m_dragItem != null)
            {
                Message(player, "Put down the item you're holding first.");
                return false;
            }
            return true;
        }

        private static bool RequireArea(Player player, BaseArea area)
        {
            if (area.IsValid) return true;
            Message(player, "You need to be in your base (near a workbench) to change loadouts.");
            return false;
        }

        internal static void Message(Player player, string text)
        {
            if (player != null) player.Message(MessageHud.MessageType.Center, text);
        }
    }
}
