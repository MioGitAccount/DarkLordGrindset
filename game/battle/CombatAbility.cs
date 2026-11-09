using Godot;

public enum CombatAbilityKind
{
	Roar,     // Every living unit on the caster's side gets +Amount attack for its next Duration turns
	Firebolt, // Spell at any enemy (guards don't block it): spell attack against half the target's defence
	Fireball, // Spell at every enemy at once: spell attack against half of each target's defence
	ManaSteal, // Takes up to Amount mana from any enemy; one without mana takes EmptyDamage instead
	Heal,     // One ally (or the caster) gets back Amount + half the caster's Cleverness health
	HealAll,  // Every living ally (the caster too) gets back Amount + half the caster's Cleverness health
	Curse,    // One enemy (guards don't block it) gets -Amount attack for its next Duration turns
	CurseAll, // Every enemy gets -Amount attack for its next Duration turns
	AimedShot, // Shot at any enemy (guards don't block it): attack + Amount against half the target's defence
	Volley,    // Arrows at every enemy at once: attack - Amount against each target's full defence
	WhirlingBlades, // Normal hit (+Amount attack) on every enemy the unit can reach right now
	Roots,    // One enemy (guards don't block it) is held for its next Duration turns: Amount damage on each,
	          // no abilities, and only ranged units can still attack
	Rejuvenation, // Every living ally (the caster too) heals Amount at the end of each of its next Duration turns
	Taunt,    // For their next Duration turns, enemies that can reach the caster must aim their normal attacks at it
	Fortify,  // Every living unit on the caster's side gets +Amount defence for its next Duration turns
	Freeze,   // Melee hit (+Amount attack) on an enemy the unit can reach; unless blocked, the target skips its next Duration turns
	Bleed,    // Melee hit on an enemy the unit can reach; unless blocked, it loses Amount% of the attacker's Deadliness
	          // (min 1, no defence) at the end of each of its next Duration turns; PreventsHealing stops heals meanwhile
	Charge,   // Back row only: steps into a front tile in front of it (swapping with whoever stands there) and
	          // makes a normal attack (+Amount attack) on the closest enemy from there
	CrushDefence, // Melee hit (+Amount attack) on an enemy the unit can reach; unless blocked, the target loses
	          // defence equal to the attacker's Deadliness for its next Duration turns
	Blizzard, // Frost spell at every enemy at once (spell attack against half each one's defence); every
	          // enemy that survives is frozen and skips its next Duration turns
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
	// Attack bonus (Roar), defence bonus (Fortify), spell base power (Firebolt, Fireball; see BattleSimulation.SpellAttack),
	// mana taken (ManaSteal), base healing (Heal, HealAll), attack taken away (Curse, CurseAll)
	// attack added to the hit (AimedShot, WhirlingBlades, Freeze), taken off each arrow (Volley),
	// damage per rooted turn (Roots), healing per turn (Rejuvenation), % of Deadliness bled per turn (Bleed),
	// attack added to the strike (Charge, CrushDefence) or spell base power (Blizzard)
	[Export] public int Amount = 2;
	// Turns the effect lasts (Roar, Fortify, Freeze, Bleed, Curse, CurseAll, Roots, Rejuvenation, Taunt)
	[Export] public int Duration = 2;
	// Bleed: the bleeding target can't be healed until the bleed ends
	[Export] public bool PreventsHealing;
	// Damage dealt (no defence) when Mana Steal hits an enemy without mana
	[Export] public int EmptyDamage = 3;
	// A weaker version of this ability; a unit that has both keeps only this one
	[Export] public CombatAbility Replaces;

	// True when the ability needs an enemy picked as its target
	public bool TargetsEnemy => Kind == CombatAbilityKind.Firebolt || Kind == CombatAbilityKind.ManaSteal || Kind == CombatAbilityKind.Curse || Kind == CombatAbilityKind.AimedShot || Kind == CombatAbilityKind.Roots || Kind == CombatAbilityKind.Freeze || Kind == CombatAbilityKind.Bleed || Kind == CombatAbilityKind.CrushDefence;
	// True when the ability needs an ally (or the caster) picked as its target
	public bool TargetsAlly => Kind == CombatAbilityKind.Heal;
	public bool NeedsTarget => TargetsEnemy || TargetsAlly;
	public bool IsCurse => Kind == CombatAbilityKind.Curse || Kind == CombatAbilityKind.CurseAll;
	// A weapon hit that has to reach its target like a normal melee attack (guards block it)
	public bool IsMeleeHit => Kind == CombatAbilityKind.Freeze || Kind == CombatAbilityKind.Bleed || Kind == CombatAbilityKind.CrushDefence || Kind == CombatAbilityKind.Charge;
	public bool IsSpell => Kind == CombatAbilityKind.Firebolt || Kind == CombatAbilityKind.Fireball || Kind == CombatAbilityKind.Blizzard;
}
