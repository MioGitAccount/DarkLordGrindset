using Godot;
using System;
[GlobalClass]
public partial class UnitTemplate : Resource
{

	[Export] public string Name = "Imp";
	[Export] public Texture2D Icon;
	// Animated map sprite with "idle" and "walk" animations; units without one show Icon instead
	[Export] public SpriteFrames MapSprite;

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
