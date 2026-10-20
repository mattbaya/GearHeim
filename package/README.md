# GearHeim

Save your gear as **loadouts**: "Mining", "Boss fight", "Farming". They're kept locked in a **Loadout Chest**, and you can switch between them from **anywhere in your base**. Food, potions and arrows are topped back up from your chests every time.

## Getting started

1. Build a **Loadout Chest** (Hammer → Furniture; 10 Fine Wood, 2 Bronze) somewhere in your base.
2. Equip the gear you want and fill your hotbar, then open the chest (or press **L**), type a name and click **Save gear as new**. Your equipped gear and hotbar move into the chest as that loadout.
3. Anywhere in the base, press **L** and click **Equip**, or press **Alt+1 … Alt+5**, to switch.

## How switching works

- The loadout you switch **to** comes out of the chest, goes back into the same hotbar slots, and equips itself.
- The loadout you switch **from** goes back into the chest. If what you were wearing wasn't a saved loadout, it's saved as **Last worn**.
- If your inventory doesn't have room, nothing moves and you're told how many slots to free.

## Locked items

Loadout items can't be taken out of the chest by hand. To change a loadout, equip it, change your gear, then open the chest (or press **L**) and click **Save changes**. **Store it** puts the loadout you're wearing back without changing it. **Delete** gives you the items back.

## Restocking

Each loadout remembers how many of each stackable item it had: food, potions, arrows, bolts and so on. When a loadout goes back into the chest, and again when you equip it, GearHeim tops each stack up from **ordinary chests in the same base**. It only uses chests you could open yourself, and skips chests someone has open, gravestones, and other players' private chests. If it can't fill everything, it does what it can and tells you what's missing:

> 'Mining' equipped incomplete. Missing: 4 Cooked meat, 20 Wood arrow

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
