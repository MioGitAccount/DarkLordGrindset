# Writes resources/quests/*.tres from the QUESTS table below (and removes quests no longer in it).
# Run from the project root: python tools/quests/make_quests.py
# Editing a quest by hand in Godot is fine too; re-running this overwrites the listed files.
import os

RES = {"gold": 0, "wood": 1, "ore": 2, "crystal": 3, "mana": 4}
ATTR = ["Deadliness", "Strength", "Durability", "Cleverness", "Quickness"]
SHORT = ["DEA", "STR", "DUR", "CLE", "QUI"]
FAILS = ["EndQuest", "Retry", "InjureContinue"]
# game/units/Biome.cs, in order
BIOMES = ["None", "Village", "Forest", "Mountain", "Plains", "Castle", "City"]
NEUTRALS = "res://unit_templates/neutral_unit_templates/"
PERKS = "res://resources/field_perks/"

# Level = number of stages: 1 -> one roll, 2 -> two rolls, 3 -> three (battles allowed from here),
# 4 -> three hard rolls, 5 -> four stages
STAGES_PER_LEVEL = {1: 1, 2: 2, 3: 3, 4: 3, 5: 4}


def roll(name, desc, weights, diff, dur=30, fail="EndQuest", retry=20, dmg=0, death=0.0):
    return dict(kind=0, name=name, desc=desc, weights=weights, diff=diff, dur=dur, fail=fail,
                retry=retry, dmg=dmg, death=death, enemies=[])


def battle(name, desc, enemies, fail="EndQuest"):
    return dict(kind=1, name=name, desc=desc, weights={}, diff=5, dur=0, fail=fail,
                retry=20, dmg=0, death=0.0, enemies=enemies)


ARENA = ["Village", "City", "Castle"]


def ENTRY(diff):
    return roll("Sign the roll", "Talk your way onto the fight card.", {"CLE": 0.5, "STR": 0.5}, diff, 20, "Retry", 15)


# Rewards are the level-1 base: the game multiplies resources and XP by 1 + 0.5 per level above 1
# (level 5 = 3x). Difficulty: 3 at level 1, up to 6 at levels 4-5. Meeting it exactly is 75%.
QUESTS = {
    # --- Level 1 -------------------------------------------------------------------------------
    "pick_a_purse": dict(
        title="Pick a Purse", level=1, biomes=["Village"], deadline=150, weight=1.2,
        story="Market day. Fat purses everywhere, and nobody watching them.",
        stages=[roll("Cut the purse", "Brush past, snip, walk on.", {"QUI": 1}, 3, 25, "EndQuest", dmg=2)],
        reward=dict(res={"gold": 30}, xp=20)),
    "gather_nightshade": dict(
        title="Gather Nightshade", level=1, biomes=["Forest"], deadline=0, weight=1,
        story="Deadly nightshade grows in the shade of these trees. Your witches pay well for it.",
        stages=[roll("Find the plants", "They hide among harmless weeds.", {"CLE": 1}, 3, 30, "Retry", 15)],
        reward=dict(res={"mana": 15}, xp=20)),
    "rob_a_traveller": dict(
        title="Rob a Traveller", level=1, biomes=["Plains"], deadline=120, weight=1,
        story="A lone traveller is crossing the open road with a heavy pack.",
        stages=[roll("Jump him", "Fast and frightening.", {"DEA": 0.5, "QUI": 0.5}, 3, 20, "EndQuest", dmg=2)],
        reward=dict(res={"gold": 25}, xp=20)),
    "prospect_the_tunnels": dict(
        title="Prospect the Tunnels", level=1, biomes=["Mountain"], deadline=0, weight=1,
        story="Abandoned side tunnels may still hold ore the miners missed.",
        stages=[roll("Search the tunnels", "Tap the walls, follow the glint.", {"CLE": 0.5, "STR": 0.5}, 3, 30, "Retry", 20)],
        reward=dict(res={"ore": 25}, xp=20)),
    "drill_in_the_courtyard": dict(
        title="Drill in the Courtyard", level=1, biomes=["Castle"], deadline=180, weight=0.8,
        story="The sergeant-at-arms wants to see who can stand a beating.",
        stages=[roll("Take the drill", "Shields up, hold the line.", {"STR": 0.5, "DUR": 0.5}, 3, 30, "EndQuest", dmg=2)],
        reward=dict(xp=40)),
    # --- Level 2 -------------------------------------------------------------------------------
    "steal_the_ring": dict(
        title="Steal the Ring", level=2, biomes=["Village"], deadline=240, weight=1,
        story="A merchant passing through town wears a ruby ring worth more than the whole market. He leaves when his business is done.",
        stages=[roll("Blend into the crowd", "Look like just another face at the market.", {"CLE": 0.7, "QUI": 0.3}, 4, 25, "Retry", 15),
                roll("Lift the ring", "One quick touch while he haggles.", {"QUI": 1}, 4, 20, "EndQuest", dmg=3)],
        reward=dict(res={"gold": 50}, xp=30)),
    "the_lost_wolf_pup": dict(
        title="The Lost Wolf Pup", level=2, biomes=["Forest"], deadline=300, weight=0.7,
        story="A wolf pup whimpers somewhere in the undergrowth, lost from its pack. Raised by you, it would grow into a loyal hunter.",
        stages=[roll("Find it", "Follow the whimpering.", {"CLE": 1}, 4, 30, "Retry", 15),
                roll("Calm it", "It bites. Be patient.", {"CLE": 0.6, "DUR": 0.4}, 4, 30, "Retry", 20, dmg=3)],
        reward=dict(unit="wolf", xp=15)),
    "deserter_at_the_gate": dict(
        title="Deserter at the Gate", level=2, biomes=["Castle"], deadline=180, weight=0.6,
        story="A night watchman who fled his post begs at your gate. He might serve you - or he might be a spy.",
        stages=[roll("Question him", "Find out who sent him.", {"CLE": 1}, 4, 25, "EndQuest"),
                roll("Test his loyalty", "Make him prove himself.", {"STR": 0.5, "DUR": 0.5}, 4, 25, "EndQuest", dmg=2)],
        reward=dict(unit="night_watchman")),
    "raid_the_dwarf_stores": dict(
        title="Raid the Dwarf Stores", level=2, biomes=["Mountain"], deadline=240, weight=0.8,
        story="The dwarfs keep their best ore in a storeroom deep in the mine.",
        stages=[roll("Sneak past the miners", "Their lamps light every corner.", {"QUI": 1}, 4, 25, "Retry", 20),
                roll("Haul the crates", "Heavy, and you can't drop them.", {"STR": 1}, 4, 30, "InjureContinue", dmg=3)],
        reward=dict(res={"ore": 40, "crystal": 15}, xp=25)),
    "extort_the_toll": dict(
        title="Extort the Toll", level=2, biomes=["Plains"], deadline=240, weight=0.8,
        story="Travellers on this road will pay to pass - once they understand who runs it now.",
        stages=[roll("Block the road", "Look as dangerous as you are.", {"DEA": 0.5, "STR": 0.5}, 4, 25, "EndQuest", dmg=2),
                roll("Count the coin", "Make sure nobody short-changes you.", {"CLE": 1}, 3, 20, "InjureContinue")],
        reward=dict(res={"gold": 40}, xp=25)),
    # --- Arena (levels 2-4): 1v1 or 2v2, one round per level above 1; damage carries between rounds.
    # arena=True lets level 2 have a battle (the entry roll + one round).
    "arena_first_blood": dict(
        title="Arena: First Blood", level=2, biomes=ARENA, deadline=240, weight=0.4, arena=True,
        story="The pit is open to anyone who signs the roll. One fight, one on one, and the crowd pays the winner.",
        stages=[ENTRY(3),
                battle("Round 1", "A militiaman with something to prove.", ["villager_militia"])],
        reward=dict(res={"gold": 30}, xp=40)),
    "arena_the_gauntlet": dict(
        title="Arena: The Gauntlet", level=3, biomes=ARENA, deadline=300, weight=0.35, arena=True,
        story="Two opponents, back to back, one on one. No healing between rounds.",
        stages=[ENTRY(4),
                battle("Round 1", "A night watchman, earning extra coin.", ["night_watchman"]),
                battle("Round 2", "An elf blade dancer, fast and smiling.", ["elf_blade_dancer"])],
        reward=dict(res={"gold": 35}, xp=45)),
    "arena_champions_duel": dict(
        title="Arena: Champion's Duel", level=4, biomes=ARENA, deadline=360, weight=0.3, arena=True,
        story="Three rounds, one on one, ending against the reigning champion. Win, and the whole town knows your name.",
        stages=[battle("Round 1", "A dwarf axeman far from his mine.", ["dwarf_axeman"]),
                battle("Round 2", "A night watchman who never loses twice.", ["night_watchman"]),
                battle("Round 3", "The champion: a sergeant who has never lost.", ["village_sergeant"])],
        reward=dict(res={"gold": 45}, xp=50)),
    "arena_pair_fight": dict(
        title="Arena: Pair Fight", level=2, biomes=ARENA, deadline=240, weight=0.4, arena=True, minagents=2, maxagents=2,
        story="Two on two in the pit. Bring a partner you trust.",
        stages=[ENTRY(3),
                battle("Round 1", "A militiaman and an archer.", ["villager_militia", "villager_archer"])],
        reward=dict(res={"gold": 30}, xp=40)),
    "arena_twin_blades": dict(
        title="Arena: Twin Blades", level=3, biomes=ARENA, deadline=300, weight=0.35, arena=True, minagents=2, maxagents=2,
        story="Two rounds of two on two. The second pair has fought together for years.",
        stages=[ENTRY(4),
                battle("Round 1", "Two militiamen.", ["villager_militia", "villager_militia"]),
                battle("Round 2", "Two dwarfs: an axe in front, a crossbow behind.", ["dwarf_axeman", "dwarf_crossbowman"])],
        reward=dict(res={"gold": 35}, xp=45)),
    "arena_grand_melee": dict(
        title="Arena: Grand Melee", level=4, biomes=ARENA, deadline=360, weight=0.3, arena=True, minagents=2, maxagents=2,
        story="The finest pairs of the realm, three rounds of two on two. The prize purse is enormous.",
        stages=[battle("Round 1", "A night watchman and an archer.", ["night_watchman", "villager_archer"]),
                battle("Round 2", "A blade dancer and an elf archer.", ["elf_blade_dancer", "elf_archer"]),
                battle("Round 3", "The champions: a sergeant and a dwarf axeman.", ["village_sergeant", "dwarf_axeman"])],
        reward=dict(res={"gold": 45}, xp=50)),
    # --- Escaped Prisoner (levels 1-3): one of the Dark Lord's prisoners broke out of the dungeon
    "escaped_prisoner_find": dict(
        title="Escaped Prisoner", level=1, biomes=["Plains", "Forest"], deadline=180, weight=0.5,
        story="A prisoner slipped out of your dungeon last night. He can't have gone far on foot. Find him before he does.",
        stages=[roll("Find him", "Muddy footprints, a torn sleeve on a thorn bush...", {"CLE": 0.6, "QUI": 0.4}, 3, 30, "Retry", 15)],
        reward=dict(res={"gold": 20}, xp=25)),
    "escaped_prisoner_kill": dict(
        title="Escaped Prisoner: Silence Him", level=2, biomes=["Plains", "Forest", "Mountain"], deadline=240, weight=0.4,
        story="An escaped prisoner knows the way into your dungeons and is running to sell it. He must not reach anyone who would listen.",
        stages=[roll("Track him down", "He is clever enough to hide his trail.", {"CLE": 0.6, "QUI": 0.4}, 4, 30, "Retry", 15),
                roll("Kill him", "He has a stolen knife and nothing to lose.", {"DEA": 0.7, "QUI": 0.3}, 4, 20, "EndQuest", dmg=3)],
        reward=dict(res={"gold": 35}, xp=35)),
    "escaped_prisoner_sheltered": dict(
        title="Escaped Prisoner: The Sheltered Man", level=3, biomes=["Village", "Plains"], deadline=300, weight=0.4, maxagents=2,
        story="The escaped prisoner found friends: villagers hide him and arm themselves for anyone who comes. Bring him back to the dungeon, as a lesson to everyone.",
        stages=[roll("Find the hiding place", "Someone here knows. Make them talk.", {"CLE": 0.5, "DEA": 0.5}, 5, 30, "Retry", 20, dmg=2),
                battle("Break the shelter", "His protectors stand in the doorway.", ["villager_militia", "villager_militia", "villager_archer"]),
                roll("Drag him back", "He fights every step of the way.", {"STR": 0.7, "DUR": 0.3}, 4, 30, "InjureContinue", dmg=3)],
        reward=dict(res={"gold": 40, "mana": 15}, xp=40)),
    # --- Torture Chamber (Chaos castle building, levels 1-3): interrogations in the castle dungeon.
    # Only the Torture Chamber hands these out (see its QuestPool); they never appear at random.
    "torture_loosen_a_tongue": dict(
        title="Loosen a Tongue", level=1, biomes=["Castle"], deadline=240, weight=1,
        story="A village scout hangs in chains in the Torture Chamber. He knows where the town hides its coin.",
        stages=[roll("Make him talk", "Patience, a hot iron, and more patience.", {"CLE": 0.6, "DEA": 0.4}, 3, 30, "Retry", 15)],
        reward=dict(res={"gold": 25}, xp=20)),
    "torture_the_silent_ranger": dict(
        title="The Silent Ranger", level=2, biomes=["Castle"], deadline=300, weight=1,
        story="An elf ranger caught in your woods has not said a word in three days. Her people's paths and wards are worth the effort.",
        stages=[roll("Break her will", "She stares back, unblinking.", {"DUR": 0.5, "CLE": 0.5}, 4, 30, "Retry", 20),
                roll("Read her maps", "Elven marks, half of them lies.", {"CLE": 1}, 4, 25, "EndQuest")],
        reward=dict(res={"crystal": 15, "mana": 15}, xp=25)),
    "torture_the_spy_in_chains": dict(
        title="The Spy in Chains", level=3, biomes=["Castle"], deadline=360, weight=1, maxagents=2,
        story="A spy from the town was caught inside your walls. His friends will not leave him here for long.",
        stages=[roll("Question the spy", "Who sent him, and who else is inside?", {"CLE": 1}, 5, 30, "Retry", 20),
                battle("The rescue", "His friends storm the dungeon stairs.", ["night_watchman", "villager_militia"]),
                roll("Make an example", "The whole castle should hear it.", {"DEA": 0.6, "STR": 0.4}, 4, 20, "InjureContinue", dmg=2)],
        reward=dict(res={"gold": 40, "mana": 20}, xp=35)),
    # --- Level 3 -------------------------------------------------------------------------------
    "hunt_the_wolf_pack": dict(
        title="Hunt the Wolf Pack", level=3, biomes=["Forest"], deadline=0, weight=1, maxagents=3,
        story="A wolf pack has been tearing at travellers in these woods. Their pelts would fetch a good price.",
        stages=[roll("Track them", "Follow the prints and the blood.", {"CLE": 0.5, "QUI": 0.5}, 4, 30, "Retry", 20),
                battle("Fight the pack", "The wolves turn to face you.", ["wolf", "wolf", "wolf"]),
                roll("Skin the pelts", "Heavy, messy work.", {"STR": 1}, 3, 20, "InjureContinue")],
        reward=dict(res={"gold": 30, "wood": 15}, xp=30)),
    "ambush_the_caravan": dict(
        title="Ambush the Merchant Caravan", level=3, biomes=["Plains"], deadline=300, weight=1, minagents=2, maxagents=3,
        story="A merchant caravan with a militia escort will pass here soon, heavy with coin and ore.",
        stages=[roll("Lie in wait", "Hide by the road and keep still.", {"QUI": 0.5, "CLE": 0.5}, 4, 30, "EndQuest"),
                battle("Ambush!", "The escort draws their weapons.", ["villager_militia", "villager_militia", "villager_archer"]),
                roll("Loot the wagons", "Take what you can carry.", {"STR": 1}, 3, 20, "InjureContinue")],
        reward=dict(res={"gold": 50, "ore": 25}, xp=25)),
    "burn_the_granary": dict(
        title="Burn the Granary", level=3, biomes=["Village"], deadline=300, weight=1,
        story="The town's granary feeds the soldiers that hunt your agents. Let it burn.",
        stages=[roll("Sneak in", "Past the night watch.", {"QUI": 1}, 5, 25, "Retry", 15),
                roll("Set the fire", "It has to catch before anyone sees.", {"CLE": 0.5, "DEA": 0.5}, 5, 20, "EndQuest", dmg=3),
                roll("Escape", "Smoke and shouting everywhere.", {"QUI": 1}, 4, 20, "InjureContinue", dmg=5)],
        reward=dict(xp=25, addperks=["raided"], perkseconds=240)),
    "the_buried_relic": dict(
        title="The Buried Relic", level=3, biomes=["Mountain"], deadline=0, weight=0.8, minattr={"CLE": 3},
        story="Old carvings speak of a relic buried deep below. The tunnels are unstable.",
        stages=[roll("Find the way", "Read the old carvings in the dark.", {"CLE": 1}, 5, 35, "Retry", 20),
                roll("Dig it out", "Break the rock around it.", {"STR": 0.7, "DUR": 0.3}, 4, 35, "Retry", 20, dmg=3),
                roll("Escape the collapse", "The tunnel comes down behind you.", {"DUR": 0.6, "QUI": 0.4}, 4, 15, "InjureContinue", dmg=6, death=0.1)],
        reward=dict(res={"crystal": 20, "mana": 15}, xp=30, addperks=["ore_vein"])),
    # --- Level 4 -------------------------------------------------------------------------------
    "assassinate_the_baron": dict(
        title="Assassinate the Baron", level=4, biomes=["Village"], deadline=420, weight=0.8, minattr={"DEA": 4},
        story="A baron is visiting the town to rally its guards against you. Cut the head off the snake before he rides on.",
        stages=[roll("Get close", "Find a way past his retinue.", {"CLE": 0.6, "QUI": 0.4}, 6, 30, "Retry", 20, dmg=2),
                roll("Strike", "There will be only one chance.", {"DEA": 0.7, "QUI": 0.3}, 6, 15, "EndQuest", dmg=6, death=0.25),
                roll("Escape", "The guards are coming.", {"QUI": 1}, 6, 25, "InjureContinue", dmg=6)],
        reward=dict(res={"gold": 60}, xp=30, removeneutrals=["village_sergeant"])),
    # --- Level 5 -------------------------------------------------------------------------------
    "slay_the_elder_treant": dict(
        title="Slay the Elder Treant", level=5, biomes=["Forest"], deadline=0, weight=0.6, minagents=2, maxagents=4,
        story="The forest itself fights you here. Its heart is an ancient treant, guarded by druids and elves. Kill it, and the woods are yours.",
        stages=[roll("Find the heart grove", "The paths shift when you are not looking.", {"CLE": 1}, 6, 40, "Retry", 25),
                roll("Break the wards", "Old magic in the roots.", {"CLE": 0.5, "DEA": 0.5}, 6, 30, "EndQuest", dmg=4),
                battle("The Elder wakes", "The grove rises against you.", ["treant", "druid", "elf_blade_dancer", "elf_archer"]),
                roll("Burn the heartwood", "Make sure it never grows back.", {"DEA": 0.5, "STR": 0.5}, 5, 25, "InjureContinue", dmg=4)],
        reward=dict(res={"gold": 50, "mana": 40}, xp=50, removeperks=["old_growth"])),
    # --- Thefts and killings in town -------------------------------------------------------------
    "steal_from_the_mage": dict(
        title="Steal from the Mage", level=3, biomes=["Village", "City"], deadline=360, weight=0.8, minattr={"CLE": 4},
        story="A travelling mage keeps his grimoire behind locks no thief can see. Only a sharp mind gets past them.",
        stages=[roll("Study his habits", "When does he sleep? What does he fear?", {"CLE": 1}, 5, 30, "Retry", 20),
                roll("Unpick the wards", "One wrong thought and the runes burn.", {"CLE": 1}, 6, 30, "EndQuest", dmg=4),
                roll("Take the grimoire", "Out before the wards wake again.", {"CLE": 0.5, "QUI": 0.5}, 4, 20, "InjureContinue", dmg=4)],
        reward=dict(res={"mana": 40, "crystal": 20}, xp=30)),
    "steal_the_blacksmiths_sword": dict(
        title="Steal the Blacksmith's Sword", level=2, biomes=["Village"], deadline=240, weight=0.9,
        story="The blacksmith has just finished a sword for the baron's captain. It would be a shame if the captain never got it.",
        stages=[roll("Distract the smith", "Haggle over horseshoes until he looks away.", {"CLE": 0.7, "STR": 0.3}, 4, 25, "Retry", 15),
                roll("Take the sword", "Still hot from the forge.", {"QUI": 0.7, "STR": 0.3}, 4, 20, "EndQuest", dmg=3)],
        reward=dict(res={"gold": 30, "ore": 30}, xp=25)),
    "assassinate_the_priest": dict(
        title="Assassinate the Priest", level=3, biomes=["Village", "City"], deadline=360, weight=0.8, minattr={"DEA": 3},
        story="The village priest preaches every day against the Dark Lord, and the people listen. Silence him during his own prayers.",
        stages=[roll("Enter the temple", "Kneel among the faithful. Look pious.", {"CLE": 1}, 5, 30, "Retry", 20),
                roll("Strike during the prayer", "Every eye is closed. Almost.", {"DEA": 0.8, "QUI": 0.2}, 5, 15, "EndQuest", dmg=4, death=0.15),
                roll("Slip out with the crowd", "Panic is the best cover.", {"QUI": 0.5, "CLE": 0.5}, 4, 20, "InjureContinue", dmg=4)],
        reward=dict(res={"gold": 30, "mana": 30}, xp=30)),
    # --- Caravans (Ambush the Caravan above is the merchant one) --------------------------------
    "rob_the_farmers_cart": dict(
        title="Rob the Farmers' Cart", level=2, biomes=["Plains"], deadline=180, weight=0.9,
        story="Farmers are taking their harvest and firewood to market. Their only guard is a dog.",
        stages=[roll("Wait by the road", "Patience. The cart is slow.", {"QUI": 1}, 3, 25, "Retry", 15),
                roll("Scare them off", "Farmers don't fight for turnips.", {"DEA": 0.5, "STR": 0.5}, 4, 20, "EndQuest", dmg=2)],
        reward=dict(res={"gold": 20, "wood": 25}, xp=20)),
    "ambush_the_elven_envoy": dict(
        title="Ambush the Elven Envoy", level=3, biomes=["Forest", "Plains"], deadline=300, weight=0.8, maxagents=3,
        story="Elven envoys are carrying gifts of crystal to the human king. Their guards are few, but deadly.",
        stages=[roll("Find their path", "Elves leave almost no tracks.", {"CLE": 1}, 5, 30, "Retry", 20),
                battle("Ambush!", "The blade dancer is already moving.", ["elf_blade_dancer", "elf_archer", "elf_archer"]),
                roll("Take the gifts", "Fragile crystal, carefully packed.", {"CLE": 0.5, "QUI": 0.5}, 4, 20, "InjureContinue")],
        reward=dict(res={"crystal": 25, "mana": 25}, xp=30)),
    "ambush_the_dwarf_ore_train": dict(
        title="Ambush the Dwarf Ore Train", level=4, biomes=["Mountain", "Plains"], deadline=360, weight=0.7, minagents=2, maxagents=3,
        story="A train of dwarf wagons heavy with ore is leaving the mountain. Dwarfs guard their ore like their own children.",
        stages=[roll("Block the pass", "Roll rocks onto the road.", {"STR": 1}, 6, 30, "EndQuest", dmg=3),
                battle("Ambush!", "Axes out, crossbows up.", ["dwarf_axeman", "dwarf_axeman", "dwarf_crossbowman"]),
                roll("Haul the ore", "Dwarf wagons are built heavy.", {"STR": 0.6, "DUR": 0.4}, 5, 30, "InjureContinue", dmg=3)],
        reward=dict(res={"ore": 60, "crystal": 20}, xp=35)),
    "ambush_the_royal_treasury": dict(
        title="Ambush the Royal Treasury", level=5, biomes=["Plains"], deadline=480, weight=0.5, minagents=2, maxagents=4,
        story="The king's tax collectors are bringing a year of gold to the capital, with soldiers on every side.",
        stages=[roll("Learn the route", "Bribe a clerk, read the orders.", {"CLE": 1}, 6, 35, "Retry", 25),
                roll("Lie in wait", "Hours in a ditch, without a sound.", {"QUI": 0.5, "DUR": 0.5}, 6, 30, "EndQuest"),
                battle("Ambush!", "The sergeant shouts the escort into line.",
                       ["village_sergeant", "night_watchman", "villager_militia", "villager_archer", "villager_archer"]),
                roll("Escape with the chests", "Riders will come from the capital.", {"QUI": 0.5, "STR": 0.5}, 6, 25, "InjureContinue", dmg=5)],
        reward=dict(res={"gold": 100}, xp=50)),
    # --- One attribute only: every stage checks the same attribute --------------------------------
    "haul_the_timber": dict(
        title="Haul the Timber", level=1, biomes=["Forest"], deadline=0, weight=0.8,
        story="Woodcutters left a pile of felled trees behind. They are yours if you can drag them.",
        stages=[roll("Drag the logs", "Pure muscle.", {"STR": 1}, 3, 30, "Retry", 15)],
        reward=dict(res={"wood": 30}, xp=20)),
    "shift_the_boulder": dict(
        title="Shift the Boulder", level=2, biomes=["Mountain"], deadline=0, weight=0.8,
        story="A rockfall sealed an old tunnel. Behind it, the miners say, is a vein nobody worked.",
        stages=[roll("Lever it loose", "Put your back into it.", {"STR": 1}, 4, 30, "Retry", 20),
                roll("Roll it clear", "Don't let it roll back on you.", {"STR": 1}, 4, 25, "InjureContinue", dmg=3)],
        reward=dict(res={"ore": 30, "crystal": 10}, xp=25)),
    "decipher_the_old_map": dict(
        title="Decipher the Old Map", level=1, biomes=["Castle"], deadline=0, weight=0.8,
        story="A crumbling map was found in the castle vaults. The ink has faded, but someone clever could read it.",
        stages=[roll("Read the faded ink", "Old words, older places.", {"CLE": 1}, 3, 30, "Retry", 15)],
        reward=dict(res={"gold": 15, "crystal": 10}, xp=25)),
    "forge_the_royal_seal": dict(
        title="Forge the Royal Seal", level=2, biomes=["Village", "City"], deadline=300, weight=0.8,
        story="With a copy of the king's seal, your orders would open any gate. You only need one real letter to copy.",
        stages=[roll("Study a real seal", "Every curl of the crown matters.", {"CLE": 1}, 4, 30, "Retry", 15),
                roll("Carve the copy", "A forgery is hanged if it is caught.", {"CLE": 1}, 4, 30, "EndQuest")],
        reward=dict(res={"gold": 45}, xp=25)),
    "cull_the_wild_boar": dict(
        title="Cull the Wild Boar", level=1, biomes=["Forest"], deadline=180, weight=0.8,
        story="A huge boar tears up the woods. One clean kill, and its hide and meat are yours.",
        stages=[roll("Kill it clean", "Miss, and it gores you.", {"DEA": 1}, 3, 20, "EndQuest", dmg=3)],
        reward=dict(res={"gold": 15, "wood": 10}, xp=25)),
    "silence_the_witness": dict(
        title="Silence the Witness", level=2, biomes=["Village"], deadline=240, weight=0.8,
        story="A stable boy saw one of your agents where he shouldn't have been. He is telling everyone.",
        stages=[roll("Corner him", "Somewhere nobody will hear.", {"DEA": 1}, 4, 25, "Retry", 15),
                roll("Make it quiet", "Quick and final.", {"DEA": 1}, 4, 15, "EndQuest", dmg=2)],
        reward=dict(res={"gold": 40}, xp=25)),
    "endure_the_initiation": dict(
        title="Endure the Initiation", level=1, biomes=["Castle"], deadline=180, weight=0.7,
        story="The old guard of the castle will only respect an agent who takes their beating without a sound.",
        stages=[roll("Take the punishment", "Don't fall. Don't cry out.", {"DUR": 1}, 3, 25, "EndQuest", dmg=3)],
        reward=dict(xp=45)),
    "survive_the_deep_shafts": dict(
        title="Survive the Deep Shafts", level=2, biomes=["Mountain"], deadline=0, weight=0.8,
        story="Crystals grow at the bottom of the deepest shafts, where the air itself makes miners faint.",
        stages=[roll("Climb down", "Hours on a rope in the dark.", {"DUR": 1}, 4, 30, "Retry", 20, dmg=2),
                roll("Breathe the bad air", "Stay long enough to dig them out.", {"DUR": 1}, 4, 30, "InjureContinue", dmg=4)],
        reward=dict(res={"crystal": 25, "ore": 20}, xp=25)),
    "catch_the_messenger": dict(
        title="Catch the Messenger", level=1, biomes=["Plains"], deadline=120, weight=0.8,
        story="A messenger is running across the fields with a letter for the baron. Catch him before he reaches the road.",
        stages=[roll("Run him down", "He is fast. Be faster.", {"QUI": 1}, 3, 20, "EndQuest")],
        reward=dict(res={"gold": 20}, xp=20)),
    "steal_the_lords_horse": dict(
        title="Steal the Lord's Horse", level=2, biomes=["Village", "Castle"], deadline=240, weight=0.8,
        story="The local lord's warhorse is worth a small fortune - if you can get it out of the stable and away.",
        stages=[roll("Slip into the stable", "Between the grooms' rounds.", {"QUI": 1}, 4, 25, "Retry", 15),
                roll("Ride hard", "His riders know these roads.", {"QUI": 1}, 4, 25, "InjureContinue", dmg=3)],
        reward=dict(res={"gold": 45}, xp=25)),
}


def check(qid, q):
    level = q["level"]
    assert len(q["stages"]) == STAGES_PER_LEVEL[level], f"{qid}: level {level} needs {STAGES_PER_LEVEL[level]} stages"
    assert level >= 3 or q.get("arena") or all(st["kind"] == 0 for st in q["stages"]), f"{qid}: battles only from level 3"
    assert q["biomes"] and all(b in BIOMES for b in q["biomes"]), f"{qid}: unknown or missing biome"


def write(root, qid, q):
    ext = [("res://game/quests/QuestDefinition.cs", "def"), ("res://game/quests/QuestStage.cs", "stage"),
           ("res://game/quests/QuestReward.cs", "reward"), ("res://game/castle/ResourceCost.cs", "cost"),
           ("res://game/units/SpecialAttributes.cs", "attr")]
    scripts = len(ext)

    def ext_ref(path):
        for p, i in ext:
            if p == path:
                return f'ExtResource("{i}")'
        ext.append((path, f"r{len(ext)}"))
        return f'ExtResource("{ext[-1][1]}")'

    subs = []

    def sub(lines):
        subs.append((f"S{len(subs)}", lines))
        return f'SubResource("{subs[-1][0]}")'

    def array(items):
        return "[" + ", ".join(items) + "]"

    stages = []
    for st in q["stages"]:
        lines = ['script = ExtResource("stage")', f'Name = "{st["name"]}"', f'Description = "{st["desc"]}"',
                 f'Kind = {st["kind"]}', f'Duration = {st["dur"]}']
        lines += [f"Weight{ATTR[i]} = {float(st['weights'].get(SHORT[i], 0))}" for i in range(5)]
        lines += [f'Difficulty = {float(st["diff"])}',
                  f'Enemies = {array(ext_ref(NEUTRALS + e + ".tres") for e in st["enemies"])}',
                  f'OnFail = {FAILS.index(st["fail"])}', f'RetryDelay = {st["retry"]}',
                  f'FailDamage = {st["dmg"]}', f'DeathChance = {float(st["death"])}']
        stages.append(sub(lines))

    r = q["reward"]
    costs = [sub(['script = ExtResource("cost")', f"Type = {RES[k]}", f"Amount = {float(v)}"]) for k, v in r.get("res", {}).items()]
    lines = ['script = ExtResource("reward")', f"Resources = {array(costs)}", f'Experience = {r.get("xp", 0)}']
    if "unit" in r:
        lines.append(f'JoiningUnit = {ext_ref(NEUTRALS + r["unit"] + ".tres")}')
    lines += [f'AddPerks = {array(ext_ref(PERKS + p + ".tres") for p in r.get("addperks", []))}',
              f'AddPerksSeconds = {r.get("perkseconds", 0)}',
              f'RemovePerks = {array(ext_ref(PERKS + p + ".tres") for p in r.get("removeperks", []))}',
              f'RemoveNeutrals = {array(ext_ref(NEUTRALS + n + ".tres") for n in r.get("removeneutrals", []))}']
    reward = sub(lines)

    main = ['script = ExtResource("def")', f'Title = "{q["title"]}"', f'Story = "{q["story"]}"',
            f'Level = {q["level"]}', f'Biomes = {array(str(BIOMES.index(b)) for b in q["biomes"])}',
            f'Deadline = {q["deadline"]}', f'SpawnWeight = {float(q["weight"])}', f'MinLevel = {q.get("minlevel", 1)}']
    if "minattr" in q:
        main.append("MinAttributes = " + sub(['script = ExtResource("attr")'] + [f"{ATTR[SHORT.index(k)]} = {v}" for k, v in q["minattr"].items()]))
    main += [f'MinAgents = {q.get("minagents", 1)}', f'MaxAgents = {q.get("maxagents", 1)}',
             f"Stages = {array(stages)}", f"Reward = {reward}"]

    out = [f'[gd_resource type="Resource" script_class="QuestDefinition" load_steps={len(ext) + len(subs) + 1} format=3]', ""]
    out += [f'[ext_resource type="{"Script" if n < scripts else "Resource"}" path="{p}" id="{i}"]' for n, (p, i) in enumerate(ext)]
    out.append("")
    for sid, lines in subs:
        out += [f'[sub_resource type="Resource" id="{sid}"]'] + lines + [""]
    out += ["[resource]"] + main + [""]
    with open(os.path.join(root, "resources", "quests", qid + ".tres"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out))


if __name__ == "__main__":
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    folder = os.path.join(root, "resources", "quests")
    os.makedirs(folder, exist_ok=True)
    for qid, q in QUESTS.items():
        check(qid, q)
    # Quests dropped from the table go too
    for name in os.listdir(folder):
        if name.endswith(".tres") and name[:-5] not in QUESTS:
            os.remove(os.path.join(folder, name))
    for qid, q in QUESTS.items():
        write(root, qid, q)
    print(f"wrote {len(QUESTS)} quests")
