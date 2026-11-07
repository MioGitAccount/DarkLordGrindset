using Godot;
using System;
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
	// An item for the inventory: this one (id from ItemCatalog), else a random one of ItemTier (0 = none)
	[Export] public string ItemId = "";
	[Export(PropertyHint.Range, "0,3")] public int ItemTier;

	public ItemDefinition Item => ItemCatalog.Get(ItemId);
	public bool GivesItem => Item != null || ItemTier > 0;

	// Resources and XP scaled by the quest's level (see QuestDefinition.RewardMultiplier)
	public float ResourceAmount(ResourceCost cost, float multiplier) => Mathf.Round(cost.Amount * multiplier);
	public int ExperienceFor(float multiplier) => Mathf.RoundToInt(Experience * ExperienceBonus * multiplier);
	// The XP is paid in equal parts, one for each stage passed (a stage failed and skipped pays nothing)
	public int ExperiencePerStage(float multiplier, int stages) => Mathf.RoundToInt((float)ExperienceFor(multiplier) / Math.Max(1, stages));

	// Quests pay much more XP than work, and keep the agents busy for minutes (a roll stage takes 90-120s, see tools/quests/make_quests.py);
	// every quest's Experience is multiplied by this
	public const float ExperienceBonus = 10f;

	public List<string> Lines(float multiplier, int stages = 1)
	{
		var lines = new List<string>();
		var parts = new List<string>();
		foreach (var cost in Resources)
			if (cost != null && cost.Amount > 0)
				parts.Add($"{ResourceAmount(cost, multiplier):0} {cost.Type}");
		if (parts.Count > 0)
			lines.Add(string.Join(", ", parts));
		if (Experience > 0)
			lines.Add(stages > 1
				? $"{ExperiencePerStage(multiplier, stages) * stages} XP to each agent ({ExperiencePerStage(multiplier, stages)} per stage)"
				: $"{ExperienceFor(multiplier)} XP to each agent");
		if (Item != null)
			lines.Add($"Item: {Item.Name} ({Item.StatsText()})");
		else if (ItemTier > 0)
			lines.Add($"A random tier {ItemTier} item");
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
