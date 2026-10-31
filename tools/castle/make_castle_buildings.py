# Writes resources/buildings/castle_buildings.tres from the tables below.
# Run from the project root: python tools/castle/make_castle_buildings.py
# Editing the .tres by hand in Godot is fine too; re-running this overwrites it.
# Design: docs/CASTLE_FACTIONS_DESIGN.md
import os

RES = {"gold": 0, "wood": 1, "ore": 2, "crystal": 3, "mana": 4}
# game/castle/Faction.cs, in order
FACTIONS = ["None", "Chaos", "Stronghold", "DarkHumans", "Undead"]
UNITS = {"imp": "res://unit_templates/imp.tres", "orc": "res://unit_templates/orc.tres",
         "witch": "res://unit_templates/witch.tres", "skeleton": "res://unit_templates/skeleton.tres"}
QUESTS = "res://resources/quests/"

TOWERS = [
    dict(id="tower1", name="Tower I", level=1, tower=1, start=True, vision=50, gold=0.25, agents=8,
         desc="The dark lord's keep. Watches over the castle, brings in a little gold and lets the faction bases reach level 1."),
    dict(id="tower2", name="Tower II", level=2, tower=2, vision=150, agents=4, requires=["tower1"],
         cost={"wood": 120, "ore": 60, "gold": 40},
         desc="A taller tower of black stone. Sees further and lets the faction bases reach level 2."),
    dict(id="tower3", name="Tower III", level=3, tower=3, vision=250, agents=4, requires=["tower2"],
         cost={"wood": 200, "ore": 120, "crystal": 60},
         desc="The spire of the dark lord, seen from every corner of the land. Lets the faction bases reach level 3."),
]

# Faction bases: level 1-3, one slot per level; unit cap = cap + per_level for each level above 1
BASES = [
    dict(id="imp_den", name="Imp Den", faction="Chaos", unit="imp", cap=4, per_level=2,
         cost={"wood": 40, "gold": 20}, recruit={"gold": 30},
         up2={"wood": 80, "gold": 60}, up3={"gold": 120, "crystal": 40, "mana": 40},
         desc="A smoky burrow where imps breed, the heart of the Chaos layer. Lets you recruit imps."),
    dict(id="orc_pit", name="Orc Pit", faction="Stronghold", unit="orc", cap=4, per_level=2,
         cost={"wood": 60, "ore": 30}, recruit={"gold": 50, "ore": 10},
         up2={"wood": 100, "ore": 60}, up3={"gold": 120, "ore": 100, "crystal": 30},
         desc="A mud pit full of brawling orcs and goblins, the heart of the Stronghold layer. Lets you recruit orcs."),
    dict(id="witch_hut", name="Witch's Hut", faction="DarkHumans", unit="witch", cap=2, per_level=1,
         cost={"wood": 50, "gold": 30}, recruit={"gold": 60, "wood": 20},
         up2={"wood": 80, "gold": 60, "mana": 20}, up3={"gold": 120, "crystal": 40, "mana": 60},
         desc="A crooked hut at the edge of the woods, the heart of the Dark Humans layer. Lets you recruit witches."),
    dict(id="crypt", name="Crypt", faction="Undead", unit="skeleton", cap=4, per_level=2,
         cost={"ore": 40, "gold": 30}, recruit={"gold": 40, "ore": 10},
         up2={"ore": 80, "gold": 60}, up3={"gold": 120, "ore": 80, "crystal": 40},
         desc="Cold stairs down to the restless dead, the heart of the Undead layer. Lets you raise skeletons."),
]

# Slot buildings: faction None = neutral (fits any layer). level = tower level needed, base = base level needed.
SLOTS = [
    # Chaos
    dict(id="torture_chamber", name="Torture Chamber", faction="Chaos", level=1, cost={"wood": 60, "gold": 40},
         quests=["torture_loosen_a_tongue", "torture_the_silent_ranger", "torture_the_spy_in_chains"],
         desc="Chains, irons and a prisoner who knows something. Hands out interrogation quests."),
    dict(id="imp_academy", name="Imp Academy", faction="Chaos", level=2, base=2, cost={"wood": 80, "gold": 60, "crystal": 20},
         bonus_for="imp", bonus=2, cap_for="imp", cap=1,
         desc="Old imps teach young ones dirty tricks. Imps are recruited 2 levels higher."),
    dict(id="infernal_library", name="Infernal Library", faction="Chaos", level=3, base=3, cost={"gold": 100, "crystal": 60, "mana": 80},
         bonus_for="imp", bonus=2, cap_for="imp", cap=2,
         desc="Forbidden books, read by candlelight. Imps are recruited 2 more levels higher."),
    # Stronghold
    dict(id="arena", name="Arena", faction="Stronghold", level=1, cost={"wood": 80, "ore": 40},
         quests=["arena_first_blood", "arena_pair_fight", "arena_the_gauntlet", "arena_twin_blades",
                 "arena_champions_duel", "arena_grand_melee"],
         desc="A sand pit ringed with jeering orcs. Hands out arena fights."),
    dict(id="orc_war_camp", name="Orc War Camp", faction="Stronghold", level=2, base=2, cost={"wood": 100, "ore": 80, "gold": 40},
         bonus_for="orc", bonus=2, cap_for="orc", cap=1,
         desc="Drills, fights and more fights. Orcs are recruited 2 levels higher."),
    dict(id="warlords_hall", name="Warlord's Hall", faction="Stronghold", level=3, base=3, cost={"gold": 150, "ore": 100, "crystal": 50},
         bonus_for="orc", bonus=2, cap_for="orc", cap=2,
         desc="Only the toughest orcs leave it alive. Orcs are recruited 2 more levels higher."),
    # Dark Humans
    dict(id="prison", name="Prison", faction="DarkHumans", level=1, cost={"ore": 60, "wood": 40},
         quests=["escaped_prisoner_find", "escaped_prisoner_kill", "escaped_prisoner_sheltered"],
         desc="Damp cells under the castle. Prisoners keep trying to get out, and someone has to bring them back."),
    dict(id="healing_hut", name="Healing Hut", faction="DarkHumans", level=1, cost={"wood": 50, "mana": 20}, heal=8,
         desc="Herbs, leeches and a witch who doesn't ask questions. Units on the castle field heal over time."),
    # Undead
    dict(id="graveyard", name="Graveyard", faction="Undead", level=1, cost={"ore": 60, "gold": 60, "mana": 30},
         bonus_for="skeleton", bonus=2, cap_for="skeleton", cap=1,
         desc="Old bones, freshly dug. Skeletons are raised 2 levels higher."),
    # Neutral
    dict(id="bank", name="Bank", faction="None", level=1, cost={"gold": 120, "wood": 40}, gold=0.5,
         desc="A vault of stolen coin, lent out at cruel rates. Brings in gold every tick."),
    dict(id="dark_throne", name="Dark Throne", faction="None", level=3, cost={"gold": 200, "crystal": 80, "mana": 100},
         bonus=1, agents=2,
         desc="Every recruit kneels before the dark lord first. All units are recruited 1 level higher."),
]


def main():
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    ext = [("Script", "res://game/castle/BuildingTree.cs", "tree"), ("Script", "res://game/castle/BuildingDefinition.cs", "building"),
           ("Script", "res://game/castle/ResourceCost.cs", "cost")]

    def ext_ref(kind, path):
        for _, p, i in ext:
            if p == path:
                return f'ExtResource("{i}")'
        ext.append((kind, path, f"r{len(ext)}"))
        return f'ExtResource("{ext[-1][2]}")'

    subs = []

    def costs(table):
        refs = []
        for k, v in (table or {}).items():
            subs.append((f"Cost_{len(subs)}", ['script = ExtResource("cost")', f"Type = {RES[k]}", f"Amount = {float(v)}"]))
            refs.append(f'SubResource("{subs[-1][0]}")')
        return "[" + ", ".join(refs) + "]"

    def unit(name):
        return ext_ref("Resource", UNITS[name])

    ids = []

    def building(b, lines):
        lines = ['script = ExtResource("building")', f'Name = "{b["name"]}"', f'Description = "{b["desc"]}"'] + lines
        sid = "Building_" + b["id"]
        subs.append((sid, lines))
        ids.append(sid)

    for t in TOWERS:
        building(t, [f'Level = {t["level"]}', f'Cost = {costs(t.get("cost"))}',
                     "Requires = [" + ", ".join(f'SubResource("Building_{r}")' for r in t.get("requires", [])) + "]",
                     f'BuiltAtStart = {"true" if t.get("start") else "false"}', f'TowerLevel = {t["tower"]}',
                     f'BonusVision = {float(t["vision"])}', f'GoldPerTick = {float(t.get("gold", 0))}', f'AgentCap = {t["agents"]}'])
    for b in BASES:
        building(b, ["Level = 1", f'Cost = {costs(b["cost"])}', "Requires = []", "BuiltAtStart = true",
                     f'Faction = {FACTIONS.index(b["faction"])}', "IsFactionBase = true",
                     f'UpgradeCostLevel2 = {costs(b["up2"])}', f'UpgradeCostLevel3 = {costs(b["up3"])}',
                     f'UnitCapPerLevel = {b["per_level"]}', f'RecruitsUnit = {unit(b["unit"])}',
                     f'RecruitCost = {costs(b["recruit"])}', f'UnitCapFor = {unit(b["unit"])}', f'UnitCap = {b["cap"]}'])
    for s in SLOTS:
        lines = [f'Level = {s["level"]}', f'Cost = {costs(s["cost"])}', "Requires = []",
                 f'Faction = {FACTIONS.index(s["faction"])}', f'MinBaseLevel = {s.get("base", 1)}']
        if s.get("gold"):
            lines.append(f'GoldPerTick = {float(s["gold"])}')
        if s.get("bonus"):
            if s.get("bonus_for"):
                lines.append(f'BonusLevelFor = {unit(s["bonus_for"])}')
            lines.append(f'BonusRecruitLevel = {s["bonus"]}')
        if s.get("agents"):
            lines.append(f'AgentCap = {s["agents"]}')
        if s.get("cap_for"):
            lines += [f'UnitCapFor = {unit(s["cap_for"])}', f'UnitCap = {s["cap"]}']
        if s.get("heal"):
            lines.append(f'HealInterval = {s["heal"]}')
        if s.get("quests"):
            for q in s["quests"]:
                assert os.path.exists(os.path.join(root, "resources", "quests", q + ".tres")), f"{s['id']}: no quest {q}"
            lines.append("QuestPool = [" + ", ".join(ext_ref("Resource", QUESTS + q + ".tres") for q in s["quests"]) + "]")
        building(s, lines)

    out = [f'[gd_resource type="Resource" script_class="BuildingTree" load_steps={len(ext) + len(subs) + 1} format=3]', ""]
    out += [f'[ext_resource type="{kind}" path="{p}" id="{i}"]' for kind, p, i in ext]
    out.append("")
    for sid, lines in subs:
        out += [f'[sub_resource type="Resource" id="{sid}"]'] + lines + [""]
    out += ["[resource]", 'script = ExtResource("tree")', 'Name = "Castle"', "BaseVision = 250.0",
            "Buildings = [" + ", ".join(f'SubResource("{i}")' for i in ids) + "]", ""]
    path = os.path.join(root, "resources", "buildings", "castle_buildings.tres")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out))
    print(f"wrote {len(ids)} buildings")


if __name__ == "__main__":
    main()
