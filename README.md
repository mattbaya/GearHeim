# GearHeim

A [BepInEx](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) + [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/) mod for Valheim: gear loadouts locked in a buildable Loadout Chest, switchable from anywhere in your base, with stackables restocked from your chests.

Player-facing docs: [package/README.md](package/README.md).

## Layout

| Path | Purpose |
| --- | --- |
| `src/` | Mod source (`GearHeim.csproj`, targets `netstandard2.1`) |
| `package/` | Thunderstore files: `manifest.json`, `README.md`, `CHANGELOG.md`, `icon.png` |
| `build.sh` | Builds and zips a Thunderstore package into `dist/` |
| `libs/` | Game assemblies and `Jotunn.dll` to compile against (**git-ignored, never commit**) |

## Building

1. Install the .NET 8 SDK (or newer).
2. Put Valheim's managed assemblies in `libs/Managed/` (or set `VALHEIM_MANAGED`): `assembly_valheim`, `assembly_guiutils`, `assembly_utils`, `UnityEngine`, `UnityEngine.CoreModule`, `UnityEngine.AssetBundleModule`, `UnityEngine.InputLegacyModule`, `UnityEngine.PhysicsModule`, `UnityEngine.TextRenderingModule`, `UnityEngine.UI`, `UnityEngine.UIModule`, `Unity.TextMeshPro`.
   No game install? The free dedicated server has them:
   `steamcmd +@sSteamCmdForcePlatformType windows +force_install_dir ./valheim_server +login anonymous +app_update 896660 +quit`
3. Put `Jotunn.dll` (from the [JotunnLib NuGet package](https://www.nuget.org/packages/JotunnLib), `lib/net462/`) in `libs/`.
4. Run `./build.sh` → `dist/GearHeim-<version>.zip`.

## How it stores things

- Loadouts live in the Loadout Chest's ZDO (`GearHeim.Loadouts`), serialized with the game's own `Inventory.Save`, so nothing can be dragged out by hand.
- Items carry `GearHeim.Loadout` / `GearHeim.Entry` tags in `ItemData.m_customData`.
- The loadout you're wearing is remembered in `Player.m_customData["GearHeim.Worn"]` (saved with your character).

## Contributing

Bug reports and pull requests are welcome. Please open an issue first for anything larger than a small fix. Releases to Thunderstore are published by the maintainer.
