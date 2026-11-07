# Quests - design (built 2026-10-27)

Jobs for agents that appear on fields. Agents on a quest are stuck on its field until it ends.
Decisions below were picked by the user; items marked *(default)* are my proposals, open to change.

## Lifecycle

1. **Spawn** - a random quest appears on an explored field that fits it (Village, forest, cave/mine,
   bridge/road, and also the player's own castle field). About one every 75-105 seconds (first after 30s), at most 3
   random quests available at once. Quests stay near the castle: a level L quest appears at most
   L + 5 steps (field connections) away (Lv1 = 6 ... Lv5 = 10), and each step away makes a field less likely
   (weight / (1 + 0.5 x steps)), so low quests cluster around the castle.
   As the game goes on they move out: each level of the best agent above 1 pushes quests that are not on
   the castle itself one step further out (nearest and farthest step both), up to 4 steps (agent Lv5).
   No repeats: random quests come from a shuffle bag. A quest leaves the bag when it appears, and only
   quests still in the bag can appear (and never one already open). When nothing left in the bag can
   appear, the bag is refilled. Each castle building with a quest pool has its own bag. Map files can also place fixed story quests (no timer) on fields.
2. **Deadline** - most quests have a timer that starts when they appear; the whole quest must be done
   before it ends (the visitor leaves town). Running out mid-quest fails the current stage.
   Some quests have no timer. Timers by level: Lv1 6:00, Lv2 7:00, Lv3 8:00, Lv4 9:00, Lv5 10:00
   (`DEADLINE_PER_LEVEL` in tools/quests/make_quests.py).
3. **Assign** - only agents standing on the quest field can be assigned. A quest says how many agents
   it takes (min/max, usually 1). Gates: minimum level and hard minimum attributes.
4. **Work** - the quest runs through its stages in order. Assigned agents cannot move or use work
   abilities *(default)* until the quest ends. They can **Abandon** at any time: the quest is lost,
   no failure consequence.
5. **End** - success gives the rewards; failure applies the failed stage's consequence.

## Levels and biomes

Every quest has a **level 1-5** and a list of **biomes** it can appear in (Village, Forest = spring and
autumn forests, Mountain = caves and ore mines, Plains = grass, roads, crossroads and bridges, Castle).
- Stages per level: Lv1 = 1 roll, Lv2 = 2 rolls, Lv3 = 3 stages (battles allowed from here),
  Lv4 = 3 hard rolls, Lv5 = 4 stages. Difficulty about 3 at Lv1 up to 6 at Lv4-5.
- Random quests appear up to **one level above your best agent** (level 1 agents: Lv1-2 quests).
- Rewards scale: resources and XP x(1 + 0.5 per level above 1), so Lv3 = 2x, Lv5 = 3x the listed base.
- Quest XP is also multiplied by 10 on top (`QuestReward.ExperienceBonus`), because agents are busy for minutes;
  the XP in the tables below is before that bonus. It is paid in equal parts, one for each stage passed or fight won
  (a failed stage that the quest carries on past pays nothing).
- The level shows on the map marker (corner number), the field panel, the hover card and the window.

## Stages

Each stage has a name, a flavour line, a duration *(default: ~20-40 s)* and one of two kinds:

- **Roll** - weighted attribute mix vs. difficulty, e.g. `Strike: DEA x0.7 + QUI x0.3, difficulty 6`.
  Score = weighted sum of the agent's attributes. Chance: score = difficulty -> 75%, +10% per point
  above (100% at +3), -15% per point below, never under 5%. E.g. STR 8 vs 5 = 100%, STR 4 vs 5 = 60%.
  With a team, the best assigned agent for that stage rolls it.
  A roll stage takes 90s of work, or 120s when it is the quest's only roll stage (`ROLL_SECONDS`).
- **Battle** - spawns neutral enemies on the field; the quest waits with a **Fight!** button (deadline
  keeps running) and opens the normal battle screen with the quest's agents. Win = stage passed.

Each stage sets what its failure does: **end the quest**, **retry after a delay**, or **injure and
continue** (reward kept). Injury = damage to the agent that rolled; dangerous stages add a death
chance. Most quests fail with no consequence beyond lost time.

## Rewards (first version)

Resources, XP to the agents, a new unit that joins (ignores agent caps), field perks added or removed,
neutral guards removed from the field, an item (a named one or a random one of a tier, see ITEMS_DESIGN.md).

## UI

- **Map markers** - `!` on fields with quests: gold = available, blue = in progress, red = expiring.
- **Field panel section** - the field's quests with their timers.
- **Quest window** - opened from the field panel or the marker: story, stages with odds per agent,
  gates, timer, rewards, Assign / Abandon / Fight buttons.
- **Toasts** - new quest, stage passed/failed, agent injured/killed, fight ready, quest done/expired.

## Data *(default)*

- `QuestDefinition` (.tres in `resources/quests/`): title, story, level, biomes, deadline
  (0 = none), spawn weight, min level, min attributes, min/max agents, stages, rewards.
- `QuestStage`: name, text, duration, attribute weights + difficulty or enemy templates (battle),
  failure effect (end / retry delay / injure), damage, death chance.
- `QuestManager` on the global tick: spawning, deadlines, stage timers, rolls.
- Map JSON field entry gets `"quests": ["quest_id"]` for story quests; the map editor can set it.

## Quests (16)

Quest files are generated by `tools/quests/make_quests.py` (edit the table there and re-run, or edit the
.tres in Godot). The script checks the stage count per level and that battles start at level 3.

| Lv | Quest | Biome | Stages | Reward (after level scaling) |
|---|---|---|---|---|
| 1 | Pick a Purse | Village | Cut the purse (QUI) | 30 gold |
| 1 | Gather Nightshade | Forest | Find the plants (CLE) | 15 mana |
| 1 | Rob a Traveller | Plains | Jump him (DEA/QUI) | 25 gold |
| 1 | Prospect the Tunnels | Mountain | Search the tunnels (CLE/STR) | 25 ore |
| 1 | Drill in the Courtyard | Castle | Take the drill (STR/DUR) | 40 XP |
| 2 | Steal the Ring | Village | Blend in (CLE/QUI) - Lift the ring (QUI) | 75 gold |
| 2 | The Lost Wolf Pup | Forest | Find it (CLE) - Calm it (CLE/DUR) | Wolf joins |
| 2 | Deserter at the Gate | Castle | Question him (CLE) - Test his loyalty (STR/DUR) | Night Watchman joins |
| 2 | Raid the Dwarf Stores | Mountain | Sneak past (QUI) - Haul the crates (STR) | 60 ore, 22 crystal |
| 2 | Extort the Toll | Plains | Block the road (DEA/STR) - Count the coin (CLE) | 60 gold |
| 3 | Hunt the Wolf Pack | Forest, 1-3 agents | Track - **Fight** 3 wolves - Skin the pelts | 60 gold, 30 wood |
| 3 | Ambush the Merchant Caravan | Plains, 2-3 agents | Lie in wait - **Fight** escort - Loot the wagons | 100 gold, 50 ore |
| 3 | Burn the Granary | Village | Sneak in - Set the fire - Escape | town Raided 240s |
| 3 | The Buried Relic | Mountain, CLE 3+ | Find the way - Dig it out - Escape the collapse | 40 crystal, 30 mana, Ore Vein |
| 4 | Assassinate the Baron | Village, DEA 4+ | Get close - Strike (death chance) - Escape | 150 gold, Sergeant leaves |
| 5 | Slay the Elder Treant | Forest, 2-4 agents | Find the grove - Break the wards - **Fight** treant, druid, 2 elves - Burn the heartwood | 150 gold, 120 mana, forest loses Old Growth |

**Arena** (handed out only by the castle's Arena building, see CASTLE_FACTIONS_DESIGN.md; levels 2-4, the only quests allowed a battle below level 3). One round per level above 1,
1v1 (exactly 1 agent) or 2v2 (exactly 2 agents). Levels 2-3 start with a "Sign the roll" entry roll; level 4 is three fights.
Damage carries between rounds (no healing), and losing a round means death, as in any battle.

| Lv | Quest | Rounds | Reward |
|---|---|---|---|
| 2 | Arena: First Blood (1v1) | militia | 45 gold, 60 XP |
| 3 | Arena: The Gauntlet (1v1) | night watchman - elf blade dancer | 70 gold, 90 XP |
| 4 | Arena: Champion's Duel (1v1) | dwarf axeman - night watchman - sergeant | 112 gold, 125 XP |
| 2 | Arena: Pair Fight (2v2) | militia + archer | 45 gold, 60 XP |
| 3 | Arena: Twin Blades (2v2) | 2 militia - dwarf axeman + crossbowman | 70 gold, 90 XP |
| 4 | Arena: Grand Melee (2v2) | watchman + archer - blade dancer + elf archer - sergeant + dwarf axeman | 112 gold, 125 XP |

**Escaped Prisoner** (one of the Dark Lord's prisoners broke out of the dungeon; handed out only by the castle's Prison)

| Lv | Quest | Where | Stages | Reward (base) |
|---|---|---|---|---|
| 1 | Escaped Prisoner | Plains, Forest | Find him (CLE/QUI) | 20 gold, 25 XP |
| 2 | Escaped Prisoner: Silence Him | Plains, Forest, Mountain | Track him down - Kill him (DEA/QUI) | 35 gold, 35 XP |
| 3 | Escaped Prisoner: The Sheltered Man | Village, Plains, 1-2 agents | Find the hiding place - **Fight** 2 militia + archer - Drag him back (STR/DUR) | 40 gold, 15 mana, 40 XP |

**Thefts and killings in town**

| Lv | Quest | Where | Stages | Reward (base) |
|---|---|---|---|---|
| 2 | Steal the Blacksmith's Sword | Village | Distract the smith (CLE/STR) - Take the sword (QUI/STR) | 30 gold, 30 ore |
| 3 | Steal from the Mage | Village, City, CLE 4+ | Study his habits (CLE) - Unpick the wards (CLE 6) - Take the grimoire (CLE/QUI) | 40 mana, 20 crystal |
| 3 | Assassinate the Priest | Village, City, DEA 3+ | Enter the temple (CLE) - Strike during the prayer (DEA, death chance) - Slip out (QUI/CLE) | 30 gold, 30 mana |

**Caravans**

| Lv | Quest | Where | Stages | Reward (base) |
|---|---|---|---|---|
| 2 | Rob the Farmers' Cart | Plains | Wait by the road (QUI) - Scare them off (DEA/STR) | 20 gold, 25 wood |
| 3 | Ambush the Merchant Caravan | Plains, 2-3 agents | (see above) | 50 gold, 25 ore |
| 3 | Ambush the Elven Envoy | Forest, Plains, 1-3 agents | Find their path (CLE) - **Fight** blade dancer + 2 archers - Take the gifts | 25 crystal, 25 mana |
| 4 | Ambush the Dwarf Ore Train | Mountain, Plains, 2-3 agents | Block the pass (STR) - **Fight** 2 axemen + crossbowman - Haul the ore | 60 ore, 20 crystal |
| 5 | Ambush the Royal Treasury | Plains, 2-4 agents | Learn the route - Lie in wait - **Fight** sergeant, watchman, militia, 2 archers - Escape | 100 gold |

**One attribute only** (every stage checks the same attribute)

| Attr | Lv1 | Lv2 |
|---|---|---|
| STR | Haul the Timber (Forest, 30 wood) | Shift the Boulder (Mountain, 30 ore, 10 crystal) |
| CLE | Decipher the Old Map (Castle, 15 gold, 10 crystal) | Forge the Royal Seal (Village/City, 45 gold) |
| DEA | Cull the Wild Boar (Forest, 15 gold, 10 wood) | Silence the Witness (Village, 40 gold) |
| DUR | Endure the Initiation (Castle, 45 XP) | Survive the Deep Shafts (Mountain, 25 crystal, 20 ore) |
| QUI | Catch the Messenger (Plains, 20 gold) | Steal the Lord's Horse (Village/Castle, 45 gold) |
