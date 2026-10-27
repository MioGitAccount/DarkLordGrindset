using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// The player's units (agents) and how many of them the player may have. Every unit is one agent.
// Castle buildings raise the total cap (towers) and the cap of each unit type (its dwelling and
// upgrades); recruiting stops at either. The top bar shows it through Changed.
public static class PlayerAgents
{
	private static readonly List<unit> _units = new();
	private static readonly List<Castle> _castles = new();

	public static event Action Changed;

	public static int Count => _units.Count;
	public static int Cap => _castles.Sum(c => c.AgentCap);

	public static int CountOf(UnitTemplate type) => _units.Count(u => u.Template == type);
	public static int CapFor(UnitTemplate type) => _castles.Sum(c => c.UnitCap(type));

	// Unit types the player has or may recruit, for the top bar tooltip
	public static IEnumerable<UnitTemplate> Types =>
		_units.Select(u => u.Template)
			.Concat(_castles.SelectMany(c => c.Built).Select(b => b.UnitCapFor))
			.Where(t => t != null)
			.Distinct();

	// Why another unit of this type can't join, or null when it can
	public static string RecruitBlockedReason(UnitTemplate type)
	{
		if (Count >= Cap)
			return $"Agent cap reached ({Count}/{Cap}) - upgrade the Tower";
		if (CountOf(type) >= CapFor(type))
			return $"{type.Name} cap reached ({CountOf(type)}/{CapFor(type)}) - upgrade its dwelling";
		return null;
	}

	public static void AddUnit(unit agent)
	{
		if (agent == null || _units.Contains(agent))
			return;
		_units.Add(agent);
		Changed?.Invoke();
	}

	public static void RemoveUnit(unit agent)
	{
		if (_units.Remove(agent))
			Changed?.Invoke();
	}

	public static void AddCastle(Castle castle)
	{
		if (castle == null || _castles.Contains(castle))
			return;
		_castles.Add(castle);
		castle.Changed += OnCastleChanged;
		Changed?.Invoke();
	}

	private static void OnCastleChanged() => Changed?.Invoke();

	// Start of a new game: no units, no castles
	public static void Reset()
	{
		foreach (var castle in _castles)
			castle.Changed -= OnCastleChanged;
		_units.Clear();
		_castles.Clear();
		Changed?.Invoke();
	}
}
