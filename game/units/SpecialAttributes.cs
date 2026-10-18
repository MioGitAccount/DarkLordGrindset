using Godot;
using System;

// The five core attributes every unit has (0-10 each). They sit on top of the base BattleStats:
// Deadliness -> attack, Strength -> health, Durability -> defence, Cleverness -> mana, Quickness -> movement speed.
// Order matters: it is the clockwise order of the pentagon corners, starting at the top.
[GlobalClass]
public partial class SpecialAttributes : Resource
{
	public const int MinValue = 0;
	public const int MaxValue = 10;

	public const int AttackPerDeadliness = 1;
	public const int HealthPerStrength = 2;
	public const int DefencePerDurability = 1;
	public const int ManaPerCleverness = 1;
	public const float MoveSpeedPerQuickness = 0.1f; // +10% movement speed per point

	public static readonly string[] Names = { "Deadliness", "Strength", "Durability", "Cleverness", "Quickness" };
	public static readonly string[] ShortNames = { "DEA", "STR", "DUR", "CLE", "QUI" };
	public const int Count = 5;

	private int _deadliness, _strength, _durability, _cleverness, _quickness;

	[Export(PropertyHint.Range, "0,10,1")] public int Deadliness { get => _deadliness; set => _deadliness = Clamp(value); }
	[Export(PropertyHint.Range, "0,10,1")] public int Strength { get => _strength; set => _strength = Clamp(value); }
	[Export(PropertyHint.Range, "0,10,1")] public int Durability { get => _durability; set => _durability = Clamp(value); }
	[Export(PropertyHint.Range, "0,10,1")] public int Cleverness { get => _cleverness; set => _cleverness = Clamp(value); }
	[Export(PropertyHint.Range, "0,10,1")] public int Quickness { get => _quickness; set => _quickness = Clamp(value); }

	public SpecialAttributes() { }
	public SpecialAttributes(int deadliness, int strength, int durability, int cleverness, int quickness)
	{
		Deadliness = deadliness;
		Strength = strength;
		Durability = durability;
		Cleverness = cleverness;
		Quickness = quickness;
	}

	private static int Clamp(int value) => Math.Clamp(value, MinValue, MaxValue);

	// Index access in pentagon order (0 = Deadliness ... 4 = Quickness)
	public int Get(int index) => index switch
	{
		0 => Deadliness,
		1 => Strength,
		2 => Durability,
		3 => Cleverness,
		4 => Quickness,
		_ => throw new ArgumentOutOfRangeException(nameof(index))
	};

	public void Set(int index, int value)
	{
		switch (index)
		{
			case 0: Deadliness = value; break;
			case 1: Strength = value; break;
			case 2: Durability = value; break;
			case 3: Cleverness = value; break;
			case 4: Quickness = value; break;
			default: throw new ArgumentOutOfRangeException(nameof(index));
		}
	}

	public float MoveSpeedMultiplier => 1f + MoveSpeedPerQuickness * Quickness;

	// Returns a new BattleStats: the base stats plus the attribute bonuses. The base is never modified,
	// so a template's shared DefaultStats stay untouched.
	public BattleStats ApplyTo(BattleStats baseStats)
	{
		var stats = baseStats != null ? (BattleStats)baseStats.Duplicate() : new BattleStats();
		stats.attack += Deadliness * AttackPerDeadliness;
		stats.health += Strength * HealthPerStrength;
		stats.maxHealth += Strength * HealthPerStrength;
		stats.defence += Durability * DefencePerDurability;
		stats.mana += Cleverness * ManaPerCleverness;
		stats.maxMana += Cleverness * ManaPerCleverness;
		return stats;
	}
}
