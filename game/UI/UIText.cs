using Godot;

// Shared text formatting for UI labels and tooltips
public static class UIText
{
	public static string Stats(BattleStats stats)
	{
		if (stats == null)
			return "No stats";

		string hp = stats.maxHealth > 0 ? $"{stats.health}/{stats.maxHealth}" : stats.health.ToString();
		string text = $"HP {hp}   ATK {stats.attack}   DEF {stats.defence}";
		if (stats.initiative != 0)
			text += $"   INI {stats.initiative}";
		if (stats.rangeUnit)
			text += "   (ranged)";
		return text;
	}

	public static string UnitTooltip(string name, BattleStats stats)
	{
		return $"{name}\n{Stats(stats)}";
	}

	public static void ClearChildren(Node node)
	{
		foreach (var child in node.GetChildren())
		{
			node.RemoveChild(child);
			child.QueueFree();
		}
	}
}
