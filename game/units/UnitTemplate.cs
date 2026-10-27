using Godot;
using System;
[GlobalClass]
public partial class UnitTemplate : Resource
{

	[Export] public string Name = "Imp";
	// How big a threat a neutral of this type is, 1-5; shown to the player, does not change stats
	[Export(PropertyHint.Range, "1,5")] public int Level = 1;
	// Where neutrals of this type live (see Biomes.Of); the map editor suggests them for those fields
	[Export] public Biome Biome = Biome.None;
	[Export] public Texture2D Icon;
	// Animated map sprite with "idle" and "walk" animations; units without one show Icon instead
	[Export] public SpriteFrames MapSprite;
	// Painted cutout for the battle screen, animated in code; units without one use MapSprite there
	[Export] public Texture2D BattleSprite;
	// The same cutout in its attack pose, at the same scale (tools/sprites "attack_source"); shown
	// at the moment of the strike. Optional
	[Export] public Texture2D BattleAttackSprite;
	// Tilt the battle sprite when it attacks; off where bending looks wrong (four-legged creatures)
	[Export] public bool BattleAttackLean = true;
	// Colour of the bolt a ranged unit shoots with its normal attack
	[Export] public Color AttackProjectileColor = new Color(1f, 0.7f, 0.3f);

	[Export] public AudioStream SelectSound;
	[Export] public BattleStats DefaultStats;
	// Deadliness/Strength/Durability/Cleverness/Quickness; added on top of DefaultStats
	[Export] public SpecialAttributes Attributes;
	// Shared by every unit of this type; each unit chooses which skills to unlock
	[Export] public SkillTree SkillTree;
	// Battle traits and actions every unit of this type has; skills can add more
	[Export] public Godot.Collections.Array<CombatPassive> CombatPassives = new();
	[Export] public Godot.Collections.Array<CombatAbility> CombatAbilities = new();
}
