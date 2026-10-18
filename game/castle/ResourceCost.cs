using Godot;
using System.Collections.Generic;

// An amount of one resource the player pays, e.g. 50 Wood for a building
[GlobalClass]
public partial class ResourceCost : Resource
{
	[Export] public ResourceType Type = ResourceType.Gold;
	[Export] public float Amount;

	public static bool CanAfford(IEnumerable<ResourceCost> costs)
	{
		foreach (var cost in costs)
			if (cost != null && PlayerResources.Get(cost.Type) < cost.Amount)
				return false;
		return true;
	}

	// Takes all of the costs, or nothing when the player can't pay every one of them
	public static bool TryPay(IEnumerable<ResourceCost> costs)
	{
		if (!CanAfford(costs))
			return false;
		foreach (var cost in costs)
			if (cost != null)
				PlayerResources.Add(cost.Type, -cost.Amount);
		return true;
	}

	// e.g. "50 Wood, 20 Gold"; "free" when there is nothing to pay
	public static string Text(IEnumerable<ResourceCost> costs)
	{
		var parts = new List<string>();
		foreach (var cost in costs)
			if (cost != null && cost.Amount > 0)
				parts.Add($"{cost.Amount:0} {cost.Type}");
		return parts.Count > 0 ? string.Join(", ", parts) : "free";
	}
}
