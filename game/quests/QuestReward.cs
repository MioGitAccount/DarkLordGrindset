using Godot;
using System.Collections.Generic;

// What finishing a quest gives: resources, XP, a unit that joins (ignoring agent caps) and changes
// to the quest's field (perks added or removed, neutral guards leaving)
[GlobalClass]
public partial class QuestReward : Resource
{
	[Export] public Godot.Collections.Array<ResourceCost> Resources = new();
	// Given to every agent on the quest
	[Export] public int Experience;
	[Export] public UnitTemplate JoiningUnit;
	[Export(PropertyHint.Range, "1,20")] public int JoiningUnitLevel = 1;
	[Export] public Godot.Collections.Array<FieldPerk> AddPerks = new();
	// Seconds the added perks last; 0 = for good
	[Export] public int AddPerksSeconds;
	[Export] public Godot.Collections.Array<FieldPerk> RemovePerks = new();
	// One neutral of each listed type leaves the field (e.g. an assassinated sergeant)
	[Export] public Godot.Collections.Array<UnitTemplate> RemoveNeutrals = new();

	// Resources and XP scaled by the quest's level (see QuestDefinition.RewardMultiplier)
	public float ResourceAmount(ResourceCost cost, float multiplier) => Mathf.Round(cost.Amount * multiplier);
	public int ExperienceFor(float multiplier) => Mathf.RoundToInt(Experience * ExperienceBonus * multiplier);

	// Quests pay much more XP than work; every quest's Experience is multiplied by this
	public const float ExperienceBonus = 2f;

	public List<string> Lines(float multiplier)
	{
		var lines = new List<string>();
		var parts = new List<string>();
		foreach (var cost in Resources)
			if (cost != null && cost.Amount > 0)
				parts.Add($"{ResourceAmount(cost, multiplier):0} {cost.Type}");
		if (parts.Count > 0)
			lines.Add(string.Join(", ", parts));
		if (Experience > 0)
			lines.Add($"{ExperienceFor(multiplier)} XP to each agent");
		if (JoiningUnit != null)
			lines.Add($"{JoiningUnit.Name} joins you" + (JoiningUnitLevel > 1 ? $" (level {JoiningUnitLevel})" : ""));
		foreach (var perk in AddPerks)
			if (perk != null)
				lines.Add($"Field gains {perk.Name}" + (AddPerksSeconds > 0 ? $" for {AddPerksSeconds}s" : ""));
		foreach (var perk in RemovePerks)
			if (perk != null)
				lines.Add($"Field loses {perk.Name}");
		foreach (var neutral in RemoveNeutrals)
			if (neutral != null)
				lines.Add($"A {neutral.Name} leaves the field");
		return lines;
	}
}
