using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace GearHeim
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // The Loadout Chest is a new buildable piece, so every player needs the mod to see it.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class GearHeimPlugin : BaseUnityPlugin
    {
        public const string Guid = "mbaya.GearHeim";
        public const string Name = "GearHeim";
        public const string Version = "1.0.0";

        public const string ChestPrefab = "piece_gearheim_loadoutchest";
        private const string ChestBasePrefab = "piece_chest";

        internal static ManualLogSource Log;

        internal static ConfigEntry<RangeMode> Mode;
        internal static ConfigEntry<float> Range;
        internal static ConfigEntry<bool> RefillFromChests;
        internal static ConfigEntry<KeyboardShortcut> MenuKey;
        internal static ConfigEntry<KeyboardShortcut>[] QuickKeys;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            Mode = Config.Bind("General", "RangeMode", RangeMode.Workbench,
                "Where GearHeim works. Workbench = inside your base: within build range of the workbench you're near " +
                "(workbenches with overlapping ranges count as one base). Distance = within 'Range' meters of you.");
            Range = Config.Bind("General", "Range", 50f,
                "Used when RangeMode is Distance: how far (in meters, straight line including height) Loadout Chests " +
                "and restocking chests can be.");
            RefillFromChests = Config.Bind("General", "RefillFromChests", true,
                "Top up food, potions, ammo and other stackables to their saved amounts from ordinary chests in the same base.");
            MenuKey = Config.Bind("Hotkeys", "OpenMenu", new KeyboardShortcut(KeyCode.L),
                "Open the loadout menu (works anywhere in a base with a Loadout Chest).");

            QuickKeys = new ConfigEntry<KeyboardShortcut>[5];
            for (int i = 0; i < QuickKeys.Length; i++)
            {
                QuickKeys[i] = Config.Bind("Hotkeys", $"SwitchToLoadout{i + 1}",
                    new KeyboardShortcut(KeyCode.Alpha1 + i, KeyCode.LeftAlt),
                    $"Switch straight to loadout #{i + 1} as listed in the loadout menu.");
            }

            PrefabManager.OnVanillaPrefabsAvailable += AddLoadoutChest;

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(GearHeimPlugin).Assembly);

            Log.LogInfo($"{Name} {Version} loaded");
        }

        private void AddLoadoutChest()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddLoadoutChest;

            var piece = new CustomPiece(ChestPrefab, ChestBasePrefab, new PieceConfig
            {
                Name = "Loadout Chest",
                Description = "Holds your gear loadouts. Switch loadouts from anywhere within range.",
                PieceTable = "Hammer",
                Category = "Furniture",
                Requirements = new[]
                {
                    new RequirementConfig("FineWood", 10),
                    new RequirementConfig("Bronze", 2),
                },
            });

            var prefab = piece.PiecePrefab;
            if (prefab == null)
            {
                Log.LogError($"Could not clone {ChestBasePrefab}; Loadout Chest disabled.");
                return;
            }

            // Loadouts live in the chest's own save data, not a normal chest inventory,
            // so nothing can be dragged out of it by hand.
            var container = prefab.GetComponent<Container>();
            if (container != null) Object.DestroyImmediate(container);
            prefab.AddComponent<LoadoutChest>();

            PieceManager.Instance.AddPiece(piece);
        }

        private void Update()
        {
            LoadoutMenu.Update();

            var player = Player.m_localPlayer;
            if (player == null || LoadoutMenu.IsTyping()) return;

            if (MenuKey.Value.IsDown())
            {
                LoadoutMenu.Toggle();
                return;
            }

            if (LoadoutMenu.IsOpen) return;
            for (int i = 0; i < QuickKeys.Length; i++)
            {
                if (QuickKeys[i].Value.IsDown())
                {
                    LoadoutService.SwitchToListed(player, i);
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
