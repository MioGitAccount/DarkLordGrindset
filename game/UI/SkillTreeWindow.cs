using Godot;
using System.Collections.Generic;
using System;
using System.Linq;

// Centred window showing a unit's skill tree: tier 1 at the bottom up to tier 5 at the top,
// lines from each skill down to the skills it requires. Click a skill to see its details,
// then "Learn" to spend skill points on it. Built in code, like FieldHoverCard.
public partial class SkillTreeWindow : PanelContainer
{
	private static readonly Vector2 FullNodeSize = new Vector2(150, 46);
	private const float FullColumnGap = 26;
	// Wide trees shrink their nodes down to this to fit the screen
	private const float MinNodeWidth = 100;
	private const float NarrowColumnGap = 12;
	// Room the window's frame and padding take next to the canvas
	private const float WindowMargin = 60;
	private const float RowGap = 26;
	private const float TierLabelWidth = 70;

	private static readonly Color Gold = new Color(0.85f, 0.65f, 0.3f);
	private static readonly Color BrightGold = new Color(1f, 0.82f, 0.4f);
	private static readonly Color LockedGrey = new Color(0.35f, 0.33f, 0.33f);

	private Label _title;
	private Label _progressLabel;
	private Control _canvas;
	private Label _detailName;
	private Label _detailInfo;
	private Label _detailEffects;
	private Label _detailStatus;
	private Button _learnButton;

	private unit _unit;
	private SkillDefinition _selected;
	private readonly Dictionary<SkillDefinition, Button> _nodes = new();
	private Vector2 _nodeSize = FullNodeSize;
	private float _columnGap = FullColumnGap;
	private int _shownRevision = -1;

	public override void _Ready()
	{
		Visible = false;

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 10);
		AddChild(box);

		var header = new HBoxContainer();
		_title = new Label { ThemeTypeVariation = "HeaderLabel", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		header.AddChild(_title);
		if (OS.IsDebugBuild())
		{
			// Testing aid until battles hand out experience; not shown in exported release builds
			var debugXp = new Button { Text = "+100 XP (debug)", FocusMode = FocusModeEnum.None };
			debugXp.Pressed += () => _unit?.AddExperience(100);
			header.AddChild(debugXp);
		}
		var close = new Button { Text = "Close", FocusMode = FocusModeEnum.None };
		close.Pressed += Close;
		header.AddChild(close);
		box.AddChild(header);

		_progressLabel = new Label { ThemeTypeVariation = "MutedLabel" };
		box.AddChild(_progressLabel);
		box.AddChild(new HSeparator());

		_canvas = new Control { MouseFilter = MouseFilterEnum.Pass };
		_canvas.Draw += DrawTree;
		box.AddChild(_canvas);

		box.AddChild(new HSeparator());
		var details = new HBoxContainer();
		details.AddThemeConstantOverride("separation", 12);
		box.AddChild(details);
		var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		details.AddChild(text);
		_detailName = new Label { ThemeTypeVariation = "HeaderLabel" };
		// Wrapping labels need a width, or they wrap after every word and the window grows very tall
		_detailInfo = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(380, 0) };
		_detailEffects = new Label();
		_detailEffects.AddThemeColorOverride("font_color", BrightGold);
		_detailStatus = new Label { ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(380, 0) };
		text.AddChild(_detailName);
		text.AddChild(_detailInfo);
		text.AddChild(_detailEffects);
		text.AddChild(_detailStatus);
		_learnButton = new Button { Text = "Learn", FocusMode = FocusModeEnum.None, SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(110, 36) };
		_learnButton.Pressed += LearnSelected;
		details.AddChild(_learnButton);
	}

	public void Open(unit shownUnit)
	{
		if (!IsInstanceValid(shownUnit) || shownUnit.SkillTree == null)
		{
			Close();
			return;
		}
		if (shownUnit != _unit)
			_selected = null;
		_unit = shownUnit;
		_title.Text = $"{_unit.Template?.Name ?? _unit.Name} - {_unit.SkillTree.Name} skill tree";
		BuildTree();
		Refresh();
		Visible = true;
		ResetSize();
		SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.KeepSize);
	}

	public void Close()
	{
		Visible = false;
		_unit = null;
		_selected = null;
	}

	public override void _Process(double delta)
	{
		if (!Visible)
			return;
		if (!IsInstanceValid(_unit))
		{
			Close();
			return;
		}
		// Level ups and learned skills bump the unit's revision
		if (_unit.Revision != _shownRevision)
			Refresh();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Visible && @event.IsActionPressed("ui_cancel"))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private void BuildTree()
	{
		UIText.ClearChildren(_canvas);
		_nodes.Clear();

		var skills = _unit.SkillTree.Skills.Where(s => s != null).ToList();
		int columns = skills.Count == 0 ? 1 : skills.Max(s => s.Column) + 1;
		int tiers = SkillDefinition.MaxTier;
		FitNodes(columns);
		_canvas.CustomMinimumSize = new Vector2(
			TierLabelWidth + columns * _nodeSize.X + (columns - 1) * _columnGap,
			tiers * _nodeSize.Y + (tiers - 1) * RowGap);

		// Tier labels down the left side: tier 5 at the top
		for (int tier = SkillDefinition.MinTier; tier <= tiers; tier++)
		{
			var label = new Label
			{
				Text = $"Tier {tier}\nLv {1 + (tier - 1) * 2}+",
				ThemeTypeVariation = "MutedLabel",
				Position = new Vector2(0, RowY(tier)),
				Size = new Vector2(TierLabelWidth - 8, _nodeSize.Y),
				VerticalAlignment = VerticalAlignment.Center,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			_canvas.AddChild(label);
		}

		foreach (var skill in skills)
		{
			var button = new Button
			{
				Position = NodePosition(skill),
				Size = _nodeSize,
				CustomMinimumSize = _nodeSize,
				FocusMode = FocusModeEnum.None,
				ClipText = true,
				TooltipText = skill.Description,
			};
			var labels = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
			labels.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			labels.AddThemeConstantOverride("separation", 0);
			var nameLabel = new Label { Text = skill.Name, HorizontalAlignment = HorizontalAlignment.Center, ClipText = true, MouseFilter = MouseFilterEnum.Ignore, Name = "Name" };
			var effectLabel = new Label
			{
				Text = string.Join(", ", skill.EffectLines().Select(ShortEffect)),
				HorizontalAlignment = HorizontalAlignment.Center,
				ThemeTypeVariation = "MutedLabel",
				TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			if (Narrow)
				effectLabel.AddThemeFontSizeOverride("font_size", 11);
			labels.AddChild(nameLabel);
			labels.AddChild(effectLabel);
			button.AddChild(labels);
			var clicked = skill;
			button.Pressed += () => Select(clicked);
			_canvas.AddChild(button);
			FitName(nameLabel, effectLabel);
			_nodes[skill] = button;
		}
	}

	// "+1 Deadliness" -> "+1 DEA", "Unlocks ability: Cut" -> "Cut" to fit on a tree node
	private static string ShortEffect(string line)
	{
		for (int i = 0; i < SpecialAttributes.Count; i++)
			line = line.Replace(SpecialAttributes.Names[i], SpecialAttributes.ShortNames[i]);
		return line.Replace("Unlocks ability: ", "").Replace("Battle passive: ", "").Replace("Battle ability: ", "").Replace(" range", "");
	}

	// Full-size nodes when they fit on screen, otherwise narrower nodes and gaps
	private void FitNodes(int columns)
	{
		_nodeSize = FullNodeSize;
		_columnGap = FullColumnGap;
		float available = GetViewportRect().Size.X - WindowMargin - TierLabelWidth;
		if (columns * FullNodeSize.X + (columns - 1) * FullColumnGap <= available)
			return;
		_columnGap = NarrowColumnGap;
		float width = Mathf.Floor((available - (columns - 1) * _columnGap) / columns);
		_nodeSize = new Vector2(Mathf.Clamp(width, MinNodeWidth, FullNodeSize.X), FullNodeSize.Y);
	}

	private bool Narrow => _nodeSize.X < FullNodeSize.X;

	// Makes the font of a long skill name smaller until it fits on its node (the label must be in the tree).
	// A name too long even then takes two lines, in place of the effect line.
	private void FitName(Label label, Label effectLabel)
	{
		const int minSize = 11;
		var font = label.GetThemeFont("font");
		int size = label.GetThemeFontSize("font_size");
		if (Narrow)
			size = Math.Min(size, 13);
		float room = _nodeSize.X - 16;
		while (size > minSize && font.GetStringSize(label.Text, HorizontalAlignment.Left, -1, size).X > room)
			size--;
		if (font.GetStringSize(label.Text, HorizontalAlignment.Left, -1, size).X > room)
		{
			label.ClipText = false;
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			effectLabel.Visible = false;
		}
		label.AddThemeFontSizeOverride("font_size", size);
	}

	private float RowY(int tier) => (SkillDefinition.MaxTier - tier) * (_nodeSize.Y + RowGap);

	private Vector2 NodePosition(SkillDefinition skill)
	{
		int tier = Mathf.Clamp(skill.Tier, SkillDefinition.MinTier, SkillDefinition.MaxTier);
		return new Vector2(TierLabelWidth + skill.Column * (_nodeSize.X + _columnGap), RowY(tier));
	}

	private void DrawTree()
	{
		if (_unit == null)
			return;
		// Faint band behind each tier row
		for (int tier = SkillDefinition.MinTier; tier <= SkillDefinition.MaxTier; tier++)
		{
			var band = new Rect2(TierLabelWidth - 6, RowY(tier) - 4, _canvas.Size.X - TierLabelWidth + 6, _nodeSize.Y + 8);
			bool reached = _unit.Progression.Level >= 1 + (tier - 1) * 2;
			_canvas.DrawRect(band, new Color(1, 1, 1, reached ? 0.04f : 0.015f));
		}
		// Requirement lines: from the top of the required skill to the bottom of the skill needing it,
		// turning in the gap right above the required skill so they don't run behind other skills
		foreach (var (skill, button) in _nodes)
		{
			foreach (var required in skill.Requires)
			{
				if (required == null || !_nodes.TryGetValue(required, out var requiredButton))
					continue;
				var from = requiredButton.Position + new Vector2(_nodeSize.X / 2, 0);
				var to = button.Position + new Vector2(_nodeSize.X / 2, _nodeSize.Y);
				bool active = _unit.Progression.IsUnlocked(required);
				var color = active ? Gold : LockedGrey;
				float midY = from.Y - RowGap / 2;
				_canvas.DrawPolyline(new[] { from, new Vector2(from.X, midY), new Vector2(to.X, midY), to }, color, active ? 3f : 2f, true);
			}
		}
	}

	private void Refresh()
	{
		_shownRevision = _unit.Revision;
		var progression = _unit.Progression;
		string xp = progression.IsMaxLevel
			? "max level"
			: $"XP {progression.Experience}/{UnitProgression.ExperienceToNextLevel(progression.Level)}";
		_progressLabel.Text = $"Level {progression.Level}   {xp}   Skill points: {progression.SkillPoints}";

		foreach (var (skill, button) in _nodes)
			StyleNode(button, progression.GetStatus(skill), skill == _selected);
		_canvas.QueueRedraw();
		ShowDetails();
	}

	private void StyleNode(Button button, SkillStatus status, bool selected)
	{
		(Color bg, Color border, Color text) = status switch
		{
			SkillStatus.Unlocked => (new Color(0.38f, 0.16f, 0.1f), BrightGold, new Color(1f, 0.92f, 0.75f)),
			SkillStatus.Available => (new Color(0.16f, 0.13f, 0.12f), BrightGold, new Color(1f, 0.92f, 0.75f)),
			SkillStatus.NoPoints => (new Color(0.13f, 0.11f, 0.11f), Gold.Darkened(0.4f), new Color(0.8f, 0.75f, 0.68f)),
			_ => (new Color(0.09f, 0.085f, 0.09f), LockedGrey, new Color(0.5f, 0.48f, 0.46f)),
		};
		var style = new StyleBoxFlat { BgColor = bg, BorderColor = selected ? Colors.White : border };
		style.SetBorderWidthAll(selected ? 3 : status == SkillStatus.Available ? 2 : 1);
		style.SetCornerRadiusAll(5);
		var hover = (StyleBoxFlat)style.Duplicate();
		hover.BgColor = bg.Lightened(0.08f);
		button.AddThemeStyleboxOverride("normal", style);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("pressed", hover);
		var nameLabel = button.GetChild(0).GetNode<Label>("Name");
		nameLabel.AddThemeColorOverride("font_color", text);
	}

	private void Select(SkillDefinition skill)
	{
		_selected = skill;
		Refresh();
	}

	private void ShowDetails()
	{
		var skill = _selected;
		_learnButton.Visible = skill != null;
		if (skill == null)
		{
			_detailName.Text = "Pick a skill";
			_detailInfo.Text = "Click a skill in the tree to see what it does.";
			_detailEffects.Text = "";
			_detailStatus.Text = "";
			return;
		}

		var progression = _unit.Progression;
		var status = progression.GetStatus(skill);
		_detailName.Text = skill.Name;
		_detailInfo.Text = $"Tier {skill.Tier}  ·  costs {skill.Cost} point{(skill.Cost == 1 ? "" : "s")}\n{skill.Description}";
		_detailEffects.Text = string.Join("\n", skill.EffectLines());

		var requires = skill.Requires.Where(r => r != null).Select(r => r.Name).ToList();
		string requiresText = requires.Count > 0 ? $"Requires: {string.Join(", ", requires)}.  " : "";
		_detailStatus.Text = requiresText + status switch
		{
			SkillStatus.Unlocked => "Learned.",
			SkillStatus.Available => "Ready to learn.",
			SkillStatus.NoPoints => "Not enough skill points. Level up to earn more.",
			SkillStatus.NeedsLevel => $"Needs unit level {skill.RequiredUnitLevel}.",
			_ => "Learn the required skills first.",
		};
		_learnButton.Disabled = status != SkillStatus.Available;
		_learnButton.Text = status == SkillStatus.Unlocked ? "Learned" : "Learn";
	}

	private void LearnSelected()
	{
		if (_unit != null && _selected != null)
			_unit.UnlockSkill(_selected); // bumps the unit's revision, which refreshes the window
	}
}
