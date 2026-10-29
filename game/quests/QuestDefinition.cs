using Godot;
using System.Collections.Generic;
using System.Linq;

// A job for agents, e.g. "Steal the Ring". Random quests appear on explored fields of its Biomes
// (see QuestManager); map files can also place one on a field as a story quest (no timer).
// Level 1-5 sets how many stages it has (level 1: one roll, level 3+: battles allowed), when it
// starts to appear and how much its reward is worth. Files live in res://resources/quests.
[GlobalClass]
public partial class QuestDefinition : Resource
{
	[Export] public string Title = "Quest";
	[Export(PropertyHint.MultilineText)] public string Story = "";
	[Export(PropertyHint.Range, "1,5")] public int Level = 1;
	// Where it can appear at random (Forest = spring and autumn forests, see Biomes.Of)
	[Export] public Godot.Collections.Array<Biome> Biomes = new();
	// Seconds from appearing until it must be finished; 0 = no timer
	[Export] public int Deadline = 240;
	// How likely it is picked when a random quest appears; 0 = story quests only
	[Export] public float SpawnWeight = 1;

	// Gates: every agent must meet them
	[Export(PropertyHint.Range, "1,20")] public int MinLevel = 1;
	[Export] public SpecialAttributes MinAttributes;
	[Export(PropertyHint.Range, "1,5")] public int MinAgents = 1;
	[Export(PropertyHint.Range, "1,5")] public int MaxAgents = 1;

	[Export] public Godot.Collections.Array<QuestStage> Stages = new();
	[Export] public QuestReward Reward;

	public const int MaxLevel = 5;
	// Reward resources and XP are worth this much more per level above 1 (level 5 = 3x)
	public const float RewardPerLevel = 0.5f;
	public float RewardMultiplier => 1 + RewardPerLevel * (Level - 1);

	public bool FitsField(field place) => place?.TypeName != null && Biomes.Contains(global::Biomes.Of(place.TypeName));

	// Why this unit can't take the quest, or null when it can
	public string BlockedReason(unit agent)
	{
		if (agent.Progression.Level < MinLevel)
			return $"needs level {MinLevel}";
		if (MinAttributes != null)
			for (int i = 0; i < SpecialAttributes.Count; i++)
				if (agent.Attributes.Get(i) < MinAttributes.Get(i))
					return $"needs {SpecialAttributes.ShortNames[i]} {MinAttributes.Get(i)}";
		return null;
	}

	// e.g. "Level 2, DEA 4+, 1-3 agents"
	public string GatesText()
	{
		var parts = new List<string>();
		if (MinLevel > 1)
			parts.Add($"level {MinLevel}+");
		if (MinAttributes != null)
			for (int i = 0; i < SpecialAttributes.Count; i++)
				if (MinAttributes.Get(i) > 0)
					parts.Add($"{SpecialAttributes.ShortNames[i]} {MinAttributes.Get(i)}+");
		parts.Add(MinAgents == MaxAgents
			? (MinAgents == 1 ? "1 agent" : $"{MinAgents} agents")
			: $"{MinAgents}-{MaxAgents} agents");
		return string.Join(", ", parts);
	}
}
