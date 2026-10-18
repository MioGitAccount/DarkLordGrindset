using Godot;
using System;
using System.Collections.Generic;

// What the player owns of every ResourceType. Abilities add to it (cutting trees, stealing, ...);
// the top bar shows it through Changed.
public static class PlayerResources
{
	private static readonly Dictionary<ResourceType, float> _amounts = new();

	// (type, new amount)
	public static event Action<ResourceType, float> Changed;

	public static float Get(ResourceType type) => _amounts.GetValueOrDefault(type);

	public static void Add(ResourceType type, float amount)
	{
		if (amount == 0)
			return;
		_amounts[type] = Get(type) + amount;
		Changed?.Invoke(type, _amounts[type]);
	}

	// Takes the amount only if the player has all of it
	public static bool TrySpend(ResourceType type, float amount)
	{
		if (Get(type) < amount)
			return false;
		Add(type, -amount);
		return true;
	}

	// Start of a new game: everything back to 0
	public static void Reset()
	{
		_amounts.Clear();
		foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
			Changed?.Invoke(type, 0);
	}
}
