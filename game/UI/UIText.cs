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
		if (stats.maxMana > 0)
			text += $"   MP {stats.mana}/{stats.maxMana}";
		if (stats.initiative != 0)
			text += $"   INI {stats.initiative}";
		if (stats.rangeUnit)
			text += "   (ranged)";
		return text;
	}

	public static string Attributes(SpecialAttributes attributes)
	{
		if (attributes == null)
			return "";
		var parts = new string[SpecialAttributes.Count];
		for (int i = 0; i < parts.Length; i++)
			parts[i] = $"{SpecialAttributes.ShortNames[i]} {attributes.Get(i)}";
		return string.Join("  ", parts);
	}

	// What one attribute adds to the unit, e.g. "+6 HP" or "+30% speed"
	public static string AttributeBonus(SpecialAttributes attributes, int index)
	{
		int value = attributes.Get(index);
		return index switch
		{
			0 => $"+{value * SpecialAttributes.AttackPerDeadliness} ATK",
			1 => $"+{value * SpecialAttributes.HealthPerStrength} HP",
			2 => $"+{value * SpecialAttributes.DefencePerDurability} DEF",
			3 => $"+{value * SpecialAttributes.ManaPerCleverness} MP",
			_ => $"+{Mathf.RoundToInt(value * SpecialAttributes.MoveSpeedPerQuickness * 100)}% speed",
		};
	}

	public static string UnitTooltip(string name, BattleStats stats, SpecialAttributes attributes = null)
	{
		string text = $"{name}\n{Stats(stats)}";
		if (attributes != null)
			text += $"\n{Attributes(attributes)}";
		return text;
	}

	public static string AbilityTooltip(Ability ability, bool running)
	{
		string text = ability.Name ?? "Ability";
		if (!string.IsNullOrEmpty(ability.Description))
			text += $"\n{ability.Description}";
		text += running ? "\nWorking... click to stop" : "\nClick to start";
		return text;
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
