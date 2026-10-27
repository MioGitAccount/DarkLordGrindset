using Godot;
using System.Collections.Generic;
using System.Linq;

// Centred window showing a castle's buildings as cards: level 1 at the bottom up to level 3 at
// the top, lines from each building down to the buildings it requires. Click a card to see its
// details, then "Build" to pay for it, or "Recruit" on a built dwelling. Built in code, like SkillTreeWindow.
public partial class BuildWindow : PanelContainer
{
	private static readonly Vector2 NodeSize = new Vector2(200, 62);
	private const float ColumnGap = 20;
	private const float RowGap = 30;
	private const float LevelLabelWidth = 76;

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
	private Button _buildButton;
	private Button _recruitButton;

	private Castle _castle;
	private BuildingDefinition _selected;
	private readonly Dictionary<BuildingDefinition, Button> _nodes = new();

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
			// Testing aid while gathering is slow; not shown in exported release builds
			var debugResources = new Button { Text = "+200 all (debug)", FocusMode = FocusModeEnum.None };
			debugResources.Pressed += () =>
			{
				foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
					PlayerResources.Add(type, 200);
			};
			header.AddChild(debugResources);
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
		_detailInfo = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0) };
		_detailEffects = new Label();
		_detailEffects.AddThemeColorOverride("font_color", BrightGold);
		_detailStatus = new Label { ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0) };
		text.AddChild(_detailName);
		text.AddChild(_detailInfo);
		text.AddChild(_detailEffects);
		text.AddChild(_detailStatus);
		var buttons = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
		details.AddChild(buttons);
		_buildButton = new Button { Text = "Build", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(130, 36) };
		_buildButton.Pressed += BuildSelected;
		buttons.AddChild(_buildButton);
		_recruitButton = new Button { Text = "Recruit", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(130, 36) };
		_recruitButton.Pressed += RecruitSelected;
		buttons.AddChild(_recruitButton);
	}

	public override void _ExitTree()
	{
		Unsubscribe();
	}

	public void Open(Castle castle)
	{
		if (castle == null)
		{
			Close();
			return;
		}
		if (castle != _castle)
		{
			Unsubscribe();
			_selected = null;
			_castle = castle;
			_castle.Changed += Refresh;
			PlayerResources.Changed += OnResourcesChanged;
			PlayerAgents.Changed += Refresh;
		}
		_title.Text = $"{_castle.Tree.Name} - buildings";
		BuildTree();
		Refresh();
		Visible = true;
		ResetSize();
		SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.KeepSize);
	}

	public void Close()
	{
		Visible = false;
		Unsubscribe();
		_castle = null;
		_selected = null;
	}

	private void Unsubscribe()
	{
		if (_castle != null)
			_castle.Changed -= Refresh;
		PlayerResources.Changed -= OnResourcesChanged;
		PlayerAgents.Changed -= Refresh;
	}

	// What the player can afford decides which cards light up
	private void OnResourcesChanged(ResourceType type, float amount) => Refresh();

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

		var buildings = _castle.Buildings.ToList();
		int columns = buildings.Count == 0 ? 1 : buildings.Max(b => b.Column) + 1;
		int levels = BuildingDefinition.MaxLevel;
		_canvas.CustomMinimumSize = new Vector2(
			LevelLabelWidth + columns * NodeSize.X + (columns - 1) * ColumnGap,
			levels * NodeSize.Y + (levels - 1) * RowGap);

		// Level labels down the left side: level 3 at the top
		for (int level = BuildingDefinition.MinLevel; level <= levels; level++)
		{
			var label = new Label
			{
				Text = $"Level {level}\nTower {level}",
				ThemeTypeVariation = "MutedLabel",
				Position = new Vector2(0, RowY(level)),
				Size = new Vector2(LevelLabelWidth - 8, NodeSize.Y),
				VerticalAlignment = VerticalAlignment.Center,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			_canvas.AddChild(label);
		}

		foreach (var building in buildings)
		{
			var button = new Button
			{
				Position = NodePosition(building),
				Size = NodeSize,
				CustomMinimumSize = NodeSize,
				FocusMode = FocusModeEnum.None,
				ClipText = true,
				TooltipText = building.Description,
			};
			var labels = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
			labels.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			labels.AddThemeConstantOverride("separation", 0);
			labels.AddChild(new Label { Text = building.Name, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore, Name = "Name" });
			labels.AddChild(new Label
			{
				Text = ShortEffect(building),
				HorizontalAlignment = HorizontalAlignment.Center,
				ThemeTypeVariation = "MutedLabel",
				ClipText = true,
				MouseFilter = MouseFilterEnum.Ignore,
			});
			labels.AddChild(new Label
			{
				Name = "Cost",
				HorizontalAlignment = HorizontalAlignment.Center,
				ThemeTypeVariation = "MutedLabel",
				ClipText = true,
				MouseFilter = MouseFilterEnum.Ignore,
			});
			button.AddChild(labels);
			var clicked = building;
			button.Pressed += () => Select(clicked);
			_canvas.AddChild(button);
			_nodes[building] = button;
		}
	}

	// One line that fits on a card, e.g. "Recruit Imp", "+150 vision", "Orc +2 lv"
	private static string ShortEffect(BuildingDefinition building)
	{
		var parts = new List<string>();
		if (building.RecruitsUnit != null)
			parts.Add($"Recruit {building.RecruitsUnit.Name}");
		if (building.BonusVision != 0)
			parts.Add($"+{building.BonusVision:0} vision");
		if (building.BonusRecruitLevel != 0)
			parts.Add($"{building.BonusLevelFor?.Name ?? "All"} +{building.BonusRecruitLevel} lv");
		if (building.AgentCap != 0)
			parts.Add($"+{building.AgentCap} agents");
		if (building.UnitCap != 0 && building.UnitCapFor != null && building.RecruitsUnit == null)
			parts.Add($"+{building.UnitCap} {building.UnitCapFor.Name}");
		return string.Join(", ", parts);
	}

	private static float RowY(int level) => (BuildingDefinition.MaxLevel - level) * (NodeSize.Y + RowGap);

	private static Vector2 NodePosition(BuildingDefinition building)
	{
		int level = Mathf.Clamp(building.Level, BuildingDefinition.MinLevel, BuildingDefinition.MaxLevel);
		return new Vector2(LevelLabelWidth + building.Column * (NodeSize.X + ColumnGap), RowY(level));
	}

	private void DrawTree()
	{
		if (_castle == null)
			return;
		// Faint band behind each level row, brighter once the tower reaches it
		for (int level = BuildingDefinition.MinLevel; level <= BuildingDefinition.MaxLevel; level++)
		{
			var band = new Rect2(LevelLabelWidth - 6, RowY(level) - 4, _canvas.Size.X - LevelLabelWidth + 6, NodeSize.Y + 8);
			_canvas.DrawRect(band, new Color(1, 1, 1, _castle.TowerLevel >= level ? 0.04f : 0.015f));
		}
		// Requirement lines: from the top of the required building to the bottom of the one needing it
		foreach (var (building, button) in _nodes)
		{
			foreach (var required in building.Requires)
			{
				if (required == null || !_nodes.TryGetValue(required, out var requiredButton))
					continue;
				var from = requiredButton.Position + new Vector2(NodeSize.X / 2, 0);
				var to = button.Position + new Vector2(NodeSize.X / 2, NodeSize.Y);
				bool active = _castle.IsBuilt(required);
				var color = active ? Gold : LockedGrey;
				float midY = (from.Y + to.Y) / 2;
				_canvas.DrawPolyline(new[] { from, new Vector2(from.X, midY), new Vector2(to.X, midY), to }, color, active ? 3f : 2f, true);
			}
		}
	}

	private void Refresh()
	{
		if (_castle == null)
			return;
		_progressLabel.Text = $"Tower level {_castle.TowerLevel}   Vision {_castle.VisionRadius:0}   Built {_castle.Built.Count}/{_castle.Buildings.Count()}";

		foreach (var (building, button) in _nodes)
		{
			var status = _castle.GetStatus(building);
			StyleNode(button, status, building == _selected);
			var costLabel = button.GetChild(0).GetNode<Label>("Cost");
			costLabel.Text = status == BuildingStatus.Built ? "Built" : ResourceCost.Text(building.Cost);
		}
		_canvas.QueueRedraw();
		ShowDetails();
	}

	private void StyleNode(Button button, BuildingStatus status, bool selected)
	{
		(Color bg, Color border, Color text) = status switch
		{
			BuildingStatus.Built => (new Color(0.38f, 0.16f, 0.1f), BrightGold, new Color(1f, 0.92f, 0.75f)),
			BuildingStatus.Available => (new Color(0.16f, 0.13f, 0.12f), BrightGold, new Color(1f, 0.92f, 0.75f)),
			BuildingStatus.NoResources => (new Color(0.13f, 0.11f, 0.11f), Gold.Darkened(0.4f), new Color(0.8f, 0.75f, 0.68f)),
			_ => (new Color(0.09f, 0.085f, 0.09f), LockedGrey, new Color(0.5f, 0.48f, 0.46f)),
		};
		var style = new StyleBoxFlat { BgColor = bg, BorderColor = selected ? Colors.White : border };
		style.SetBorderWidthAll(selected ? 3 : status == BuildingStatus.Available ? 2 : 1);
		style.SetCornerRadiusAll(5);
		var hover = (StyleBoxFlat)style.Duplicate();
		hover.BgColor = bg.Lightened(0.08f);
		button.AddThemeStyleboxOverride("normal", style);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("pressed", hover);
		var nameLabel = button.GetChild(0).GetNode<Label>("Name");
		nameLabel.AddThemeColorOverride("font_color", text);
	}

	private void Select(BuildingDefinition building)
	{
		_selected = building;
		Refresh();
	}

	private void ShowDetails()
	{
		var building = _selected;
		_buildButton.Visible = building != null;
		_recruitButton.Visible = building?.RecruitsUnit != null;
		if (building == null)
		{
			_detailName.Text = "Pick a building";
			_detailInfo.Text = "Click a building to see what it does.";
			_detailEffects.Text = "";
			_detailStatus.Text = "";
			return;
		}

		var status = _castle.GetStatus(building);
		_detailName.Text = building.Name;
		_detailInfo.Text = $"Level {building.Level}  ·  costs {ResourceCost.Text(building.Cost)}\n{building.Description}";
		_detailEffects.Text = string.Join("\n", building.EffectLines());

		var requires = building.Requires.Where(r => r != null).Select(r => r.Name).ToList();
		string requiresText = requires.Count > 0 ? $"Requires: {string.Join(", ", requires)}.  " : "";
		_detailStatus.Text = requiresText + status switch
		{
			BuildingStatus.Built => "Built.",
			BuildingStatus.Available => "Ready to build.",
			BuildingStatus.NoResources => "Not enough resources.",
			BuildingStatus.NeedsTower => $"Needs Tower level {building.RequiredTowerLevel}.",
			_ => "Build the required buildings first.",
		};
		_buildButton.Disabled = status != BuildingStatus.Available;
		_buildButton.Text = status == BuildingStatus.Built ? "Built" : "Build";

		if (building.RecruitsUnit != null)
		{
			int level = _castle.RecruitLevel(building.RecruitsUnit);
			_recruitButton.Text = $"Recruit (lv {level})";
			_recruitButton.Disabled = !_castle.CanRecruit(building);
			var type = building.RecruitsUnit;
			string blocked = PlayerAgents.RecruitBlockedReason(type);
			_recruitButton.TooltipText = status != BuildingStatus.Built ? "Build it first"
				: blocked ?? $"Recruit a level {level} {type.Name} for {ResourceCost.Text(building.RecruitCost)}"
					+ $" ({type.Name} {PlayerAgents.CountOf(type)}/{PlayerAgents.CapFor(type)}, agents {PlayerAgents.Count}/{PlayerAgents.Cap})";
			if (status == BuildingStatus.Built && blocked != null)
				_detailStatus.Text += $"  {blocked}.";
		}
	}

	private void BuildSelected()
	{
		_castle?.TryBuild(_selected); // raises Castle.Changed, which refreshes the window
	}

	private void RecruitSelected()
	{
		if (_castle != null && _selected != null)
			_castle.TryRecruit(_selected); // paying changes PlayerResources, which refreshes the window
	}
}
