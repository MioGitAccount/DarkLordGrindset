using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Every quest in play. Random quests appear every 75-105 seconds on explored fields that fit them
// (at most MaxRandomQuests waiting at once, one quest per field), up to one level above the
// player's best agent, near the castle: a quest of level L appears at most L + 5 steps away, and
// closer fields are picked more often. Story quests come from the map file. Ticks the quests and
// hands out rewards. Created by the map; the UI listens to the events.
public partial class QuestManager : Node
{
	public const int FirstSpawnDelay = 30;
	public const int MinSpawnInterval = 75;
	public const int MaxSpawnInterval = 105;
	public const int MaxRandomQuests = 3;
	// Steps from the castle a quest of level L may appear at: L + this (level 1 = 6, level 5 = 10)
	public const int ExtraStepsPerLevel = 5;
	// How much less likely each step away from the castle is (weight / (1 + steps * this))
	public const float DistanceFalloff = 0.5f;

	// Set by the map: the player's castle field; random quests appear around it
	public field Home;

	private readonly List<Quest> _quests = new();
	private List<QuestDefinition> _definitions;
	private IEnumerable<field> _fields = Array.Empty<field>();
	private int _ticksToSpawn = FirstSpawnDelay;

	// Open quests (available or in progress)
	public IReadOnlyList<Quest> Quests => _quests;

	public event Action<Quest> QuestAdded;
	// Raised once a quest has ended (done, failed, expired, abandoned) and left the list
	public event Action<Quest> QuestRemoved;
	public event Action<Quest> QuestChanged;
	// A line for the player, e.g. "Steal the Ring: Lift it passed (64%)"
	public event Action<Quest, string> Notified;

	// Set by the map: puts a unit that joined (a quest reward) on a field
	public Func<UnitTemplate, field, int, unit> SpawnUnit;

	public override void _Ready()
	{
		_definitions = MapCatalog.QuestNames.Select(MapCatalog.LoadQuest).Where(d => d != null).ToList();
		GlobalTickManager.Instance.OnTick += Tick;
	}

	public override void _ExitTree()
	{
		if (GlobalTickManager.Instance != null)
			GlobalTickManager.Instance.OnTick -= Tick;
	}

	public void SetFields(IEnumerable<field> fields) => _fields = fields;

	public IEnumerable<Quest> QuestsOn(field place) => _quests.Where(q => q.Field == place);

	// Highest level of random quest that can appear: one above the player's best agent
	public static int MaxSpawnLevel(IEnumerable<unit> agents) =>
		Math.Min(QuestDefinition.MaxLevel, 1 + agents.Where(GodotObject.IsInstanceValid).Select(a => a.Progression.Level).DefaultIfEmpty(0).Max());

	// Set by the map: the player's units, for MaxSpawnLevel
	public Func<IEnumerable<unit>> PlayerUnits = () => Array.Empty<unit>();

	// A quest placed on a field by the map file
	public Quest AddStoryQuest(QuestDefinition definition, field place) => Add(definition, place, isStory: true);

	private Quest Add(QuestDefinition definition, field place, bool isStory)
	{
		var quest = new Quest(this, definition, place, isStory);
		quest.Changed += OnQuestChanged;
		_quests.Add(quest);
		QuestAdded?.Invoke(quest);
		place.NotifyContentsChanged();
		return quest;
	}

	private void OnQuestChanged(Quest quest)
	{
		QuestChanged?.Invoke(quest);
		if (quest.IsOpen || !_quests.Remove(quest))
			return;
		quest.Changed -= OnQuestChanged;
		QuestRemoved?.Invoke(quest);
		quest.Field.NotifyContentsChanged();
	}

	public void Notify(Quest quest, string message)
	{
		GD.Print($"[Quest] {message}");
		Notified?.Invoke(quest, message);
	}

	private void Tick()
	{
		foreach (var quest in _quests.ToList())
			quest.Tick();
		if (--_ticksToSpawn > 0)
			return;
		_ticksToSpawn = (int)GD.RandRange(MinSpawnInterval, MaxSpawnInterval);
		TrySpawnRandom();
	}

	// Steps from Home to every field it connects to (Home = 0); empty without a Home
	public Dictionary<field, int> StepsFromHome()
	{
		var steps = new Dictionary<field, int>();
		if (!GodotObject.IsInstanceValid(Home))
			return steps;
		var queue = new Queue<field>();
		steps[Home] = 0;
		queue.Enqueue(Home);
		while (queue.Count > 0)
		{
			var current = queue.Dequeue();
			foreach (var next in current.Neighbors)
				if (next != null && !steps.ContainsKey(next))
				{
					steps[next] = steps[current] + 1;
					queue.Enqueue(next);
				}
		}
		return steps;
	}

	// Picks a fitting (quest, field) pair at random, weighted by the quest's SpawnWeight and
	// by how close the field is to the castle
	public Quest TrySpawnRandom()
	{
		if (_quests.Count(q => !q.IsStory && q.State == QuestState.Available) >= MaxRandomQuests)
			return null;
		var taken = _quests.Select(q => q.Field).ToHashSet();
		int maxLevel = MaxSpawnLevel(PlayerUnits());
		var stepsFromHome = StepsFromHome();
		var options = new List<(QuestDefinition, field, float)>();
		foreach (var place in _fields)
		{
			if (!GodotObject.IsInstanceValid(place) || place.Fog == FogState.Unexplored || taken.Contains(place))
				continue;
			// Without a castle (or off its network) the distance rule doesn't apply
			int steps = stepsFromHome.Count == 0 ? 0 : stepsFromHome.TryGetValue(place, out int s) ? s : int.MaxValue;
			foreach (var definition in _definitions)
				if (definition.SpawnWeight > 0 && definition.Level <= maxLevel && steps <= definition.Level + ExtraStepsPerLevel
					&& definition.FitsField(place))
					options.Add((definition, place, definition.SpawnWeight / (1 + steps * DistanceFalloff)));
		}
		if (options.Count == 0)
			return null;
		float pick = GD.Randf() * options.Sum(o => o.Item3);
		foreach (var (definition, place, weight) in options)
		{
			pick -= weight;
			if (pick <= 0)
			{
				var quest = Add(definition, place, isStory: false);
				Notify(quest, $"New quest in {place.type?.Name ?? place.Name}: {quest.Title}");
				return quest;
			}
		}
		return null;
	}

	// Called by a quest that succeeded, with the agents that finished it
	public void GiveReward(Quest quest, List<unit> agents)
	{
		var reward = quest.Definition.Reward;
		if (reward == null)
			return;
		float multiplier = quest.Definition.RewardMultiplier;
		foreach (var cost in reward.Resources)
			if (cost != null)
				PlayerResources.Add(cost.Type, reward.ResourceAmount(cost, multiplier));
		if (reward.Experience > 0)
			foreach (var agent in agents)
				agent.AddExperience(reward.ExperienceFor(multiplier));
		var place = quest.Field;
		foreach (var perk in reward.AddPerks)
		{
			if (perk == null)
				continue;
			if (reward.AddPerksSeconds > 0)
				place.AddTimedPerk(perk, reward.AddPerksSeconds);
			else
				place.AddPerk(perk);
		}
		foreach (var perk in reward.RemovePerks)
			if (perk != null)
				place.RemovePerk(perk);
		foreach (var template in reward.RemoveNeutrals)
			if (template != null)
				place.RemoveNeutralOfType(template);
		if (reward.JoiningUnit != null && SpawnUnit != null)
		{
			SpawnUnit(reward.JoiningUnit, place, reward.JoiningUnitLevel);
			Notify(quest, $"{reward.JoiningUnit.Name} joined you");
		}
		place.NotifyContentsChanged();
	}
}
