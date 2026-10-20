using System;
using System.Collections.Generic;
using System.Linq;

namespace GearHeim
{
    /// <summary>One remembered item in a loadout: what it is, where it goes, and how many to keep.</summary>
    internal class LoadoutEntry
    {
        public string Prefab;
        public int Quality;
        public int Variant;
        public bool Equipped;
        /// <summary>Hotbar column (0-7) the item sat in, or -1 if it was elsewhere.</summary>
        public int HotbarX = -1;
        public int Target;
        public bool Stackable;

        public void Write(ZPackage pkg)
        {
            pkg.Write(Prefab ?? "");
            pkg.Write(Quality);
            pkg.Write(Variant);
            pkg.Write(Equipped);
            pkg.Write(HotbarX);
            pkg.Write(Target);
            pkg.Write(Stackable);
        }

        public static LoadoutEntry Read(ZPackage pkg) => new LoadoutEntry
        {
            Prefab = pkg.ReadString(),
            Quality = pkg.ReadInt(),
            Variant = pkg.ReadInt(),
            Equipped = pkg.ReadBool(),
            HotbarX = pkg.ReadInt(),
            Target = pkg.ReadInt(),
            Stackable = pkg.ReadBool(),
        };
    }

    /// <summary>A loadout's definition. Lives with the chest, and on the player while worn.</summary>
    internal class LoadoutDef
    {
        public string Id;
        public string Name;
        public long Owner;
        public List<LoadoutEntry> Entries = new List<LoadoutEntry>();

        public static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 12);

        public void Write(ZPackage pkg)
        {
            pkg.Write(Id);
            pkg.Write(Name);
            pkg.Write(Owner);
            pkg.Write(Entries.Count);
            foreach (var e in Entries) e.Write(pkg);
        }

        public static LoadoutDef Read(ZPackage pkg)
        {
            var def = new LoadoutDef { Id = pkg.ReadString(), Name = pkg.ReadString(), Owner = pkg.ReadLong() };
            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++) def.Entries.Add(LoadoutEntry.Read(pkg));
            return def;
        }

        public string ToBase64()
        {
            var pkg = new ZPackage();
            Write(pkg);
            return Convert.ToBase64String(pkg.GetArray());
        }

        public static LoadoutDef FromBase64(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            try { return Read(new ZPackage(Convert.FromBase64String(s))); }
            catch (Exception e)
            {
                GearHeimPlugin.Log.LogWarning($"Could not read worn loadout: {e.Message}");
                return null;
            }
        }
    }

    /// <summary>A loadout as stored in one chest. Items is empty while the loadout is being worn.</summary>
    internal class StoredLoadout
    {
        public LoadoutDef Def;
        public List<ItemDrop.ItemData> Items = new List<ItemDrop.ItemData>();
        public bool CheckedOut;
        /// <summary>Number of stored items; valid even when read without loading the items.</summary>
        public int ItemCount;
    }

    /// <summary>Tags written into each item's custom data so we know which loadout slot it belongs to.</summary>
    internal static class Tags
    {
        public const string Loadout = "GearHeim.Loadout";
        public const string Entry = "GearHeim.Entry";

        public static string LoadoutOf(ItemDrop.ItemData item) =>
            item.m_customData.TryGetValue(Loadout, out var id) ? id : null;

        public static int EntryOf(ItemDrop.ItemData item) =>
            item.m_customData.TryGetValue(Entry, out var s) && int.TryParse(s, out int i) ? i : -1;

        public static void Set(ItemDrop.ItemData item, string loadoutId, int entry)
        {
            item.m_customData[Loadout] = loadoutId;
            item.m_customData[Entry] = entry.ToString();
        }

        public static void Clear(ItemDrop.ItemData item)
        {
            item.m_customData.Remove(Loadout);
            item.m_customData.Remove(Entry);
        }
    }

    /// <summary>Serializes a chest's loadouts with the game's own item format.</summary>
    internal static class LoadoutStorage
    {
        private const int Version = 1;
        // Scratch inventory used only to reuse Inventory.Save/Load; big enough for any loadout.
        private const int ScratchWidth = 8;
        private const int ScratchHeight = 32;

        public static byte[] Write(List<StoredLoadout> loadouts)
        {
            var pkg = new ZPackage();
            pkg.Write(Version);
            pkg.Write(loadouts.Count);
            foreach (var l in loadouts)
            {
                l.Def.Write(pkg);
                pkg.Write(l.CheckedOut);
                pkg.Write(SaveItems(l.Items));
            }
            return pkg.GetArray();
        }

        /// <param name="withItems">False reads names and counts only, without creating item objects (cheap).</param>
        public static List<StoredLoadout> Read(byte[] data, bool withItems = true)
        {
            var result = new List<StoredLoadout>();
            if (data == null || data.Length == 0) return result;

            var pkg = new ZPackage(data);
            int version = pkg.ReadInt();
            // Throw rather than return nothing: the chest then refuses to save, so data written by a
            // newer GearHeim is never overwritten with an empty list.
            if (version != Version)
                throw new InvalidOperationException($"Unknown loadout data version {version} (made by a newer GearHeim?)");

            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var def = LoadoutDef.Read(pkg);
                bool checkedOut = pkg.ReadBool();
                var bytes = pkg.ReadByteArray();
                var items = withItems ? LoadItems(bytes) : new List<ItemDrop.ItemData>();
                result.Add(new StoredLoadout
                {
                    Def = def,
                    CheckedOut = checkedOut,
                    Items = items,
                    ItemCount = withItems ? items.Count : CountItems(bytes),
                });
            }
            return result;
        }

        private static byte[] SaveItems(List<ItemDrop.ItemData> items)
        {
            var inventory = new Inventory("GearHeim", null, ScratchWidth, ScratchHeight);
            for (int i = 0; i < items.Count; i++)
            {
                var copy = items[i].Clone();
                copy.m_equipped = false;
                copy.m_gridPos = new Vector2i(i % ScratchWidth, i / ScratchWidth);
                inventory.m_inventory.Add(copy);
            }
            var pkg = new ZPackage();
            inventory.Save(pkg);
            return pkg.GetArray();
        }

        // Inventory.Save writes an int version followed by a ushort item count.
        private static int CountItems(byte[] data)
        {
            if (data == null || data.Length < 6) return 0;
            var pkg = new ZPackage(data);
            pkg.ReadInt();
            return pkg.ReadUShort();
        }

        private static List<ItemDrop.ItemData> LoadItems(byte[] data)
        {
            var inventory = new Inventory("GearHeim", null, ScratchWidth, ScratchHeight);
            if (data != null && data.Length > 0) inventory.Load(new ZPackage(data));
            return inventory.m_inventory.ToList();
        }
    }
}
