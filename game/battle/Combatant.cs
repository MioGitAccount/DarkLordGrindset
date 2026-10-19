using Godot;
using System;
using System.Collections.Generic;

public enum BattleSide { Player, Enemy }

// Temporary attack bonus (e.g. from Roar), counted down at the end of its owner's turns
public class CombatBuff
{
	public string Source;
	public int AttackBonus;
	public int TurnsLeft;
	// Set on the caster's own buff, so the turn it was cast in doesn't count
	public bool SkipNextCountdown;
}

// One unit taking part in a battle. Holds a copy of its stats; the real unit (player unit or
// neutral) only gets the outcome (health, mana, death) when the battle is over.
public class Combatant
{
	public string Name;
	// Units of the same type count for Pack Hunter
	public string TypeKey;
	public Texture2D Icon;
	// "idle"/"attack" animations; null shows Icon instead
	public SpriteFrames Sprite;
	public BattleSide Side;
	// 0-4, see BattleFormation
	public int Slot = -1;

	public int Attack, Defence, Health, MaxHealth, Mana, MaxMana, Initiative, Quickness;
	public bool Ranged;
	public readonly List<CombatPassive> Passives = new();
	public readonly List<CombatAbility> Abilities = new();
	public readonly Dictionary<CombatAbility, int> Cooldowns = new();
	public readonly List<CombatBuff> Buffs = new();
	public int StunnedTurns;

	// Where the outcome goes; exactly one of these is set
	public unit PlayerUnit;
	public NeutralUnit Neutral;

	// Player orders: the target to keep attacking, and an ability to use on the next turn
	public Combatant OrderedTarget;
	public CombatAbility QueuedAbility;
	public Combatant QueuedAbilityTarget;

	public bool IsAlive => Health > 0;
	public bool IsPlayer => Side == BattleSide.Player;

	public int BuffAttack
	{
		get
		{
			int bonus = 0;
			foreach (var buff in Buffs)
				bonus += buff.AttackBonus;
			return bonus;
		}
	}

	public CombatPassive GetPassive(CombatPassiveKind kind) => Passives.Find(passive => passive.Kind == kind);

	public int CooldownLeft(CombatAbility ability) => Cooldowns.GetValueOrDefault(ability);

	public static Combatant FromUnit(unit playerUnit)
	{
		var stats = playerUnit.battleStats;
		var combatant = new Combatant
		{
			Name = playerUnit.Template?.Name ?? playerUnit.Name,
			TypeKey = playerUnit.Template?.ResourcePath ?? playerUnit.Name,
			Icon = playerUnit.GetIcon(),
			Sprite = playerUnit.Template?.MapSprite,
			Side = BattleSide.Player,
			PlayerUnit = playerUnit,
			Quickness = playerUnit.Attributes?.Quickness ?? 0,
		};
		combatant.CopyStats(stats);
		combatant.Passives.AddRange(playerUnit.CombatPassives);
		combatant.Abilities.AddRange(playerUnit.CombatAbilities);
		return combatant;
	}

	public static Combatant FromNeutral(NeutralUnit neutral)
	{
		var template = neutral.Template;
		var combatant = new Combatant
		{
			Name = template?.Name ?? "Neutral",
			TypeKey = template?.ResourcePath ?? "neutral",
			Icon = neutral.GetIcon(),
			Sprite = template?.MapSprite,
			Side = BattleSide.Enemy,
			Neutral = neutral,
			Quickness = neutral.GetAttributes()?.Quickness ?? 0,
		};
		combatant.CopyStats(neutral.GetEffectiveStats());
		if (template != null)
		{
			foreach (var passive in template.CombatPassives)
				if (passive != null)
					combatant.Passives.Add(passive);
			foreach (var ability in template.CombatAbilities)
				if (ability != null)
					combatant.Abilities.Add(ability);
		}
		return combatant;
	}

	private void CopyStats(BattleStats stats)
	{
		if (stats == null)
		{
			Health = MaxHealth = 1;
			return;
		}
		Attack = stats.attack;
		Defence = stats.defence;
		MaxHealth = Math.Max(1, stats.maxHealth > 0 ? stats.maxHealth : stats.health);
		Health = Math.Clamp(stats.health, 0, MaxHealth);
		MaxMana = stats.maxMana;
		Mana = Math.Clamp(stats.mana, 0, Math.Max(0, MaxMana));
		Initiative = stats.initiative;
		Ranged = stats.rangeUnit;
	}
}
