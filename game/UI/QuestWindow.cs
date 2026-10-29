using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Centred window for one quest: story, gates, stages with each one's odds, rewards, and the
// agents. Pick agents standing on the field and press Start; while it runs, Abandon frees them
// and Fight! opens a battle stage. Opened from the field panel or a map marker. Built in code.
public partial class QuestWindow : PanelContainer
{
	private const float TextWidth = 470;

	private static readonly Color BrightGold = new Color(1f, 0.82f, 0.4f);
	private static readonly Color Good = new Color(0.55f, 1f, 0.55f);
	private static readonly Color Bad = new Color(1f, 0.5f, 0.45f);
	private static readonly Color Current = new Color(0.55f, 0.78f, 1f);

	// The player pressed Fight! on a battle stage
	public event Action<Quest> FightRequested;

	private Label _title;
	private Label _subtitle;
	private VBoxContainer _content;
	private Label _status;
	private Button _startButton;
	private Button _abandonButton;
	private Button _fightButton;

	private Quest _quest;
	// Agents ticked for a quest that hasn't started yet
	private readonly HashSet<unit> _picked = new();
	private bool _refreshQueued;

	public override void _Ready()
	{
		Visible = false;
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		AddChild(box);

		var header = new HBoxContainer();
		_title = new Label { ThemeTypeVariation = "HeaderLabel", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		header.AddChild(_title);
		var close = new Button { Text = "Close", FocusMode = FocusModeEnum.None };
		close.Pressed += Close;
		header.AddChild(close);
		box.AddChild(header);
		_subtitle = new Label { ThemeTypeVariation = "MutedLabel" };
		box.AddChild(_subtitle);
		box.AddChild(new HSeparator());

		_content = new VBoxContainer();
		_content.AddThemeConstantOverride("separation", 6);
		box.AddChild(_content);

		box.AddChild(new HSeparator());
		var footer = new HBoxContainer();
		footer.AddThemeConstantOverride("separation", 8);
		_status = new Label
		{
			ThemeTypeVariation = "MutedLabel",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(220, 0),
		};
		footer.AddChild(_status);
		_startButton = MakeButton("Start", StartQuest);
		_fightButton = MakeButton("Fight!", () => FightRequested?.Invoke(_quest));
		_abandonButton = MakeButton("Abandon", () => _quest?.Abandon());
		footer.AddChild(_fightButton);
		footer.AddChild(_startButton);
		footer.AddChild(_abandonButton);
		box.AddChild(footer);
	}

	private static Button MakeButton(string text, Action pressed)
	{
		var button = new Button { Text = text, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(110, 36) };
		button.Pressed += pressed;
		return button;
	}

	public override void _ExitTree() => Unsubscribe();

	public Quest Shown => Visible ? _quest : null;

	public void Open(Quest quest)
	{
		if (quest == null)
		{
			Close();
			return;
		}
		if (quest != _quest)
		{
			Unsubscribe();
			_quest = quest;
			_picked.Clear();
			_quest.Changed += OnQuestChanged;
			_quest.Field.ContentsChanged += QueueRefresh;
			// One agent standing there is picked for you when the quest takes only one
			var ready = _quest.Field.presentPlayerUnits.Where(u => _quest.BlockedReason(u) == null).ToList();
			if (_quest.Definition.MaxAgents == 1 && ready.Count > 0)
				_picked.Add(BestOverall(ready));
		}
		Refresh();
		Visible = true;
	}

	public void Close()
	{
		Visible = false;
		Unsubscribe();
		_quest = null;
		_picked.Clear();
	}

	private void Unsubscribe()
	{
		if (_quest == null)
			return;
		_quest.Changed -= OnQuestChanged;
		if (IsInstanceValid(_quest.Field))
			_quest.Field.ContentsChanged -= QueueRefresh;
	}

	private void OnQuestChanged(Quest quest) => QueueRefresh();

	// Rebuilding from inside a checkbox's signal would free the checkbox mid-signal
	private void QueueRefresh()
	{
		if (_refreshQueued)
			return;
		_refreshQueued = true;
		CallDeferred(MethodName.Refresh);
	}

	private unit BestOverall(IEnumerable<unit> agents) =>
		agents.OrderByDescending(a => _quest.Definition.Stages.Where(s => s.Kind == QuestStageKind.Roll).Sum(s => s.Chance(a.Attributes))).First();

	// Whose odds the stages show: the agents on the quest, else the picked ones, else everyone who could go
	private List<unit> OddsAgents()
	{
		if (_quest.IsStarted)
			return _quest.Agents.Where(IsInstanceValid).ToList();
		var picked = _picked.Where(IsInstanceValid).ToList();
		if (picked.Count > 0)
			return picked;
		return _quest.Field.presentPlayerUnits.Where(u => _quest.BlockedReason(u) == null).ToList();
	}

	private void Refresh()
	{
		_refreshQueued = false;
		if (_quest == null)
			return;
		_picked.RemoveWhere(u => !IsInstanceValid(u) || _quest.BlockedReason(u) != null);
		var definition = _quest.Definition;
		_title.Text = _quest.Title;
		string place = _quest.Field.type?.Name ?? _quest.Field.Name;
		string time = _quest.HasDeadline ? $"{_quest.TimeLeftText} left" : _quest.IsStory ? "story quest, no timer" : "no timer";
		_subtitle.Text = $"Level {definition.Level}  ·  {place}  ·  {time}  ·  {definition.GatesText()}";

		UIText.ClearChildren(_content);
		if (!string.IsNullOrEmpty(definition.Story))
			_content.AddChild(Wrapped(definition.Story));

		_content.AddChild(new Label { Text = "Stages", ThemeTypeVariation = "HeaderLabel" });
		var oddsAgents = OddsAgents();
		for (int i = 0; i < definition.Stages.Count; i++)
			AddStage(i, definition.Stages[i], oddsAgents);

		if (definition.Reward != null)
		{
			_content.AddChild(new Label { Text = "Reward", ThemeTypeVariation = "HeaderLabel" });
			var reward = Wrapped(string.Join("\n", definition.Reward.Lines(definition.RewardMultiplier)));
			reward.AddThemeColorOverride("font_color", BrightGold);
			_content.AddChild(reward);
		}

		_content.AddChild(new Label { Text = "Agents", ThemeTypeVariation = "HeaderLabel" });
		if (_quest.IsStarted)
			foreach (var agent in _quest.Agents.Where(IsInstanceValid))
				_content.AddChild(new Label { Text = AgentLine(agent) });
		else if (_quest.IsOpen)
			AddAgentPicker();
		RefreshFooter();
		ResetSize();
		SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.KeepSize);
	}

	private void AddStage(int index, QuestStage stage, List<unit> agents)
	{
		bool done = _quest.IsStarted && index < _quest.StageIndex || _quest.State == QuestState.Succeeded;
		bool current = _quest.IsStarted && index == _quest.StageIndex;
		var row = new HBoxContainer();
		var name = new Label { Text = $"{index + 1}. {stage.Name}", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		if (current)
			name.AddThemeColorOverride("font_color", Current);
		else if (done)
			name.Modulate = new Color(1, 1, 1, 0.5f);
		row.AddChild(name);
		row.AddChild(new Label { Text = stage.CheckText(), ThemeTypeVariation = "MutedLabel" });
		var odds = new Label { CustomMinimumSize = new Vector2(150, 0), HorizontalAlignment = HorizontalAlignment.Right };
		if (done)
			odds.Text = "done";
		else if (stage.Kind == QuestStageKind.Battle)
			odds.Text = string.Join(", ", stage.Enemies.Where(e => e != null).GroupBy(e => e.Name).Select(g => g.Count() > 1 ? $"{g.Count()} {g.Key}" : g.Key));
		else
		{
			var best = _quest.BestAgent(stage, agents);
			float chance = stage.Chance(best?.Attributes);
			odds.Text = best != null ? $"{best.Template?.Name ?? best.Name} {chance * 100:0}%" : "no agent";
			odds.AddThemeColorOverride("font_color", chance >= 0.65f ? Good : chance < 0.4f ? Bad : BrightGold);
		}
		row.AddChild(odds);
		_content.AddChild(row);

		string details = stage.Description;
		if (!done)
			details = (string.IsNullOrEmpty(details) ? "" : details + "  ") + stage.FailureText();
		if (!string.IsNullOrEmpty(details))
		{
			var label = Wrapped(details);
			label.ThemeTypeVariation = "MutedLabel";
			_content.AddChild(label);
		}

		if (current && stage.Kind == QuestStageKind.Roll)
		{
			bool retrying = _quest.RetryTicks > 0;
			var bar = new ProgressBar
			{
				MaxValue = retrying ? stage.RetryDelay : stage.Duration,
				Value = retrying ? stage.RetryDelay - _quest.RetryTicks : _quest.StageTicks,
				ShowPercentage = false,
				CustomMinimumSize = new Vector2(0, 8),
			};
			_content.AddChild(bar);
			_content.AddChild(new Label
			{
				Text = retrying ? $"Trying again in {_quest.RetryTicks}s" : $"Working... {_quest.StageTicks}/{stage.Duration}s",
				ThemeTypeVariation = "MutedLabel",
			});
		}
		else if (current && _quest.State == QuestState.WaitingForBattle)
		{
			var label = new Label { Text = "The enemy is here - press Fight! when you are ready" };
			label.AddThemeColorOverride("font_color", Bad);
			_content.AddChild(label);
		}
	}

	private void AddAgentPicker()
	{
		var units = _quest.Field.presentPlayerUnits.Where(IsInstanceValid).ToList();
		if (units.Count == 0)
		{
			_content.AddChild(Wrapped("None of your units stand here. Move agents onto this field to take the quest."));
			return;
		}
		foreach (var agent in units)
		{
			string reason = _quest.BlockedReason(agent);
			var check = new CheckBox
			{
				Text = AgentLine(agent) + (reason != null ? $"  ({reason})" : ""),
				ButtonPressed = _picked.Contains(agent),
				Disabled = reason != null,
				FocusMode = FocusModeEnum.None,
			};
			var picked = agent;
			check.Toggled += on =>
			{
				if (on)
				{
					// A one-agent quest swaps the pick instead of ticking a second one
					if (_quest.Definition.MaxAgents == 1)
						_picked.Clear();
					_picked.Add(picked);
				}
				else
					_picked.Remove(picked);
				QueueRefresh();
			};
			_content.AddChild(check);
		}
	}

	private static string AgentLine(unit agent)
	{
		var stats = agent.battleStats;
		return $"{agent.Template?.Name ?? agent.Name}  Lv {agent.Progression.Level}  HP {stats?.health}/{stats?.maxHealth}   {UIText.Attributes(agent.Attributes)}";
	}

	private void RefreshFooter()
	{
		bool available = _quest.State == QuestState.Available;
		_startButton.Visible = available;
		_abandonButton.Visible = _quest.IsStarted;
		_fightButton.Visible = _quest.State == QuestState.WaitingForBattle;
		string reason = available ? _quest.StartBlockedReason(_picked) : null;
		_startButton.Disabled = reason != null;
		_status.Text = _quest.State switch
		{
			QuestState.Available => reason ?? "Ready - the agents stay here until the quest ends",
			QuestState.Active => _quest.RetryTicks > 0 ? "Recovering from a failed attempt" : "In progress - Abandon frees the agents, the quest is lost",
			QuestState.WaitingForBattle => "A fight is waiting",
			QuestState.Succeeded => "Quest complete!",
			QuestState.Failed => "Quest failed",
			QuestState.Expired => "Too late - the chance is gone",
			QuestState.Abandoned => "Abandoned",
			_ => "",
		};
	}

	private void StartQuest()
	{
		if (_quest != null && _quest.Start(_picked.ToList()))
			_picked.Clear();
	}

	private static Label Wrapped(string text) => new Label
	{
		Text = text,
		AutowrapMode = TextServer.AutowrapMode.WordSmart,
		CustomMinimumSize = new Vector2(TextWidth, 0),
	};
}
