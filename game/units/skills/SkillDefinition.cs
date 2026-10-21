using Godot;
using System.Collections.Generic;
using System.Linq;

// One node of a skill tree. Every unit of a type shares the same definitions (through its
// UnitTemplate.SkillTree); which of them a unit has unlocked lives in its UnitProgression.
[GlobalClass]
public partial class SkillDefinition : Resource
{
	public const int MinTier = 1;
	public const int MaxTier = 5;

	[Export] public string Name = "Skill";
	[Export(PropertyHint.MultilineText)] public string Description = "";
	[Export] public Texture2D Icon;

	// Strength level of the skill, 1 (bottom of the tree) to 5 (top)
	[Export(PropertyHint.Range, "1,5,1")] public int Tier = 1;
	// Horizontal slot in the tree window (0 = leftmost); keeps a branch lined up under its follow-ups
	[Export(PropertyHint.Range, "0,7,1")] public int Column;
	[Export(PropertyHint.Range, "1,5,1")] public int Cost = 1;
	// All of these must be unlocked first
	[Export] public Godot.Collections.Array<SkillDefinition> Requires = new();

	// Effects
	[Export(PropertyHint.Range, "0,5,1")] public int BonusDeadliness;
	[Export(PropertyHint.Range, "0,5,1")] public int BonusStrength;
	[Export(PropertyHint.Range, "0,5,1")] public int BonusDurability;
	[Export(PropertyHint.Range, "0,5,1")] public int BonusCleverness;
	[Export(PropertyHint.Range, "0,5,1")] public int BonusQuickness;
	// Added to the unit's VisibilityRadius (fog of war)
	[Export] public float BonusVision;
	// Ability the unit can use once this skill is unlocked
	[Export] public Ability GrantedAbility;
	// Battle trait or battle action the unit gets once this skill is unlocked
	[Export] public CombatPassive GrantedCombatPassive;
	[Export] public CombatAbility GrantedCombatAbility;
	// Every this many seconds (ticks), each player unit on this unit's field gets 1 health back
	// (0 = no healing). Several healers on one field don't stack; the fastest counts.
	[Export] public int FieldHealInterval;
	// Extra experience (0.5 = +50%) for every tick of work done with these abilities
	[Export] public float ExperienceBonus;
	[Export] public Godot.Collections.Array<Ability> ExperienceBonusAbilities = new();

	// Unit level needed before a skill of this tier can be learned: 1, 3, 5, 7, 9
	public int RequiredUnitLevel => 1 + (Tier - 1) * 2;

	public int BonusAttribute(int index) => index switch
	{
		0 => BonusDeadliness,
		1 => BonusStrength,
		2 => BonusDurability,
		3 => BonusCleverness,
		4 => BonusQuickness,
		_ => 0
	};

	// Short effect lines for tooltips, e.g. "+1 Deadliness", "+60 vision range", "Unlocks Cut"
	public List<string> EffectLines()
	{
		var lines = new List<string>();
		for (int i = 0; i < SpecialAttributes.Count; i++)
			if (BonusAttribute(i) != 0)
				lines.Add($"+{BonusAttribute(i)} {SpecialAttributes.Names[i]}");
		if (BonusVision != 0)
			lines.Add($"+{BonusVision:0} vision range");
		if (GrantedAbility != null)
			lines.Add($"Unlocks ability: {GrantedAbility.Name}");
		if (GrantedCombatPassive != null)
			lines.Add($"Battle passive: {GrantedCombatPassive.Name}");
		if (GrantedCombatAbility != null)
			lines.Add($"Battle ability: {GrantedCombatAbility.Name}");
		if (FieldHealInterval > 0)
			lines.Add($"Heals units on its field: 1 HP every {FieldHealInterval}s");
		if (ExperienceBonus != 0)
			lines.Add($"+{ExperienceBonus * 100:0}% XP from {string.Join(", ", ExperienceBonusAbilities.Where(a => a != null && a.Replaces == null).Select(a => a.Name))}");
		return lines;
	}
}
