using System;
using System.Collections.Generic;
using System.Linq;

// What happened during one turn, for the battle screen to animate and log
public abstract class BattleEvent
{
	public Combatant Actor;
	public string Message;
}

public class HitEvent : BattleEvent
{
	public Combatant Target;
	public CombatAbility Ability; // null for a normal attack
	public int AttackValue;
	public int DefenceRoll;
	public int Damage;
	public bool Critical;
	public bool Stunned;
	public bool Killed;
	// The target's Shield Block or Evasion stopped the whole hit
	public bool ShieldBlocked;
	public bool Dodged;
	// Mana Steal: mana taken from the target
	public int ManaStolen;
}

// One action hitting several enemies at once (Fireball, Volley, Whirling Blades)
public class AreaEvent : BattleEvent
{
	public CombatAbility Ability;
	public List<HitEvent> Hits = new();
}

public class BuffEvent : BattleEvent
{
	public CombatAbility Ability;
	public List<Combatant> Affected = new();
}

// Health given back by Heal or HealAll, or by a Rejuvenation tick (Ability null; Actor is the healed unit)
public class HealEvent : BattleEvent
{
	public CombatAbility Ability;
	public List<(Combatant Target, int Amount)> Healed = new();
}

// Entangling Roots cast on Target
public class RootEvent : BattleEvent
{
	public CombatAbility Ability;
	public Combatant Target;
}

// A rooted unit takes the roots' damage; it can't use abilities or strike in melee this turn
public class RootedTurnEvent : BattleEvent
{
	public int Damage;
	public bool Killed;
}

public class SkipEvent : BattleEvent { }

// Battle rules without any visuals: turn order, targeting, attacks, passives and abilities.
// Every round, the living units act once each, highest initiative first (ties: higher Quickness,
// then random). The battle screen asks for the next actor, runs its turn and animates the events.
public class BattleSimulation
{
	public const int ManaRegenPerTurn = 1;
	// Melee units attacking from the back row deal 35% less attack (Long Reach removes this)
	public const float BackRowMeleeFactor = 0.65f;
	// Contempt for the Weak works on targets below this share of their health
	public const float WeakHealthShare = 0.5f;

	public readonly Combatant[] PlayerSlots = new Combatant[BattleFormation.SlotCount];
	public Combatant[] EnemySlots { get; private set; } = new Combatant[BattleFormation.SlotCount];
	// Neutrals that didn't fit; they come as the next wave once the current one is beaten
	public readonly List<List<Combatant>> EnemyWaves = new();
	public int Round { get; private set; }
	public int Wave { get; private set; } = 1;

	private readonly Random _rng = new();
	private readonly Queue<Combatant> _turnOrder = new();

	public IEnumerable<Combatant> Players => PlayerSlots.Where(c => c != null);
	public IEnumerable<Combatant> Enemies => EnemySlots.Where(c => c != null);
	public IEnumerable<Combatant> All => Players.Concat(Enemies);
	// Units still to act this round, in order
	public IEnumerable<Combatant> UpcomingTurns => _turnOrder.Where(c => c.IsAlive);

	public BattleSimulation(List<Combatant> enemies)
	{
		for (int i = 0; i < enemies.Count; i += BattleFormation.SlotCount)
			EnemyWaves.Add(enemies.Skip(i).Take(BattleFormation.SlotCount).ToList());
		StartNextWave();
	}

	public Combatant[] SlotsOf(BattleSide side) => side == BattleSide.Player ? PlayerSlots : EnemySlots;
	public Combatant[] OpponentsOf(Combatant combatant) => SlotsOf(combatant.IsPlayer ? BattleSide.Enemy : BattleSide.Player);
	public Combatant[] AlliesOf(Combatant combatant) => SlotsOf(combatant.Side);

	public bool SideDefeated(BattleSide side) => !SlotsOf(side).Any(c => c != null && c.IsAlive);
	public bool HasMoreWaves => EnemyWaves.Count > 0;

	// Puts the next wave of neutrals on the field; false when there is none left
	public bool StartNextWave()
	{
		if (EnemyWaves.Count == 0)
			return false;
		if (Round > 0)
			Wave++;
		var wave = EnemyWaves[0];
		EnemyWaves.RemoveAt(0);
		EnemySlots = new Combatant[BattleFormation.SlotCount];
		BattleFormation.AutoPlace(EnemySlots, wave);
		_turnOrder.Clear();
		Round = 0;
		return true;
	}

	// The unit whose turn it is, starting a new round when everyone has acted; null when one side is beaten
	public Combatant NextActor()
	{
		if (SideDefeated(BattleSide.Player) || SideDefeated(BattleSide.Enemy))
			return null;
		while (_turnOrder.Count > 0 && !_turnOrder.Peek().IsAlive)
			_turnOrder.Dequeue();
		if (_turnOrder.Count == 0)
		{
			ApplyLordBonuses();
			Round++;
			foreach (var combatant in All.Where(c => c.IsAlive).OrderByDescending(c => c.Initiative)
					.ThenByDescending(c => c.Quickness).ThenBy(_ => _rng.Next()))
				_turnOrder.Enqueue(combatant);
		}
		return _turnOrder.Dequeue();
	}

	public List<Combatant> ReachableTargets(Combatant attacker) => BattleFormation.Reachable(attacker, OpponentsOf(attacker));

	public List<Combatant> ValidTargets(Combatant caster, CombatAbility ability)
	{
		if (ability != null && ability.TargetsAlly)
			return AlliesOf(caster).Where(ally => ally != null && ally.IsAlive).ToList();
		return BattleFormation.Reachable(caster, OpponentsOf(caster), ignoreGuards: ability != null && IgnoresGuards(ability));
	}

	// The ally that needs healing most: the lowest share of its health left
	private Combatant MostWounded(Combatant caster) =>
		AlliesOf(caster).Where(ally => ally != null && ally.IsAlive)
			.OrderBy(ally => ally.Health / (float)ally.MaxHealth).FirstOrDefault();

	public static int HealAmount(Combatant caster, CombatAbility ability) => ability.Amount + caster.Cleverness / 2;

	// Targeted spells (Firebolt, Mana Steal) fly over the guards
	private static bool IgnoresGuards(CombatAbility ability) => ability.TargetsEnemy;

	// Who `attacker` would hit with a normal attack on its turn right now
	public Combatant PlannedTarget(Combatant attacker)
	{
		// A taunt overrides orders while the taunter lives and can be reached
		var taunter = attacker.TauntedBy;
		if (attacker.TauntTurns > 0 && taunter != null && taunter.IsAlive && ReachableTargets(attacker).Contains(taunter))
			return taunter;
		var ordered = attacker.OrderedTarget;
		if (ordered != null && ordered.IsAlive && ReachableTargets(attacker).Contains(ordered))
			return ordered;
		return BattleFormation.DefaultTarget(attacker, OpponentsOf(attacker));
	}

	public int EffectiveAttack(Combatant combatant)
	{
		int attack = combatant.Attack + combatant.BuffAttack;
		var packHunter = combatant.GetPassive(CombatPassiveKind.PackHunter);
		if (packHunter != null && AlliesOf(combatant).Any(ally => ally != null && ally != combatant && ally.IsAlive && ally.TypeKey == combatant.TypeKey))
			attack += packHunter.Amount;
		if (HasBackRowPenalty(combatant))
			attack = (int)MathF.Round(attack * BackRowMeleeFactor);
		return Math.Max(0, attack);
	}

	public static bool HasBackRowPenalty(Combatant combatant) =>
		!combatant.Ranged && BattleFormation.IsBack(combatant.Slot) && combatant.GetPassive(CombatPassiveKind.LongReach) == null;

	// Attack of Firebolt and Fireball: the spell's base power + Deadliness + half the Cleverness
	public static int SpellAttack(Combatant caster, CombatAbility ability) =>
		ability.Amount + caster.Deadliness + caster.Cleverness / 2;

	public static bool IsWeak(Combatant target) => target.Health < target.MaxHealth * WeakHealthShare;

	// Lord: every unit of the Lord's type on its side gets +Amount Deadliness and Strength, once per battle.
	// Several Lords don't stack.
	private void ApplyLordBonuses()
	{
		foreach (var side in new[] { BattleSide.Player, BattleSide.Enemy })
		{
			var units = SlotsOf(side).Where(c => c != null && c.IsAlive).ToList();
			foreach (var lord in units)
			{
				var passive = lord.GetPassive(CombatPassiveKind.Lord);
				if (passive == null)
					continue;
				foreach (var subject in units.Where(c => c.TypeKey == lord.TypeKey && !c.HasLordBonus))
				{
					subject.HasLordBonus = true;
					subject.Deadliness += passive.Amount;
					subject.Attack += passive.Amount * SpecialAttributes.AttackPerDeadliness;
					subject.MaxHealth += passive.Amount * SpecialAttributes.HealthPerStrength;
					subject.Health += passive.Amount * SpecialAttributes.HealthPerStrength;
				}
			}
		}
	}

	// Empty when the ability can be used now, otherwise why not
	public string WhyCantUse(Combatant caster, CombatAbility ability)
	{
		if (caster.RootedTurns > 0)
			return "Held by roots";
		if (caster.CooldownLeft(ability) > 0)
			return $"Ready in {caster.CooldownLeft(ability)} turns";
		if (caster.Mana < ability.ManaCost)
			return $"Needs {ability.ManaCost} mana";
		return "";
	}

	public bool CanUse(Combatant caster, CombatAbility ability) => WhyCantUse(caster, ability) == "";

	public List<BattleEvent> TakeTurn(Combatant actor)
	{
		var events = new List<BattleEvent>();
		if (actor.StunnedTurns > 0)
		{
			actor.StunnedTurns--;
			events.Add(new SkipEvent { Actor = actor, Message = $"{Label(actor)} is stunned and skips the turn" });
		}
		else if (actor.RootedTurns > 0)
		{
			// Held by roots: takes the damage, no abilities; ranged units can still shoot, melee units can't strike
			actor.RootedTurns--;
			int damage = Math.Min(actor.Health, actor.RootDamage);
			actor.Health -= damage;
			bool shoots = actor.IsAlive && actor.Ranged;
			events.Add(new RootedTurnEvent
			{
				Actor = actor,
				Damage = damage,
				Killed = !actor.IsAlive,
				Message = $"{Label(actor)} is held by roots: takes {damage} damage" + (!actor.IsAlive ? ", killed" : shoots ? " and can only shoot" : " and can't strike"),
			});
			actor.QueuedAbility = null;
			actor.QueuedAbilityTarget = null;
			var target = shoots ? PlannedTarget(actor) : null;
			if (target != null)
				events.Add(Hit(actor, target, null));
		}
		else
		{
			ChooseAndAct(actor, events);
		}
		EndTurn(actor, events);
		return events;
	}

	private void ChooseAndAct(Combatant actor, List<BattleEvent> events)
	{
		CombatAbility ability = null;
		Combatant target = null;
		if (actor.IsPlayer)
		{
			if (actor.QueuedAbility != null && CanUse(actor, actor.QueuedAbility))
			{
				ability = actor.QueuedAbility;
				target = actor.QueuedAbilityTarget;
			}
			actor.QueuedAbility = null;
			actor.QueuedAbilityTarget = null;
		}
		else
		{
			ability = actor.Abilities.FirstOrDefault(a => CanUse(actor, a) && WorthUsing(actor, a));
		}

		if (ability != null)
		{
			if (ability.NeedsTarget)
			{
				var valid = ValidTargets(actor, ability);
				if (target == null || !valid.Contains(target))
					target = ability.TargetsAlly ? MostWounded(actor)
						: actor.IsPlayer ? BattleFormation.DefaultTarget(actor, OpponentsOf(actor), true)
						// Neutrals root the strongest melee enemy that isn't held already (rooted archers still shoot), and shoot the weakest
						: ability.Kind == CombatAbilityKind.Roots ? valid.Where(enemy => enemy.RootedTurns == 0).OrderBy(enemy => enemy.Ranged).ThenByDescending(enemy => enemy.Attack).FirstOrDefault()
						: valid.OrderBy(enemy => enemy.Health).FirstOrDefault();
				if (target != null)
				{
					Use(actor, ability, target, events);
					return;
				}
			}
			else
			{
				Use(actor, ability, null, events);
				return;
			}
		}

		target = PlannedTarget(actor);
		if (target != null)
			events.Add(Hit(actor, target, null));
	}

	// Neutral AI: don't roar while the side is still roaring, heal only the wounded,
	// save Volley and Whirling Blades for more than one target
	private bool WorthUsing(Combatant actor, CombatAbility ability) => ability.Kind switch
	{
		CombatAbilityKind.Volley => OpponentsOf(actor).Count(enemy => enemy != null && enemy.IsAlive) > 1,
		CombatAbilityKind.WhirlingBlades => ReachableTargets(actor).Count > 1,
		CombatAbilityKind.Roots => OpponentsOf(actor).Any(enemy => enemy != null && enemy.IsAlive && enemy.RootedTurns == 0),
		CombatAbilityKind.Taunt => OpponentsOf(actor).Any(enemy => enemy != null && enemy.IsAlive && (enemy.TauntTurns == 0 || enemy.TauntedBy?.IsAlive != true)),
		CombatAbilityKind.Rejuvenation => AlliesOf(actor).Any(ally => ally != null && ally.IsAlive && ally.Health < ally.MaxHealth
			&& !ally.Buffs.Any(buff => buff.Source == ability.Name)),
		CombatAbilityKind.Roar => !actor.Buffs.Any(buff => buff.Source == ability.Name),
		CombatAbilityKind.Heal or CombatAbilityKind.HealAll => AlliesOf(actor).Any(ally => ally != null && ally.IsAlive && ally.Health < ally.MaxHealth),
		_ => true,
	};

	private void Use(Combatant actor, CombatAbility ability, Combatant target, List<BattleEvent> events)
	{
		actor.Mana -= ability.ManaCost;
		if (ability.Cooldown > 0)
			actor.Cooldowns[ability] = ability.Cooldown + 1; // +1: this turn's countdown is in EndTurn
		switch (ability.Kind)
		{
			case CombatAbilityKind.Roar:
				var buff = new BuffEvent { Actor = actor, Ability = ability };
				foreach (var ally in AlliesOf(actor))
				{
					if (ally == null || !ally.IsAlive)
						continue;
					ally.Buffs.RemoveAll(b => b.Source == ability.Name); // refresh, don't stack
					ally.Buffs.Add(new CombatBuff
					{
						Source = ability.Name,
						AttackBonus = ability.Amount,
						TurnsLeft = ability.Duration,
						SkipNextCountdown = ally == actor,
					});
					buff.Affected.Add(ally);
				}
				buff.Message = $"{Label(actor)} uses {ability.Name}: +{ability.Amount} attack to {buff.Affected.Count} allies for {ability.Duration} turns";
				events.Add(buff);
				break;
			case CombatAbilityKind.Firebolt:
			case CombatAbilityKind.AimedShot:
				events.Add(Hit(actor, target, ability));
				break;
			case CombatAbilityKind.Fireball:
			case CombatAbilityKind.Volley:
			case CombatAbilityKind.WhirlingBlades:
				var area = new AreaEvent { Actor = actor, Ability = ability };
				var struck = ability.Kind == CombatAbilityKind.WhirlingBlades ? ReachableTargets(actor)
					: OpponentsOf(actor).Where(enemy => enemy != null && enemy.IsAlive).ToList();
				foreach (var enemy in struck)
					area.Hits.Add(Hit(actor, enemy, ability));
				area.Message = $"{Label(actor)} {(ability.IsSpell ? "casts" : "uses")} {ability.Name} at {area.Hits.Count} enemies";
				events.Add(area);
				break;
			case CombatAbilityKind.ManaSteal:
				events.Add(StealMana(actor, target, ability));
				break;
			case CombatAbilityKind.Heal:
			case CombatAbilityKind.HealAll:
				var heal = new HealEvent { Actor = actor, Ability = ability };
				var patients = ability.Kind == CombatAbilityKind.Heal ? new[] { target } : AlliesOf(actor).Where(ally => ally != null && ally.IsAlive).ToArray();
				foreach (var patient in patients)
				{
					int healed = Math.Min(HealAmount(actor, ability), patient.MaxHealth - patient.Health);
					patient.Health += healed;
					heal.Healed.Add((patient, healed));
				}
				heal.Message = $"{Label(actor)} casts {ability.Name}: " + string.Join(", ", heal.Healed.Select(h => $"{Label(h.Target)} +{h.Amount} health"));
				events.Add(heal);
				break;
			case CombatAbilityKind.Roots:
				target.RootedTurns = Math.Max(target.RootedTurns, ability.Duration);
				target.RootDamage = ability.Amount;
				events.Add(new RootEvent
				{
					Actor = actor,
					Ability = ability,
					Target = target,
					Message = $"{Label(actor)} casts {ability.Name} at {Label(target)}: held for {ability.Duration} turns, {ability.Amount} damage each",
				});
				break;
			case CombatAbilityKind.Taunt:
				var taunt = new BuffEvent { Actor = actor, Ability = ability };
				foreach (var enemy in OpponentsOf(actor))
				{
					if (enemy == null || !enemy.IsAlive)
						continue;
					enemy.TauntedBy = actor;
					enemy.TauntTurns = ability.Duration;
					taunt.Affected.Add(enemy);
				}
				taunt.Message = $"{Label(actor)} uses {ability.Name}: {taunt.Affected.Count} enemies must attack it for {ability.Duration} turns";
				events.Add(taunt);
				break;
			case CombatAbilityKind.Rejuvenation:
				var rejuvenation = new BuffEvent { Actor = actor, Ability = ability };
				foreach (var ally in AlliesOf(actor))
				{
					if (ally == null || !ally.IsAlive)
						continue;
					ally.Buffs.RemoveAll(b => b.Source == ability.Name); // refresh, don't stack
					ally.Buffs.Add(new CombatBuff
					{
						Source = ability.Name,
						HealPerTurn = ability.Amount,
						TurnsLeft = ability.Duration,
						SkipNextCountdown = ally == actor,
					});
					rejuvenation.Affected.Add(ally);
				}
				rejuvenation.Message = $"{Label(actor)} casts {ability.Name}: {rejuvenation.Affected.Count} allies heal {ability.Amount} per turn for {ability.Duration} turns";
				events.Add(rejuvenation);
				break;
			case CombatAbilityKind.Curse:
			case CombatAbilityKind.CurseAll:
				var curse = new BuffEvent { Actor = actor, Ability = ability };
				var victims = ability.Kind == CombatAbilityKind.Curse ? new[] { target } : OpponentsOf(actor).Where(enemy => enemy != null && enemy.IsAlive).ToArray();
				foreach (var victim in victims)
				{
					victim.Buffs.RemoveAll(b => b.Source == ability.Name); // refresh, don't stack
					victim.Buffs.Add(new CombatBuff { Source = ability.Name, AttackBonus = -ability.Amount, TurnsLeft = ability.Duration });
					curse.Affected.Add(victim);
				}
				curse.Message = $"{Label(actor)} casts {ability.Name}: -{ability.Amount} attack to {string.Join(", ", victims.Select(Label))} for {ability.Duration} turns";
				events.Add(curse);
				break;
		}
	}

	private HitEvent Hit(Combatant attacker, Combatant target, CombatAbility ability)
	{
		int attack = EffectiveAttack(attacker);
		int defence = target.Defence + (target.GetPassive(CombatPassiveKind.BarkSkin)?.Amount ?? 0);
		bool contempt = false;
		if (ability != null && ability.IsSpell)
		{
			attack = SpellAttack(attacker, ability);
			defence /= 2;
		}
		else if (ability?.Kind == CombatAbilityKind.AimedShot)
		{
			attack += ability.Amount;
			defence /= 2;
		}
		else if (ability?.Kind == CombatAbilityKind.Volley)
			attack = Math.Max(0, attack - ability.Amount);
		else if (ability?.Kind == CombatAbilityKind.WhirlingBlades)
			attack += ability.Amount;
		else if (ability == null && IsWeak(target))
		{
			var contemptPassive = attacker.GetPassive(CombatPassiveKind.ContemptForTheWeak);
			if (contemptPassive != null)
			{
				attack += contemptPassive.Amount;
				contempt = true;
			}
		}
		var hit = new HitEvent { Actor = attacker, Target = target, Ability = ability, AttackValue = attack };
		hit.DefenceRoll = BattleMath.RollDefence(defence, _rng);
		hit.Damage = BattleMath.Damage(attack, hit.DefenceRoll);

		var critical = attacker.GetPassive(CombatPassiveKind.Critical);
		if (ability == null && critical != null && hit.Damage > 0 && _rng.NextDouble() < critical.Chance)
		{
			hit.Critical = true;
			hit.Damage = (int)MathF.Round(hit.Damage * critical.Multiplier);
		}

		if ((ability == null || !ability.IsSpell) && hit.Damage > 0)
		{
			var shield = target.GetPassive(CombatPassiveKind.ShieldBlock);
			var evasion = target.GetPassive(CombatPassiveKind.Evasion);
			if (shield != null && _rng.NextDouble() < shield.Chance)
				hit.ShieldBlocked = true;
			else if (evasion != null && _rng.NextDouble() < evasion.Chance)
				hit.Dodged = true;
			if (hit.ShieldBlocked || hit.Dodged)
				hit.Damage = 0;
		}

		target.Health = Math.Max(0, target.Health - hit.Damage);
		hit.Killed = !target.IsAlive;

		var stun = attacker.GetPassive(CombatPassiveKind.Stun);
		if (ability == null && stun != null && hit.Damage > 0 && !hit.Killed && _rng.NextDouble() < stun.Chance)
		{
			hit.Stunned = true;
			target.StunnedTurns = Math.Max(target.StunnedTurns, stun.Amount);
		}

		string how = ability == null ? "hits" : ability.IsSpell ? $"casts {ability.Name} at" : $"uses {ability.Name} on";
		string damage = hit.ShieldBlocked ? "0 (shield block)"
			: hit.Dodged ? "0 (dodged)"
			: hit.Critical ? $"{BattleMath.Damage(attack, hit.DefenceRoll)}, critical x{critical.Multiplier:0.#} = {hit.Damage}"
			: hit.Damage.ToString();
		hit.Message = $"{Label(attacker)} {how} {Label(target)}{(contempt ? " (contempt for the weak)" : "")}: {attack} attack - {hit.DefenceRoll} defence = {damage}" + (hit.Stunned ? ", stunned" : "") + (hit.Killed ? ", killed" : "");
		return hit;
	}

	// Mana Steal: takes the target's mana; a target without any takes damage instead
	private HitEvent StealMana(Combatant caster, Combatant target, CombatAbility ability)
	{
		var hit = new HitEvent { Actor = caster, Target = target, Ability = ability };
		int stolen = Math.Min(ability.Amount, target.Mana);
		if (stolen > 0)
		{
			target.Mana -= stolen;
			caster.Mana = Math.Min(caster.MaxMana, caster.Mana + stolen);
			hit.ManaStolen = stolen;
			hit.Message = $"{Label(caster)} casts {ability.Name} at {Label(target)}: steals {stolen} mana";
		}
		else
		{
			hit.Damage = Math.Min(target.Health, ability.EmptyDamage);
			target.Health -= hit.Damage;
			hit.Killed = !target.IsAlive;
			hit.Message = $"{Label(caster)} casts {ability.Name} at {Label(target)}: no mana to steal, drains {hit.Damage} health instead" + (hit.Killed ? ", killed" : "");
		}
		return hit;
	}

	private void EndTurn(Combatant actor, List<BattleEvent> events)
	{
		// Rejuvenation heals before counting down; not on the turn it was cast by this unit
		int heal = actor.IsAlive ? actor.Buffs.Where(buff => !buff.SkipNextCountdown).Sum(buff => buff.HealPerTurn) : 0;
		heal = Math.Min(heal, actor.MaxHealth - actor.Health);
		if (heal > 0)
		{
			actor.Health += heal;
			var tick = new HealEvent { Actor = actor, Message = $"{Label(actor)} regrows {heal} health" };
			tick.Healed.Add((actor, heal));
			events.Add(tick);
		}
		foreach (var buff in actor.Buffs)
		{
			if (buff.SkipNextCountdown)
				buff.SkipNextCountdown = false;
			else
				buff.TurnsLeft--;
		}
		actor.Buffs.RemoveAll(buff => buff.TurnsLeft <= 0);
		if (actor.TauntTurns > 0)
			actor.TauntTurns--;
		foreach (var ability in actor.Cooldowns.Keys.ToList())
			actor.Cooldowns[ability] = Math.Max(0, actor.Cooldowns[ability] - 1);
		if (actor.IsAlive)
			actor.Mana = Math.Min(actor.MaxMana, actor.Mana + ManaRegenPerTurn);
	}

	public static string Label(Combatant combatant) => combatant.IsPlayer ? combatant.Name : $"enemy {combatant.Name}";

	// Experience each surviving player unit gets for the enemies killed
	public static int ExperienceFor(IEnumerable<Combatant> killedEnemies, int survivors)
	{
		if (survivors <= 0)
			return 0;
		int total = killedEnemies.Sum(enemy => enemy.MaxHealth * 2 + enemy.Attack * 2);
		return (int)Math.Ceiling(total / (double)survivors);
	}
}
