using System;
using System.Collections.Generic;
using System.Linq;

// The five battle positions of one side and who may hit whom.
//
//   back row     front row          (seen from the player's side; the enemy is mirrored)
//                  2  top
//     0  top
//                  3  middle
//     1  bottom
//                  4  bottom
//
// A back position is guarded by the two closest front positions (0 by 2 and 3, 1 by 3 and 4).
// Melee units can only hit a back unit once both of its guards are dead (or the guard slots are
// empty). Ranged units and spells can hit anyone.
public static class BattleFormation
{
	public const int SlotCount = 5;
	public static readonly int[] BackSlots = { 0, 1 };
	public static readonly int[] FrontSlots = { 2, 3, 4 };
	// Front slots first: the order units are placed in automatically
	public static readonly int[] FillOrder = { 3, 2, 4, 0, 1 };

	public static bool IsBack(int slot) => slot == 0 || slot == 1;

	public static int[] GuardsOf(int slot) => slot switch
	{
		0 => new[] { 2, 3 },
		1 => new[] { 3, 4 },
		_ => Array.Empty<int>(),
	};

	// Vertical position of a slot, in front-row steps (top front = 0, bottom front = 2)
	public static float Lane(int slot) => slot switch
	{
		0 => 0.5f,
		1 => 1.5f,
		2 => 0f,
		3 => 1f,
		_ => 2f,
	};

	public static bool IsGuarded(Combatant[] side, int slot)
	{
		foreach (int guard in GuardsOf(slot))
			if (side[guard] != null && side[guard].IsAlive)
				return true;
		return false;
	}

	// Enemies `attacker` may hit with a normal attack
	public static List<Combatant> Reachable(Combatant attacker, Combatant[] enemies, bool ignoreGuards = false)
	{
		bool anyone = ignoreGuards || attacker.Ranged;
		return enemies.Where(enemy => enemy != null && enemy.IsAlive && (anyone || !IsGuarded(enemies, enemy.Slot))).ToList();
	}

	// The closest enemy `attacker` may hit: nearest lane, front row before back row
	public static Combatant DefaultTarget(Combatant attacker, Combatant[] enemies, bool ignoreGuards = false)
	{
		Combatant best = null;
		float bestScore = float.MaxValue;
		foreach (var enemy in Reachable(attacker, enemies, ignoreGuards))
		{
			float score = Math.Abs(Lane(attacker.Slot) - Lane(enemy.Slot)) + (IsBack(enemy.Slot) ? 0.75f : 0f);
			if (score < bestScore)
			{
				bestScore = score;
				best = enemy;
			}
		}
		return best;
	}

	// Puts units into the free slots: melee into the front row first, ranged into the back row first
	public static void AutoPlace(Combatant[] side, IEnumerable<Combatant> units)
	{
		foreach (var unit in units.OrderBy(unit => unit.Ranged))
		{
			var order = unit.Ranged ? BackSlots.Concat(FrontSlots) : FillOrder;
			foreach (int slot in order)
			{
				if (side[slot] != null)
					continue;
				side[slot] = unit;
				unit.Slot = slot;
				break;
			}
		}
	}
}
