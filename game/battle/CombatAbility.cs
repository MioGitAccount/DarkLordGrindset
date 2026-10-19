using Godot;

public enum CombatAbilityKind
{
	Roar,     // Every living unit on the caster's side gets +Amount attack for its next Duration turns
	Firebolt, // Spell at any enemy (guards don't block it): attack + Amount against half the target's defence
}

// An action a unit takes instead of its normal attack. Player units use it when ordered to
// (on their next turn); neutral units use it whenever they can. Units get them from their
// UnitTemplate.CombatAbilities and from skills (SkillDefinition.GrantedCombatAbility).
[GlobalClass]
public partial class CombatAbility : Resource
{
	[Export] public string Name = "Ability";
	[Export(PropertyHint.MultilineText)] public string Description = "";
	[Export] public Texture2D Icon;
	[Export] public CombatAbilityKind Kind;
	[Export] public int ManaCost;
	// Own turns to wait before it can be used again (0 = every turn)
	[Export] public int Cooldown;
	// Attack bonus (Roar) or extra damage (Firebolt)
	[Export] public int Amount = 2;
	// Turns the effect lasts (Roar)
	[Export] public int Duration = 2;

	// True when the ability needs an enemy picked as its target
	public bool TargetsEnemy => Kind == CombatAbilityKind.Firebolt;
}
