using Godot;
using System.Collections.Generic;
using System;
using System.Linq;

// Centred window showing a unit's skill tree: tier 1 at the bottom up to tier 5 at the top,
// lines from each skill down to the skills it requires. A tree split into classes shows them as
// sections side by side under their names: subclass, main class (highlighted), subclass. Every skill
// is a round icon with its name under it; its rim shows whether it is learned, ready or out of reach
// (grey). Click a skill to see its details, then "Learn" to spend skill points on it. Built in code, like FieldHoverCard.
public partial class SkillTreeWindow : PanelContainer
{
	// A node is a round icon with the skill's name under it
	private static readonly Vector2 FullNodeSize = new Vector2(112, 70);
	private const float IconSize = 52;
	private const float FullColumnGap = 22;
	// Wide trees shrink their nodes down to this to fit the screen
	private const float MinNodeWidth = 92;
	private const float NarrowColumnGap = 10;
	// Room the window's frame and padding take next to the canvas
	private const float WindowMargin = 60;
	private const float RowGap = 10;
	private const float TierLabelWidth = 70;
	// Extra room between class sections, and the row of class names above the tiers
	private const float ClassGap = 34;
	private const float HeaderHeight = 26;
	// The inspect panel on the right: its width, the size of its icon and the gap before it
	// (kept narrow so a 3-class tree still fits next to it on a 1152px wide screen)
	private const float InspectWidth = 220;
	private const float InspectIconSize = 128;
	private const int InspectGap = 24;

	private static readonly Color Gold = new Color(0.85f, 0.65f, 0.3f);
	private static readonly Color BrightGold = new Color(1f, 0.82f, 0.4f);
	private static readonly Color LockedGrey = new Color(0.35f, 0.33f, 0.33f);
	private static readonly Color IconBack = new Color(0.12f, 0.1f, 0.1f);

	private Label _title;
	private Label _progressLabel;
	private Control _canvas;
	private Label _detailName;
	private Label _detailClass;
	private Label _detailInfo;
	private VBoxContainer _inspectPanel;
	private Control _inspectIcon;
	private TextureRect _inspectTexture;
	private Control _inspectRim;
	private Label _detailEffects;
	private Label _detailStatus;
	private Button _learnButton;

	private unit _unit;
	private SkillDefinition _selected;
	private readonly Dictionary<SkillDefinition, Button> _nodes = new();
	// How each node is drawn right now (its rim and initials read it)
	private readonly Dictionary<SkillDefinition, (SkillStatus Status, bool Selected)> _nodeState = new();
	// Icons of skills still out of reach are drawn in grey
	private readonly ShaderMaterial _greyMaterial = new()
	{
		Shader = new Shader
		{
			Code = @"shader_type canvas_item;
void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	float g = dot(c.rgb, vec3(0.3, 0.59, 0.11));
	COLOR = vec4(vec3(g) * 0.7, c.a);
}",
		},
	};
	// Class sections left to right: name (null for a tree without classes), first column, column count, main class?
	private readonly List<(string Name, int FirstColumn, int Columns, bool Main)> _sections = new();
	private readonly Dictionary<SkillDefinition, int> _sectionOf = new();
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

		// The tree on the left, the inspect panel for the selected skill on the right
		var body = new HBoxContainer();
		body.AddThemeConstantOverride("separation", InspectGap / 2);
		box.AddChild(body);
		_canvas = new Control { MouseFilter = MouseFilterEnum.Pass };
		_canvas.Draw += DrawTree;
		body.AddChild(_canvas);
		body.AddChild(new VSeparator());
		body.AddChild(BuildInspectPanel());
	}

	// Large icon, then name, class and tier, description, effects, requirements and the Learn button
	private Control BuildInspectPanel()
	{
		// As tall as the tree (set in BuildTree); long text scrolls instead of making the window taller
		_inspectPanel = new VBoxContainer { CustomMinimumSize = new Vector2(InspectWidth, 0) };
		var panel = _inspectPanel;
		panel.AddThemeConstantOverride("separation", 4);

		_inspectIcon = new Control { CustomMinimumSize = new Vector2(InspectWidth, InspectIconSize + 8), MouseFilter = MouseFilterEnum.Ignore };
		_inspectIcon.Draw += DrawInspectDisc;
		_inspectTexture = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Position = new Vector2((InspectWidth - InspectIconSize) / 2, 4),
			Size = new Vector2(InspectIconSize, InspectIconSize),
			MouseFilter = MouseFilterEnum.Ignore,
			TextureFilter = TextureFilterEnum.LinearWithMipmaps,
		};
		_inspectIcon.AddChild(_inspectTexture);
		_inspectRim = new Control { Position = _inspectTexture.Position, Size = _inspectTexture.Size, MouseFilter = MouseFilterEnum.Ignore };
		_inspectRim.Draw += DrawInspectRim;
		_inspectIcon.AddChild(_inspectRim);
		panel.AddChild(_inspectIcon);

		// Wrapping labels need a width, or they wrap after every word and the window grows very tall
		Label Wrapping(string variation = "") => new Label
		{
			ThemeTypeVariation = variation,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(InspectWidth - 12, 0), // room for the scroll bar
		};
		_detailName = Wrapping("HeaderLabel");
		_detailName.HorizontalAlignment = HorizontalAlignment.Center;
		_detailName.AddThemeColorOverride("font_color", BrightGold);
		_detailClass = Wrapping("MutedLabel");
		_detailClass.HorizontalAlignment = HorizontalAlignment.Center;
		_detailInfo = Wrapping();
		_detailEffects = Wrapping();
		_detailEffects.AddThemeColorOverride("font_color", BrightGold);
		_detailStatus = Wrapping("MutedLabel");
		panel.AddChild(_detailName);
		panel.AddChild(_detailClass);
		panel.AddChild(new HSeparator());

		// Description, effects and requirements fill the rest and scroll when long
		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		panel.AddChild(scroll);
		var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		text.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(text);
		text.AddChild(_detailInfo);
		text.AddChild(_detailEffects);
		text.AddChild(_detailStatus);

		_learnButton = new Button { Text = "Learn", FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(140, 34) };
		_learnButton.Pressed += LearnSelected;
		panel.AddChild(_learnButton);
		return panel;
	}

	private void DrawInspectDisc()
	{
		var centre = _inspectTexture.Position + _inspectTexture.Size / 2;
		_inspectIcon.DrawCircle(centre, InspectIconSize / 2, IconBack);
		if (_selected?.Icon != null)
			return;
		string initials = _selected == null ? "?" : Initials(_selected);
		var status = _selected == null ? SkillStatus.NeedsLevel : _nodeState.GetValueOrDefault(_selected).Status;
		var color = status == SkillStatus.Unlocked || status == SkillStatus.Available ? BrightGold : LockedGrey.Lightened(0.25f);
		var font = _inspectIcon.GetThemeFont("font");
		const int size = 52;
		_inspectIcon.DrawString(font, new Vector2(_inspectTexture.Position.X, centre.Y + size * 0.35f), initials, HorizontalAlignment.Center, InspectIconSize, size, color);
	}

	private void DrawInspectRim()
	{
		var status = _selected == null ? SkillStatus.NeedsLevel : _nodeState.GetValueOrDefault(_selected).Status;
		(Color color, float width) = status switch
		{
			SkillStatus.Unlocked => (BrightGold, 6f),
			SkillStatus.Available => (BrightGold, 4f),
			SkillStatus.NoPoints => (Gold.Darkened(0.35f), 4f),
			_ => (LockedGrey, 4f),
		};
		float radius = InspectIconSize / 2;
		_inspectRim.DrawArc(_inspectRim.Size / 2, radius - width / 2 + 0.5f, 0, Mathf.Tau, 96, color, width, true);
	}

	private static string Initials(SkillDefinition skill) => string.Concat(skill.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
		.Where(word => char.IsLetter(word[0])).Take(2).Select(word => char.ToUpper(word[0])));

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

		var tree = _unit.SkillTree;
		var skills = tree.AllSkills().ToList();
		BuildSections(tree);
		int columns = _sections.Sum(s => s.Columns);
		int tiers = SkillDefinition.MaxTier;
		FitNodes(columns);
		_canvas.CustomMinimumSize = new Vector2(
			TierLabelWidth + columns * _nodeSize.X + (columns - 1) * _columnGap + (_sections.Count - 1) * ClassGap,
			Top + tiers * _nodeSize.Y + (tiers - 1) * RowGap);
		_inspectPanel.CustomMinimumSize = new Vector2(InspectWidth, _canvas.CustomMinimumSize.Y);

		// Class names over their sections
		foreach (var section in _sections)
		{
			if (section.Name == null)
				continue;
			var header = new Label
			{
				Text = section.Main ? section.Name.ToUpper() : section.Name,
				ThemeTypeVariation = section.Main ? "HeaderLabel" : "",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Position = new Vector2(SectionX(section), 0),
				Size = new Vector2(SectionWidth(section), HeaderHeight - 6),
				MouseFilter = MouseFilterEnum.Ignore,
			};
			header.AddThemeColorOverride("font_color", section.Main ? BrightGold : Gold);
			_canvas.AddChild(header);
		}

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
			// A flat button takes the click; the disc, icon, rim and name are its children
			var button = new Button
			{
				Position = NodePosition(skill),
				Size = _nodeSize,
				CustomMinimumSize = _nodeSize,
				FocusMode = FocusModeEnum.None,
				Flat = true,
				TooltipText = $"{skill.Name}\n{string.Join("\n", skill.EffectLines())}",
			};
			var empty = new StyleBoxEmpty();
			foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus" })
				button.AddThemeStyleboxOverride(state, empty);

			var clicked = skill;
			var iconAt = new Vector2((_nodeSize.X - IconSize) / 2, 0);
			var iconSize = new Vector2(IconSize, IconSize);
			var disc = new Control { Position = iconAt, Size = iconSize, MouseFilter = MouseFilterEnum.Ignore };
			disc.Draw += () => DrawDisc(disc, clicked);
			button.AddChild(disc);
			if (skill.Icon != null)
				button.AddChild(new TextureRect
				{
					Name = "Icon",
					Texture = skill.Icon,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					Position = iconAt,
					Size = iconSize,
					MouseFilter = MouseFilterEnum.Ignore,
					TextureFilter = TextureFilterEnum.LinearWithMipmaps,
				});
			var rim = new Control { Name = "Rim", Position = iconAt, Size = iconSize, MouseFilter = MouseFilterEnum.Ignore };
			rim.Draw += () => DrawRim(rim, clicked, button.IsHovered());
			button.AddChild(rim);
			button.MouseEntered += rim.QueueRedraw;
			button.MouseExited += rim.QueueRedraw;

			var nameLabel = new Label
			{
				Name = "Name",
				Text = skill.Name,
				HorizontalAlignment = HorizontalAlignment.Center,
				Position = new Vector2(-6, IconSize + 2),
				Size = new Vector2(_nodeSize.X + 12, _nodeSize.Y - IconSize - 2),
				ClipText = true,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			button.AddChild(nameLabel);
			button.Pressed += () => Select(clicked);
			_canvas.AddChild(button);
			FitName(nameLabel);
			_nodes[skill] = button;
		}
	}

	// The dark disc behind the icon; a skill without art yet shows its initials on it
	private void DrawDisc(Control disc, SkillDefinition skill)
	{
		disc.DrawCircle(disc.Size / 2, IconSize / 2, IconBack);
		if (skill.Icon != null)
			return;
		var status = _nodeState.GetValueOrDefault(skill).Status;
		string initials = Initials(skill);
		var color = status == SkillStatus.Unlocked || status == SkillStatus.Available ? BrightGold : LockedGrey.Lightened(0.25f);
		disc.DrawString(disc.GetThemeFont("font"), new Vector2(0, disc.Size.Y / 2 + 8), initials, HorizontalAlignment.Center, IconSize, 22, color);
	}

	// The ring around the icon: colour and width show the skill's state
	private void DrawRim(Control rim, SkillDefinition skill, bool hovered)
	{
		var (status, selected) = _nodeState.GetValueOrDefault(skill);
		(Color color, float width) = status switch
		{
			SkillStatus.Unlocked => (BrightGold, 3.5f),
			SkillStatus.Available => (BrightGold, 2.5f),
			SkillStatus.NoPoints => (Gold.Darkened(0.35f), 2f),
			_ => (LockedGrey, 2f),
		};
		if (hovered)
			color = color.Lightened(0.3f);
		var centre = rim.Size / 2;
		float radius = IconSize / 2;
		rim.DrawArc(centre, radius - width / 2 + 0.5f, 0, Mathf.Tau, 48, color, width, true);
		if (selected)
			rim.DrawArc(centre, radius + 4, 0, Mathf.Tau, 48, Colors.White, 2f, true);
		else if (status == SkillStatus.Available)
			rim.DrawArc(centre, radius + 3.5f, 0, Mathf.Tau, 48, new Color(BrightGold, 0.35f), 3f, true); // ready to learn
	}

	// Sections in tree order; a tree without classes is one unnamed section
	private void BuildSections(SkillTree tree)
	{
		_sections.Clear();
		_sectionOf.Clear();
		var classes = tree.SectionOrder();
		var groups = classes.Select(c => (c.Name, Skills: c.Skills.Where(s => s != null).ToList(), Main: c == tree.MainClass)).ToList();
		var loose = tree.Skills.Where(s => s != null).ToList();
		if (loose.Count > 0 || groups.Count == 0)
			groups.Add((groups.Count == 0 ? null : "Other", loose, false));
		int first = 0;
		foreach (var group in groups)
		{
			int columns = group.Skills.Count == 0 ? 1 : group.Skills.Max(s => s.Column) + 1;
			foreach (var skill in group.Skills)
				_sectionOf.TryAdd(skill, _sections.Count);
			_sections.Add((group.Name, first, columns, group.Main));
			first += columns;
		}
	}

	private float Top => _sections.Any(s => s.Name != null) ? HeaderHeight : 0;

	private float SectionX((string Name, int FirstColumn, int Columns, bool Main) section) =>
		TierLabelWidth + section.FirstColumn * (_nodeSize.X + _columnGap) + _sections.IndexOf(section) * ClassGap;

	private float SectionWidth((string Name, int FirstColumn, int Columns, bool Main) section) =>
		section.Columns * _nodeSize.X + (section.Columns - 1) * _columnGap;

	// Full-size nodes when they fit on screen, otherwise narrower nodes and gaps
	private void FitNodes(int columns)
	{
		_nodeSize = FullNodeSize;
		_columnGap = FullColumnGap;
		float available = GetViewportRect().Size.X - WindowMargin - InspectWidth - InspectGap - TierLabelWidth - (_sections.Count - 1) * ClassGap;
		if (columns * FullNodeSize.X + (columns - 1) * FullColumnGap <= available)
			return;
		_columnGap = NarrowColumnGap;
		float width = Mathf.Floor((available - (columns - 1) * _columnGap) / columns);
		_nodeSize = new Vector2(Mathf.Clamp(width, MinNodeWidth, FullNodeSize.X), FullNodeSize.Y);
	}

	private bool Narrow => _nodeSize.X < FullNodeSize.X;

	// Makes the font of a long skill name smaller until it fits under its icon (the label must be in
	// the tree). A name too long even then takes two lines.
	private void FitName(Label label)
	{
		const int minSize = 11;
		var font = label.GetThemeFont("font");
		int size = Math.Min(label.GetThemeFontSize("font_size"), Narrow ? 12 : 14);
		float room = label.Size.X - 4;
		while (size > minSize && font.GetStringSize(label.Text, HorizontalAlignment.Left, -1, size).X > room)
			size--;
		if (font.GetStringSize(label.Text, HorizontalAlignment.Left, -1, size).X > room)
		{
			label.ClipText = false;
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		}
		label.AddThemeFontSizeOverride("font_size", size);
	}

	private float RowY(int tier) => Top + (SkillDefinition.MaxTier - tier) * (_nodeSize.Y + RowGap);

	private Vector2 NodePosition(SkillDefinition skill)
	{
		int tier = Mathf.Clamp(skill.Tier, SkillDefinition.MinTier, SkillDefinition.MaxTier);
		var section = _sections[_sectionOf.GetValueOrDefault(skill)];
		return new Vector2(SectionX(section) + skill.Column * (_nodeSize.X + _columnGap), RowY(tier));
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
		// The main class glows warmer; thin dividers between the class sections
		float bottom = RowY(SkillDefinition.MinTier) + _nodeSize.Y + 6;
		for (int i = 0; i < _sections.Count; i++)
		{
			var section = _sections[i];
			if (section.Main)
				_canvas.DrawRect(new Rect2(SectionX(section) - 8, 0, SectionWidth(section) + 16, bottom), new Color(BrightGold, 0.06f));
			if (i > 0)
			{
				float x = SectionX(section) - (ClassGap + _columnGap) / 2;
				_canvas.DrawLine(new Vector2(x, 4), new Vector2(x, bottom), new Color(Gold, 0.35f), 1f);
			}
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
			StyleNode(skill, button, progression.GetStatus(skill), skill == _selected);
		_canvas.QueueRedraw();
		ShowDetails();
	}

	private void StyleNode(SkillDefinition skill, Button button, SkillStatus status, bool selected)
	{
		_nodeState[skill] = (status, selected);
		bool reachable = status == SkillStatus.Unlocked || status == SkillStatus.Available || status == SkillStatus.NoPoints;
		var text = status switch
		{
			SkillStatus.Unlocked => BrightGold,
			SkillStatus.Available => new Color(1f, 0.92f, 0.75f),
			SkillStatus.NoPoints => new Color(0.8f, 0.75f, 0.68f),
			_ => new Color(0.5f, 0.48f, 0.46f),
		};
		button.GetNode<Label>("Name").AddThemeColorOverride("font_color", text);
		if (button.GetNodeOrNull<TextureRect>("Icon") is { } icon)
		{
			// Learned: full colour; can be learned: a little dimmed; out of reach: grey
			icon.Material = reachable ? null : _greyMaterial;
			icon.Modulate = status == SkillStatus.Unlocked ? Colors.White : reachable ? new Color(0.8f, 0.8f, 0.8f) : Colors.White;
		}
		foreach (var child in button.GetChildren())
			if (child is Control control)
				control.QueueRedraw();
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
		_inspectTexture.Texture = skill?.Icon;
		_inspectIcon.QueueRedraw();
		_inspectRim.QueueRedraw();
		if (skill == null)
		{
			_detailName.Text = "Pick a skill";
			_detailClass.Text = "";
			_detailInfo.Text = "Click a skill in the tree to see what it does.";
			_detailEffects.Text = "";
			_detailStatus.Text = "";
			return;
		}

		var progression = _unit.Progression;
		var status = progression.GetStatus(skill);
		bool reachable = status == SkillStatus.Unlocked || status == SkillStatus.Available || status == SkillStatus.NoPoints;
		_inspectTexture.Material = reachable ? null : _greyMaterial;
		_detailName.Text = skill.Name;
		string className = _sectionOf.TryGetValue(skill, out int sectionIndex) ? _sections[sectionIndex].Name : null;
		_detailClass.Text = (className != null ? $"{className}  ·  " : "") + $"Tier {skill.Tier}  ·  costs {skill.Cost} point{(skill.Cost == 1 ? "" : "s")}";
		_detailInfo.Text = skill.Description;
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
