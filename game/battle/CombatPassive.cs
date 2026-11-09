using Godot;

public enum CombatPassiveKind
{
	PackHunter, // +Amount attack while another unit of the same type fights on its side
	Critical,   // Chance to deal Multiplier times the damage
	Stun,       // Chance that a damaging hit makes the target skip its next Amount turns
	ContemptForTheWeak, // +Amount attack on normal attacks against targets below half health
	LongReach,  // No back-row penalty for a melee unit (see BattleSimulation.BackRowMeleeFactor)
	Lord,       // Every unit of the same type on its side (itself too) gets +Amount Deadliness and Strength
	ShieldBlock, // Chance to block all damage of a weapon hit (normal attacks and Aimed Shot, not spells)
	Evasion,     // Chance to dodge all damage of a weapon hit, like ShieldBlock
	BarkSkin,    // +Amount defence against every hit
	Defender,    // +Amount defence against every hit while standing in the front row
	FrostArmor,  // Chance that an enemy striking this unit in melee is frozen and skips its next Amount turns
	Champion,    // +Amount attack and defence and +HealthBonus health once more (on top of the skill's own
	             // bonus) while it is the only living unit on its side, or in every Arena fight
	Berserk,     // +Amount attack while below half its health
}

// A combat trait that works on its own (no order needed). Units get them from their
// UnitTemplate.CombatPassives and from skills (SkillDefinition.GrantedCombatPassive).
[GlobalClass]
public partial class CombatPassive : Resource
{
	[Export] public string Name = "Passive";
	[Export(PropertyHint.MultilineText)] public string Description = "";
	[Export] public Texture2D Icon;
	[Export] public CombatPassiveKind Kind;
	// 0-1, for Critical, Stun, ShieldBlock, Evasion and FrostArmor
	[Export(PropertyHint.Range, "0,1,0.05")] public float Chance = 0.2f;
	// Attack bonus (PackHunter, ContemptForTheWeak, Berserk), turns stunned (Stun), attribute bonus (Lord)
	// or defence bonus (BarkSkin, Defender)
	[Export] public int Amount = 1;
	// Extra health (Champion)
	[Export] public int HealthBonus;
	// Damage multiplier (Critical)
	[Export] public float Multiplier = 2f;
	// A weaker version of this passive; a unit that has both keeps only this one
	[Export] public CombatPassive Replaces;
}
