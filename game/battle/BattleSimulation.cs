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
}

public class BuffEvent : BattleEvent
{
	public CombatAbility Ability;
	public List<Combatant> Affected = new();
}

public class SkipEvent : BattleEvent { }

// Battle rules without any visuals: turn order, targeting, attacks, passives and abilities.
// Every round, the living units act once each, highest initiative first (ties: higher Quickness,
// then random). The battle screen asks for the next actor, runs its turn and animates the events.
public class BattleSimulation
{
	public const int ManaRegenPerTurn = 1;

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
			Round++;
			foreach (var combatant in All.Where(c => c.IsAlive).OrderByDescending(c => c.Initiative)
					.ThenByDescending(c => c.Quickness).ThenBy(_ => _rng.Next()))
				_turnOrder.Enqueue(combatant);
		}
		return _turnOrder.Dequeue();
	}

	public List<Combatant> ReachableTargets(Combatant attacker) => BattleFormation.Reachable(attacker, OpponentsOf(attacker));

	public List<Combatant> ValidTargets(Combatant caster, CombatAbility ability) =>
		BattleFormation.Reachable(caster, OpponentsOf(caster), ignoreGuards: ability != null && IgnoresGuards(ability));

	private static bool IgnoresGuards(CombatAbility ability) => ability.Kind == CombatAbilityKind.Firebolt;

	// Who `attacker` would hit with a normal attack on its turn right now
	public Combatant PlannedTarget(Combatant attacker)
	{
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
		return attack;
	}

	// Empty when the ability can be used now, otherwise why not
	public string WhyCantUse(Combatant caster, CombatAbility ability)
	{
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
		else
		{
			ChooseAndAct(actor, events);
		}
		EndTurn(actor);
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
			if (ability.TargetsEnemy)
			{
				var valid = ValidTargets(actor, ability);
				if (target == null || !valid.Contains(target))
					target = actor.IsPlayer ? BattleFormation.DefaultTarget(actor, OpponentsOf(actor), true)
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

	// Neutral AI: don't roar while the side is still roaring
	private static bool WorthUsing(Combatant actor, CombatAbility ability) =>
		ability.Kind != CombatAbilityKind.Roar || !actor.Buffs.Any(buff => buff.Source == ability.Name);

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
				events.Add(Hit(actor, target, ability));
				break;
		}
	}

	private HitEvent Hit(Combatant attacker, Combatant target, CombatAbility ability)
	{
		int attack = EffectiveAttack(attacker);
		int defence = target.Defence;
		if (ability?.Kind == CombatAbilityKind.Firebolt)
		{
			attack += ability.Amount;
			defence /= 2;
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

		target.Health = Math.Max(0, target.Health - hit.Damage);
		hit.Killed = !target.IsAlive;

		var stun = attacker.GetPassive(CombatPassiveKind.Stun);
		if (ability == null && stun != null && hit.Damage > 0 && !hit.Killed && _rng.NextDouble() < stun.Chance)
		{
			hit.Stunned = true;
			target.StunnedTurns = Math.Max(target.StunnedTurns, stun.Amount);
		}

		string how = ability != null ? $"casts {ability.Name} at" : "hits";
		string damage = hit.Critical ? $"{BattleMath.Damage(attack, hit.DefenceRoll)}, critical x{critical.Multiplier:0.#} = {hit.Damage}" : hit.Damage.ToString();
		hit.Message = $"{Label(attacker)} {how} {Label(target)}: {attack} attack - {hit.DefenceRoll} defence = {damage}" + (hit.Stunned ? ", stunned" : "") + (hit.Killed ? ", killed" : "");
		return hit;
	}

	private void EndTurn(Combatant actor)
	{
		foreach (var buff in actor.Buffs)
		{
			if (buff.SkipNextCountdown)
				buff.SkipNextCountdown = false;
			else
				buff.TurnsLeft--;
		}
		actor.Buffs.RemoveAll(buff => buff.TurnsLeft <= 0);
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
