using Godot;

public enum CombatPassiveKind
{
	PackHunter, // +Amount attack while another unit of the same type fights on its side
	Critical,   // Chance to deal Multiplier times the damage
	Stun,       // Chance that a damaging hit makes the target skip its next Amount turns
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
	// 0-1, for Critical and Stun
	[Export(PropertyHint.Range, "0,1,0.05")] public float Chance = 0.2f;
	// Attack bonus (PackHunter) or turns stunned (Stun)
	[Export] public int Amount = 1;
	// Damage multiplier (Critical)
	[Export] public float Multiplier = 2f;
}
