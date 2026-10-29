using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public enum QuestState
{
	Available,        // waiting for agents
	Active,           // agents are working on a stage
	WaitingForBattle, // a battle stage: the player has to open the fight
	Succeeded,
	Failed,
	Expired,          // the timer ran out before anyone took it
	Abandoned,
}

// One quest on one field while it is in play: its timer, agents and the stage they are on.
// Ticked by QuestManager once a second; the agents are stuck on the field until it ends.
public class Quest
{
	// Shown in red on the map from this many seconds left
	public const int ExpiringSoonTicks = 30;

	public QuestDefinition Definition { get; }
	public field Field { get; }
	public bool IsStory { get; }
	public QuestState State { get; private set; } = QuestState.Available;
	// Seconds until the quest must be done; only counts when HasDeadline
	public int TicksLeft { get; private set; }
	public bool HasDeadline { get; }
	public int StageIndex { get; private set; }
	// Seconds of work done on the current stage
	public int StageTicks { get; private set; }
	// Seconds until a failed stage is tried again
	public int RetryTicks { get; private set; }
	public List<unit> Agents { get; } = new();
	// The current battle stage's enemies
	public List<NeutralUnit> Enemies { get; } = new();

	// Raised after anything here changes (a tick, a stage, the state)
	public event Action<Quest> Changed;

	private readonly QuestManager _manager;

	public Quest(QuestManager manager, QuestDefinition definition, field place, bool isStory)
	{
		_manager = manager;
		Definition = definition;
		Field = place;
		IsStory = isStory;
		HasDeadline = !isStory && definition.Deadline > 0;
		TicksLeft = definition.Deadline;
	}

	public string Title => Definition.Title;
	public bool IsOpen => State is QuestState.Available or QuestState.Active or QuestState.WaitingForBattle;
	public bool IsStarted => State is QuestState.Active or QuestState.WaitingForBattle;
	public bool IsExpiringSoon => HasDeadline && IsOpen && TicksLeft <= ExpiringSoonTicks;
	public QuestStage CurrentStage => IsStarted && StageIndex < Definition.Stages.Count ? Definition.Stages[StageIndex] : null;

	// "3:20", or "" without a timer
	public string TimeLeftText => HasDeadline ? $"{TicksLeft / 60}:{TicksLeft % 60:00}" : "";

	// The agent that rolls this stage: the one with the best chance
	public unit BestAgent(QuestStage stage, IEnumerable<unit> agents) =>
		agents.Where(GodotObject.IsInstanceValid).OrderByDescending(a => stage.Chance(a.Attributes)).FirstOrDefault();

	// Why this unit can't be sent on the quest, or null when it can
	public string BlockedReason(unit agent)
	{
		if (!GodotObject.IsInstanceValid(agent))
			return "gone";
		if (agent.ActiveQuest != null && agent.ActiveQuest != this)
			return $"on {agent.ActiveQuest.Title}";
		if (agent.currentField != Field || agent.isMoving)
			return "not on this field";
		return Definition.BlockedReason(agent);
	}

	// Why these agents can't start the quest, or null when they can
	public string StartBlockedReason(IReadOnlyCollection<unit> agents)
	{
		if (State != QuestState.Available)
			return "Already taken";
		if (agents.Count < Definition.MinAgents)
			return Definition.MinAgents == 1 ? "Pick an agent" : $"Pick at least {Definition.MinAgents} agents";
		if (agents.Count > Definition.MaxAgents)
			return $"At most {Definition.MaxAgents} agent{(Definition.MaxAgents == 1 ? "" : "s")}";
		foreach (var agent in agents)
		{
			string reason = BlockedReason(agent);
			if (reason != null)
				return $"{agent.Template?.Name ?? agent.Name}: {reason}";
		}
		return null;
	}

	public bool Start(IReadOnlyCollection<unit> agents)
	{
		if (StartBlockedReason(agents) != null)
			return false;
		foreach (var agent in agents)
		{
			agent.abilityRunner?.StopAbility();
			agent.ActiveQuest = this;
			agent.Died += OnAgentDied;
			Agents.Add(agent);
		}
		_manager.Notify(this, $"{AgentNames} started {Title}");
		BeginStage(0);
		return true;
	}

	public void Abandon()
	{
		if (!IsStarted)
			return;
		End(QuestState.Abandoned, $"{Title} was abandoned");
	}

	public void Tick()
	{
		if (!IsOpen)
			return;
		if (HasDeadline && --TicksLeft <= 0)
		{
			TicksLeft = 0;
			if (IsStarted)
				End(QuestState.Failed, $"{Title} failed: time ran out");
			else
				End(QuestState.Expired, $"{Title} in {FieldName} is gone");
			return;
		}
		if (State == QuestState.Active)
		{
			if (RetryTicks > 0)
				RetryTicks--;
			else if (++StageTicks >= CurrentStage.Duration)
				RollStage();
		}
		Changed?.Invoke(this);
	}

	// Battle stages end here: called with the battle's outcome
	public void BattleFinished(bool won)
	{
		if (State != QuestState.WaitingForBattle)
			return;
		State = QuestState.Active;
		var stage = CurrentStage;
		Enemies.Clear();
		if (won)
		{
			_manager.Notify(this, $"{Title}: {stage.Name} won");
			BeginStage(StageIndex + 1);
		}
		else
			StageFailed(stage, null);
	}

	private string FieldName => Field.type?.Name ?? Field.Name;
	private string AgentNames => string.Join(" and ", Agents.Select(a => a.Template?.Name ?? a.Name));

	private void BeginStage(int index)
	{
		StageIndex = index;
		StageTicks = 0;
		RetryTicks = 0;
		if (index >= Definition.Stages.Count)
		{
			Succeed();
			return;
		}
		var stage = Definition.Stages[index];
		if (stage.Kind == QuestStageKind.Battle)
			PrepareBattle(stage);
		else
			State = QuestState.Active;
		Changed?.Invoke(this);
	}

	private void PrepareBattle(QuestStage stage)
	{
		Enemies.Clear();
		foreach (var template in stage.Enemies)
			if (template != null)
				Enemies.Add(new NeutralUnit { Template = template });
		State = QuestState.WaitingForBattle;
		_manager.Notify(this, $"{Title}: fight ready - {stage.Name}");
	}

	private void RollStage()
	{
		var stage = CurrentStage;
		var agent = BestAgent(stage, Agents);
		float chance = stage.Chance(agent?.Attributes);
		if (agent != null && GD.Randf() < chance)
		{
			_manager.Notify(this, $"{Title}: {stage.Name} passed ({chance * 100:0}%)");
			BeginStage(StageIndex + 1);
		}
		else
			StageFailed(stage, agent);
	}

	// `agent` (who rolled; null after a lost battle) takes the stage's damage and may die
	private void StageFailed(QuestStage stage, unit agent)
	{
		string name = agent?.Template?.Name ?? agent?.Name;
		if (agent != null && stage.DeathChance > 0 && GD.Randf() < stage.DeathChance)
		{
			_manager.Notify(this, $"{Title}: {stage.Name} failed - {name} was killed");
			agent.Die(); // OnAgentDied fails the quest when no agent is left
			if (!IsOpen)
				return;
		}
		else if (agent != null && stage.FailDamage > 0)
		{
			agent.Injure(stage.FailDamage);
			_manager.Notify(this, $"{Title}: {stage.Name} failed - {name} took {stage.FailDamage} damage");
		}
		else
			_manager.Notify(this, $"{Title}: {stage.Name} failed");

		switch (stage.OnFail)
		{
			case QuestStageFailure.Retry:
				StageTicks = 0;
				RetryTicks = stage.RetryDelay;
				if (stage.Kind == QuestStageKind.Battle)
					PrepareBattle(stage);
				Changed?.Invoke(this);
				break;
			case QuestStageFailure.InjureContinue:
				BeginStage(StageIndex + 1);
				break;
			default:
				End(QuestState.Failed, $"{Title} failed");
				break;
		}
	}

	private void Succeed()
	{
		var agents = Agents.Where(GodotObject.IsInstanceValid).ToList();
		End(QuestState.Succeeded, $"{Title} done!");
		_manager.GiveReward(this, agents);
	}

	private void OnAgentDied(unit agent)
	{
		agent.Died -= OnAgentDied;
		Agents.Remove(agent);
		if (IsStarted && Agents.Count == 0)
			End(QuestState.Failed, $"{Title} failed: no agent is left");
		else
			Changed?.Invoke(this);
	}

	private void End(QuestState state, string message)
	{
		State = state;
		Enemies.Clear();
		foreach (var agent in Agents)
		{
			if (!GodotObject.IsInstanceValid(agent))
				continue;
			agent.Died -= OnAgentDied;
			if (agent.ActiveQuest == this)
				agent.ActiveQuest = null;
		}
		Agents.Clear();
		_manager.Notify(this, message);
		Changed?.Invoke(this);
	}
}
