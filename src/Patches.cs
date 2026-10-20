using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace GearHeim
{
    [HarmonyPatch(typeof(Piece), nameof(Piece.CanBeRemoved))]
    internal static class PieceCanBeRemovedPatch
    {
        // Like a normal chest, a Loadout Chest can't be deconstructed while it still holds items.
        private static void Postfix(Piece __instance, ref bool __result)
        {
            if (!__result) return;
            var chest = __instance.GetComponent<LoadoutChest>();
            if (chest != null && chest.HasStoredItems()) __result = false;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.UseHotbarItem))]
    internal static class UseHotbarItemPatch
    {
        // Vanilla uses hotbar slot N on the number key regardless of modifiers, so Alt+1 would
        // both switch loadouts and eat/equip slot 1. Skip the hotbar use while a loadout
        // hotkey's modifiers are held.
        private static bool Prefix(int index)
        {
            // Hotbar slot N is number key N; only skip it if a loadout hotkey is that same number key.
            var numberKey = KeyCode.Alpha0 + index;
            foreach (var key in GearHeimPlugin.QuickKeys)
            {
                var shortcut = key.Value;
                if (shortcut.MainKey != numberKey || !shortcut.Modifiers.Any()) continue;
                if (shortcut.Modifiers.All(Input.GetKey)) return false;
            }
            return true;
        }
    }
}
