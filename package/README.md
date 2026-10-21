# GearHeim

**Save your gear as loadouts and swap between them anywhere in your base, with the food, potions and arrows topped back up from your chests every time.**

"Mining". "Boss fight". "Farming". "Sailing". Build a **Loadout Chest**, put your gear into it as a named loadout, and from then on a single keypress takes you from one set to another: weapons, armour, tools and the whole hotbar, restocked and ready.

<!-- IMAGE: loadout-menu.png (the loadout window with three or four loadouts listed) -->

## Contents

- [Getting started](#getting-started)
- [How switching works](#how-switching-works)
- [Examples](#examples)
- [Locked items](#locked-items)
- [Restocking](#restocking)
- [What counts as "your base"](#what-counts-as-your-base)
- [Multiplayer](#multiplayer)
- [Configuration](#configuration)
- [Troubleshooting](#troubleshooting)
- [Works with](#works-with)

## Getting started

1. Build a **Loadout Chest** (Hammer → Furniture; 10 Fine Wood, 2 Bronze) somewhere in your base.
2. Equip the gear you want and fill your hotbar, then open the chest (or press **L**), type a name and click **Save gear as new**. Your equipped gear and hotbar move into the chest as that loadout.
3. Anywhere in the base, press **L** and click **Equip**, or press **Alt+1 … Alt+5**, to switch.

<!-- IMAGE: loadout-chest.png (the Loadout Chest built in a base) -->

## How switching works

- The loadout you switch **to** comes out of the chest, goes back into the same hotbar slots, and equips itself.
- The loadout you switch **from** goes back into the chest. If what you were wearing wasn't a saved loadout, it's saved as **Last worn**, so nothing is ever lost.
- If your inventory doesn't have room, nothing moves and you're told how many slots to free.
- Your loadouts keep their **hotbar layout**: your sword stays on slot 1, your bow on slot 2, your food on slot 5.

## Examples

**Mining.** Pickaxe on slot 1, light armour, a stack of Cooked meat, Stamina mead, and a Wisplight on the hotbar. One click when you leave for the mine; one click when you come back to your fighting set.

**Boss fight.** Your best weapon and shield, the armour with the right resistances, healing and stamina meads, and a quiver of the strongest arrows. Before you leave the base for the altar, press **Alt+2** and you're ready.

**Farming and building.** Cultivator, Hoe, Hammer, a few Wood stacks. Leave your combat gear safely in the chest while you build.

**Sailing.** Fishing rod, bait, a mace for the unexpected Serpent, food for the trip.

**Same set, different arrows.** Make "Bow: fire" and "Bow: poison" loadouts that differ only in the quiver. Switch with a key.

**A short routine.** Sets 1 to 5 on **Alt+1 … Alt+5**, in the order shown in the menu (alphabetical). Name them so the order suits you, for example *1 Fight, 2 Mine, 3 Farm*.

## Locked items

Loadout items can't be taken out of the chest by hand, so they never get mixed up with your other stuff. To change a loadout, equip it, change your gear, then open the chest (or press **L**) and click **Save changes**.

- **Store it** puts the loadout you're wearing back without changing it.
- **Delete** gives you the items back, and removes the loadout.

## Restocking

Each loadout remembers how many of each stackable item it had: food, potions, arrows, bolts and so on. When a loadout goes back into the chest, and again when you equip it, GearHeim tops each stack up from **ordinary chests in the same base**. It only uses chests you could open yourself, and skips chests someone has open, gravestones, and other players' private chests. If it can't fill everything, it does what it can and tells you what's missing:

> 'Mining' equipped incomplete. Missing: 4 Cooked meat, 20 Wood arrow

So your "Boss fight" set always has 20 Wood arrows and 4 Cooked meat if you've got them stocked in a chest, without you counting them out.

## What counts as "your base"

By default it's the area covered by the **workbench you're near**. Workbenches whose ranges overlap count as one base, so a big base with several workbenches works as a whole. Prefer a fixed distance? Set `RangeMode` to `Distance` and choose a `Range` in meters.

## Multiplayer

Each player's loadouts are their own, even in a shared Loadout Chest. **Install GearHeim on the server as well as every client.** The Loadout Chest is a new buildable, and a server without the mod deletes pieces it doesn't recognise, along with what's in them. GearHeim refuses to connect if versions don't match.

## Configuration

`BepInEx/config/mbaya.GearHeim.cfg`, or Gale's config editor:

| Section | Key | Default | Notes |
| --- | --- | --- | --- |
| General | RangeMode | Workbench | `Workbench` = your base; `Distance` = within `Range` meters |
| General | Range | 50 | Meters, used in Distance mode |
| General | RefillFromChests | true | Restock stackables from chests in the base |
| Hotkeys | OpenMenu | L | |
| Hotkeys | SwitchToLoadout1 … 5 | Alt+1 … Alt+5 | Order is as listed in the menu (alphabetical) |

## Troubleshooting

- **The Loadout Chest isn't in the Hammer menu.** It's under Furniture. Check the mod is in the profile you launch from, and that you have the Fine Wood and Bronze.
- **"Not enough room."** Free the slots it names, then try again: nothing moves until everything fits.
- **A stack wasn't topped up.** GearHeim only takes from chests you could open yourself (not warded chests you're locked out of), and ignores a chest while someone else has it open. It also only looks at chests inside your base: see [what counts as your base](#what-counts-as-your-base).
- **My loadout items vanished from my hotbar.** They are in the Loadout Chest: open it and choose the loadout. Items that aren't in a loadout are saved as **Last worn** when you switch.
- **The server disconnects me.** Versions must match on the server and every client, and the server needs the mod too.
- **Something else.** Open an issue at [github.com/mattbaya/GearHeim](https://github.com/mattbaya/GearHeim) and attach `BepInEx/LogOutput.log`.

## Works with

- **[Bag O' Heim](https://thunderstore.io/c/valheim/p/Svaha/BagOHeim/)**: carry packs that refill themselves from the same chests. Bag O' Potions plus a Mining loadout means potions and food are always topped up.
- **[PortalHeim SG1](https://thunderstore.io/c/valheim/p/Svaha/PortalHeim_SG1/)**: switch to "Mining", step through a gate to the mine.
