# Skill icon prompts (ChatGPT)

One painted icon per skill. The game crops each icon to a circle and draws its own rim around
it (gold when learned, dim grey when locked), so the images need **no frame, border or circle**
of their own: just a square picture with the subject in the middle.

## How to use

1. Copy the **style block** below, then add the **subject line** of one skill from the tables.
   One icon per message works best.
2. For an upgrade (Bleed 2, Fire Bolt 2, Raider 2, ...), attach the icon you made for the first
   skill and add: *"Same icon as the attached one, but stronger: <subject line>"*. The pair then
   looks related.
3. Save each image with the file name from the table and send them to me in chat (a few at a
   time is fine). I'll crop, size and wire them in. (Pipeline: the art goes to
   `tools/sprites/source/skill_icons/<file>.png`, `python tools/sprites/make_skill_icons.py <file>`
   writes the round 256px icon to `images/skills/`, and the skill's `Icon` field points at it.
   Skills without an icon show their initials in the tree.)
4. Skills with the same name on different units (Wood Cutter, Mana Drain, Raider/Raider 1) can
   share one icon, marked *shared* below.

## Style block (start every prompt with this)

```
Square game skill icon, 1024x1024, for a dark-fantasy strategy game where you play the Dark Lord.
Painted digital illustration, semi-realistic like Darkest Dungeon or Diablo skill icons:
bold readable shapes, strong silhouette, dramatic rim lighting, rich but dark colours.
One clear subject centred in the middle, filling the central circle of the image, readable even
when shown very small (64px). Dark moody background that fills the whole square.
No text, no letters, no numbers, no frame, no border, no circle outline, no UI elements.
Subject:
```

## Shared subclasses

Any unit type can have these classes, so their icons show **no particular creature**: objects,
hands, weapons, silhouettes or effects only. No imp, orc, witch or skeleton features (horns,
tusks, green skin, bones), so they fit every unit that learns them.

### Assassin (accent colour: crimson)

| File | Skill | Subject line |
|---|---|---|
| assassin_slippery.png | Slippery | A shadowy hooded silhouette slipping sideways out of a grasping gauntlet, motion trails, crimson accents |
| assassin_silent_killer.png | Silent Killer | A hooded figure in deep shadow, one gloved finger raised to hidden lips, a thin bloody dagger glinting below |
| assassin_bleed_1.png | Bleed 1 | Three jagged slash marks cut across the image, fresh red blood dripping from them, a curved dagger beside them |
| assassin_bleed_1.png | Bleed 2 | *reuses the Bleed 1 icon, no new image needed* |
| assassin_scout.png | Scout | A cloaked lookout crouched on a high crooked tower at dusk, peering over a misty land |
| assassin_long_reach.png | Long Reach | A long hooked blade on a chain reaching far toward the viewer, crimson glint on its edge |
| assassin_bully.png | Bully | A heavy boot stomping down on a fallen, broken shield, crimson light |
| assassin_tormentor.png | Tormentor | *(upgrade of Bully)* A dark silhouette looming over a cowering wounded soldier, a barbed whip in hand |

### Sorcerer (accent colour: hellfire orange and violet)

| File | Skill | Subject line |
|---|---|---|
| sorcerer_devious_cunning.png | Devious Cunning | A shadowed head in profile with a glowing violet rune-maze inside it |
| sorcerer_fire_bolt_1.png | Fire Bolt 1 | An open hand flicking a small bolt of orange fire forward |
| sorcerer_fire_bolt_1.png | Fire Bolt 2 | *reuses the Fire Bolt 1 icon, no new image needed* |
| sorcerer_fire_bolt_1.png | Fireball | *reuses the Fire Bolt 1 icon, no new image needed* |
| sorcerer_wicked_mind.png | Wicked Mind | A cracked grinning mask with violet smoke pouring from its eye holes |
| sorcerer_mana_drain.png | Mana Drain *(shared with witch)* | Blue glowing mana streams being pulled up out of cracked earth into an open hand |
| sorcerer_mana_steal.png | Mana Steal | A blue glowing wisp being sucked out of a screaming figure into a dark outstretched hand |

### Warrior (shared by orc and skeleton, accent colour: steel and blood red)

| File | Skill | Subject line |
|---|---|---|
| warrior_lumberjack_1.png | Lumberjack 1 *(the orc's Wood Cutter can share it)* | A heavy axe buried in a tree stump, wood chips flying |
| warrior_swift_stride.png | Swift Stride | Armoured boots mid-sprint over a battlefield, dust kicked up, motion blur |
| warrior_heavy_swing.png | Heavy Swing | A massive two-handed axe in a full overhead swing, muscles straining, a trail of motion |
| warrior_charge.png | Charge | A dark armoured silhouette rushing forward through a gap in a shield line, spear lowered |
| warrior_lumberjack_1.png | Lumberjack 2 | *reuses the Lumberjack 1 icon, no new image needed* |
| warrior_crush_defence.png | Crush Defence | A war hammer smashing a steel breastplate, the armour cracking and plates flying apart |
| warrior_champion.png | Champion | A lone warrior raising a notched blade in a sand arena, a ring of shadowy cheering crowd behind |

## Imp

Imp look: small lean demon, blue-grey skin, curved black-striped horns, a green flame on its
head, glowing yellow-red eyes, toothy grin. Only the imp's own main class uses it.

### Demonic Imp (main class, accent colour: ember gold)

| File | Skill | Subject line |
|---|---|---|
| imp_tunnel_rat.png | Tunnel Rat | An imp squeezing out of a cracked, rubble-filled tunnel, covered in dirt, still grinning |
| imp_mining.png | Mining 1 | Small clawed hands holding a tiny pickaxe striking a glowing ore vein, sparks flying |
| imp_raider_1.png | Raider 1 *(shared with orc Raider)* | A burning thatched roof with a sack of stolen gold coins in the foreground |
| imp_raider_2.png | Raider 2 | *(upgrade of Raider 1)* An overflowing chest of gold coins in a burning town, flames everywhere |
| imp_greed.png | Greed | An imp hugging a pile of gold coins and ore nuggets, eyes glowing gold with greed |
| imp_lord.png | Lord | A crowned imp on a small throne of bones and coins, smaller imps bowing before it |

## Orc

Orc look: big olive-green skin, black beard, tusks, spiked shoulder armour, heavy axe.
The orc's other classes are Warrior (above) and Tank (below).

### Bloodtusk (main class, accent colour: blood red and bone)

| File | Skill | Subject line |
|---|---|---|
| orc_brawler.png | Brawler | A huge green orc fist wrapped in leather, knuckles bloodied |
| orc_miner.png | Miner | A crude orcish pickaxe striking a rock wall with glowing ore, sparks |
| orc_berserker.png | Berserker | A roaring orc face with red-glowing eyes and veins bulging, blood on its tusks |
| imp_raider_1.png | Raider | *shared with the imp's Raider 1, no new image needed* |
| orc_war_cry.png | War Cry | An orc roaring with a visible shockwave of sound blasting outward |
| orc_forced_march.png | Forced March | Heavy fur-wrapped orc boots stomping on a muddy road, dust and torches behind |
| orc_bone_crusher.png | Bone Crusher | A war axe crashing through a dented helmet, bone splinters flying |
| orc_scouter.png | Scouter | An orc on a hilltop shading its eyes, looking over a misty valley |
| orc_warchief.png | Warchief | An orc chieftain with a horned bone crown raising an axe, war banners behind |

### Tank (accent colour: iron grey; no particular creature, other units may get it later)

| File | Skill | Subject line |
|---|---|---|
| tank_thick_hide.png | Thick Hide | Close-up of rough, scarred leathery hide with an arrowhead stuck harmlessly in it |
| tank_iron_skin.png | Iron Skin | A forearm turning to dark iron plates, a sword blade snapping against it |
| tank_stone_wall.png | Stone Wall | A huge shield planted in the mud like a wall, arrows bouncing off it |
| tank_provoke.png | Provoke | An armoured silhouette banging a weapon on its shield, red challenge glow, enemies turning toward it |
| tank_unbreakable.png | Unbreakable | A battered shield still held high, many broken arrows and blades stuck in it |
| tank_juggernaut.png | Juggernaut | A figure in massive spiked plate armour marching forward like a living fortress |

## Witch

Witch look: hooded woman in tattered black and green robes, long dark hair with grey streaks,
green eyes, gnarled antler staff with a green crystal.

| File | Skill | Subject line |
|---|---|---|
| witch_heal.png | Heal | Pale hands holding a bundle of glowing green herbs over a wound, soft green light |
| witch_curse.png | Curse | A crooked pointing finger with a dark purple hex symbol glowing in the air |
| witch_herbalist.png | Herbalist | A bubbling cauldron with herbs and mushrooms, green steam, by a campfire |
| sorcerer_mana_drain.png | Mana Drain | *shared with the Sorcerer's Mana Drain, no new image needed* |
| witch_forbidden_lore.png | Forbidden Lore | An old chained grimoire falling open, eerie green light and whispers spilling out |
| witch_dark_arts.png | Dark Arts | A hand wreathed in black-green magic shaped like snapping jaws |
| witch_evil_eye.png | Evil Eye | A single glowing green eye staring out from a dark forest |
| witch_heal_2.png | Heal 2 | *(upgrade of Heal)* A blooming glowing flower closing a wound, brighter green light |
| witch_curse_2.png | Curse 2 | *(upgrade of Curse)* A twisted voodoo doll pierced with thorns, dark purple aura |
| witch_mending_circle.png | Mending Circle | A ring of glowing green runes on the ground with healing light rising from it |
| witch_plague.png | Plague | A cloud of sickly green-purple miasma rolling over the land, a crow-shaped mask in it |

## Skeleton

Skeleton look: icy blue undead bones, cracked frosted helmet, glowing cyan eyes, ice-tipped
spear and a round wooden shield with a skull.

### Frost Revenant (main class, accent colour: icy cyan)

| File | Skill | Subject line |
|---|---|---|
| skeleton_grave_strength.png | Grave Strength | A bony skeletal fist bursting up out of a grave, frost on the earth |
| skeleton_hardened_bones.png | Hardened Bones | A frost-covered skeletal arm bone with ice crystals, an axe chipping harmlessly off it |
| skeleton_cold_fury.png | Cold Fury | A skull with blazing cyan eyes, icy breath streaming out |
| skeleton_shield_block.png | Shield Block | A round skull shield taking a sword strike, sparks and ice shards flying |
| skeleton_frost_spear.png | Frost Spear | An ice-crusted spear tip thrusting forward, freezing the air into shards |
| skeleton_defender.png | Defender | A row of skeleton spears and skull shields locked into a wall, front-line formation |
| skeleton_deathless.png | Deathless | Scattered bones pulling themselves back together into a skeleton, cyan glow |
| skeleton_frost_armor.png | Frost Armor | A skeleton encased in jagged ice plates, an enemy sword freezing solid where it struck |
| skeleton_blizzard.png | Blizzard | A skeleton raising its spear into a howling white-blue storm, ice shards raining down |
