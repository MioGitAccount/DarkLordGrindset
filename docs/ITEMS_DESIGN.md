# Items - design (built 2026-11-03)

Gear carried by agents (heroes later). Decisions below were picked by the user; items marked *(default)* are my proposals, open to change.

## Slots

An agent carries at most 3 items, one per slot:

| Slot | Kinds |
|---|---|
| **Hand** (one-handed) | Sword, Knife, Axe, Mace, Shield, Book, Staff |
| **Jewelry** | Ring, Bracelet, Necklace, Amulet, Coin |
| **Wear** (body *or* head) | Body: Cloak, Robe, Jerkin, Breastplate, Chainmail. Head: Helmet, Crown, Wizard cap, Hood |

## Tiers and stats

Stats are **fixed per item** (every Steel Sword is the same); variety comes from many named items.
An item can add ATK, DEF, HP and any of the five attributes (attributes then add their usual bonuses).

- **Tier 1** - one small bonus: 1-2 ATK, or 1 DEF, or 1-3 HP, or 1 attribute.
- **Tier 2** - 2-3 ATK, or a pair: 1 QUI + 1 ATK, 1 CLE + 2 HP, 5 HP...
- **Tier 3** - stronger mixes: 2 DUR + 6 HP, 4 ATK + 1 DEA, 3 CLE + 2 HP...
- More tiers can be added later (the tier frame colour and the guard loot table extend).

**Material** (iron, bronze, steel, silver, gold, leather, cloth, wood, bone) is flavour only: it names and tints the item.

All 48 items live in `game/items/ItemCatalog.cs` (16 per tier); add a line there to add an item.

## Getting items

- **Guard loot** - guarded fields sit on loot. At game start every guarded field rolls from its strongest guard's level:
  chance of loot Lv1 50%, Lv2 60%, Lv3 75%, Lv4 90%, Lv5 100% *(default)*; tier Lv1 = T1, Lv2 = 65% T1 / 35% T2,
  Lv3 = 75% T2 / 25% T3, Lv4 = 40% T2 / 60% T3, Lv5 = T3 *(default)*. A map file can set a field's loot by hand
  (`"loot": ["rune_sword"]`, `[]` = none; see maps/MAP_FORMAT.md).
- **Scouting** - loot shows as `?` until an agent stands on the field or on a neighbouring field; then the field
  panel and hover card show the item (or "no loot").
- **Claiming** - when the last guard is gone (battle won, or a quest removes them) the loot goes to the inventory.
  The battle result lists it and a "Found ..." toast appears.
- **Quests** - `QuestReward.ItemId` (a named item) or `ItemTier` (a random item of that tier). 16 quests give one,
  set in `tools/quests/make_quests.py` (`item=` / `itemtier=`): e.g. Steal the Ring -> Gold Ring, Steal the
  Blacksmith's Sword -> Steel Sword, Hunt the Wolf Pack -> Fur Cloak, the arenas and big ambushes -> tier 2-3.

## Inventory and equipping

- **Items** button in the top bar (or `I`, or "Your items..." in the Inspect window) opens the inventory. It stays
  open beside the Inspect window.
- **Drag** an item onto an agent's slot in the Inspect window or the unit panel (or anywhere on those panels - it
  goes to its slot). **Right-click** an inventory item gives it to the selected agent.
- An item in an occupied slot goes back to the inventory.
- **Lock** - a given item is locked on the agent for **60 s** (shown as a countdown on the slot): it can't be taken
  off or replaced until then, so items can't be cycled between agents.
- **Taking off** - after the lock: right-click the slot, or drag it to the inventory (or straight to another agent,
  where a new lock starts).
- **Death** - a dead agent's items go back to the inventory.

## Art

Code-drawn placeholder icons (`game/items/ItemIcon.cs`): a shape per kind tinted by material, a frame in the tier
colour (grey / blue / purple) and tier pips. Painted icons can replace them later.

## Code

- `game/items/ItemDefinition.cs` - slots, kinds, materials, `ItemDefinition`, `Item` (one owned item + lock).
- `game/items/ItemCatalog.cs` - all items, random by tier, guard loot rolls.
- `game/items/PlayerInventory.cs` - the inventory (static, like PlayerResources).
- `game/items/ItemSlotView.cs` - drag & drop slot, `ItemMoves` rules, `AgentItemsRow`.
- `game/UI/InventoryWindow.cs` - the inventory window.
- `unit.cs` - `Equip` / `Unequip` / `GetItem`; items are added in `RecalculateStats`, so battles use them as-is.
- `field.cs` - `Loot`, `LootRevealed`, claiming when the last guard leaves.
