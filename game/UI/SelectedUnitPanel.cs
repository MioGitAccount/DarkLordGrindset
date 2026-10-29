using Godot;

// Bottom-left panel for the selected player unit: portrait, level/XP, stats and ability buttons
public partial class SelectedUnitPanel : PanelContainer
{
	// Raised when the player asks for the detailed view of the shown unit
	public event System.Action<unit> InspectRequested;
	// Raised when the player opens the shown unit's skill tree
	public event System.Action<unit> SkillsRequested;

	[Export] public PackedScene IconSlotScene;

	[Export] private TextureRect Portrait;
	[Export] private Label NameLabel;
	[Export] private Label StatsLabel;
	[Export] private Label LocationLabel;
	[Export] private AttributePentagon AttributesChart;
	[Export] private Control AbilitiesSection;
	[Export] private Container AbilitiesRow;

	private const int AbilitySlotSize = 34;

	private unit _unit;
	private double _refreshTimer;
	private int _shownRevision = -1;
	private Label _levelLabel;
	private ProgressBar _experienceBar;
	private Button _skillsButton;

	public override void _Ready()
	{
		// Created in code (like FieldHoverCard), so saving UI.tscn in the editor can't drop them
		var details = NameLabel.GetParent();

		// Level + experience bar, right under the name
		var levelRow = new HBoxContainer { Name = "LevelRow" };
		levelRow.AddThemeConstantOverride("separation", 6);
		_levelLabel = new Label { ThemeTypeVariation = "MutedLabel" };
		_experienceBar = new ProgressBar
		{
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0, 10),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		var barBackground = new StyleBoxFlat { BgColor = new Color(0.12f, 0.1f, 0.12f), BorderColor = new Color(0.3f, 0.25f, 0.2f) };
		barBackground.SetBorderWidthAll(1);
		barBackground.SetCornerRadiusAll(3);
		var barFill = new StyleBoxFlat { BgColor = new Color(0.55f, 0.35f, 0.85f) };
		barFill.SetCornerRadiusAll(3);
		_experienceBar.AddThemeStyleboxOverride("background", barBackground);
		_experienceBar.AddThemeStyleboxOverride("fill", barFill);
		levelRow.AddChild(_levelLabel);
		levelRow.AddChild(_experienceBar);
		details.AddChild(levelRow);
		details.MoveChild(levelRow, NameLabel.GetIndex() + 1);

		var buttons = new HBoxContainer { Name = "Buttons" };
		buttons.AddThemeConstantOverride("separation", 6);
		var inspectButton = new Button
		{
			Text = "Inspect",
			TooltipText = "Show this unit's portrait and attributes",
			FocusMode = FocusModeEnum.None,
		};
		inspectButton.Pressed += () => InspectRequested?.Invoke(_unit);
		_skillsButton = new Button
		{
			Text = "Skills",
			TooltipText = "Open this unit's skill tree",
			FocusMode = FocusModeEnum.None,
		};
		_skillsButton.Pressed += () => SkillsRequested?.Invoke(_unit);
		buttons.AddChild(inspectButton);
		buttons.AddChild(_skillsButton);
		details.AddChild(buttons);
		ShowUnit(null);
	}

	public override void _Process(double delta)
	{
		// Stats and location change during play (movement, battles); a few refreshes per second is plenty
		_refreshTimer -= delta;
		if (_unit == null || _refreshTimer > 0)
			return;
		_refreshTimer = 0.25;
		RefreshDetails();
	}

	public void ShowUnit(unit selectedUnit)
	{
		_unit = IsInstanceValid(selectedUnit) ? selectedUnit : null;
		Visible = _unit != null;
		if (_unit == null)
			return;

		Portrait.Texture = _unit.GetIcon();
		NameLabel.Text = _unit.Template?.Name ?? _unit.Name;
		_shownRevision = -1;
		RefreshDetails();
	}

	private void RefreshDetails()
	{
		if (!IsInstanceValid(_unit))
		{
			ShowUnit(null);
			return;
		}

		StatsLabel.Text = UIText.Stats(_unit.battleStats);
		StatsLabel.TooltipText = StatsLabel.Text; // the label clips instead of wrapping
		RefreshAbilityStates();
		if (_unit.Revision != _shownRevision)
		{
			// Level up or new skill: attributes, abilities and XP may all have changed
			_shownRevision = _unit.Revision;
			if (AttributesChart != null)
				AttributesChart.Attributes = _unit.Attributes;
			RefreshAbilities();
			RefreshProgress();
		}
		if (_unit.isMoving)
			LocationLabel.Text = "Moving...";
		else if (_unit.OnQuest)
			LocationLabel.Text = $"{_unit.currentField?.type?.Name}: on {_unit.ActiveQuest.Title}";
		else
			LocationLabel.Text = _unit.currentField?.type?.Name ?? "";
	}

	private void RefreshProgress()
	{
		var progression = _unit.Progression;
		_levelLabel.Text = $"Lv {progression.Level}";
		int needed = UnitProgression.ExperienceToNextLevel(progression.Level);
		_experienceBar.MaxValue = progression.IsMaxLevel ? 1 : needed;
		_experienceBar.Value = progression.IsMaxLevel ? 1 : progression.Experience;
		_experienceBar.TooltipText = progression.IsMaxLevel ? "Max level" : $"XP {progression.Experience}/{needed}";
		_skillsButton.Visible = _unit.SkillTree != null;
		_skillsButton.Text = progression.SkillPoints > 0 ? $"Skills ({progression.SkillPoints})" : "Skills";
	}

	private void RefreshAbilities()
	{
		// The row keeps a fixed height even when empty, so learning abilities never resizes the panel
		UIText.ClearChildren(AbilitiesRow);
		var abilities = _unit.Abilities;
		if (abilities == null || abilities.Count == 0)
		{
			AbilitiesRow.AddChild(new Label
			{
				Text = "No abilities",
				ThemeTypeVariation = "MutedLabel",
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
			});
			return;
		}

		var owner = _unit;
		foreach (var ability in abilities)
		{
			var slot = IconSlotScene.Instantiate<IconSlot>();
			slot.CustomMinimumSize = new Vector2(AbilitySlotSize, AbilitySlotSize);
			slot.ToggleMode = true; // shows pressed while the ability runs
			slot.Setup(ability.Icon, () => UIText.AbilityTooltip(ability, owner.IsUsingAbility(ability)));
			slot.Pressed += () =>
			{
				owner.UseAbility(ability);
				RefreshAbilityStates();
			};
			slot.SetMeta("ability", ability);
			AbilitiesRow.AddChild(slot);
		}
		RefreshAbilityStates();
	}

	// Abilities stop on their own (field ran dry, unit moved), so keep the pressed look in sync
	private void RefreshAbilityStates()
	{
		foreach (var child in AbilitiesRow.GetChildren())
			if (child is IconSlot slot && slot.HasMeta("ability"))
				slot.SetPressedNoSignal(_unit.IsUsingAbility(slot.GetMeta("ability").As<Ability>()));
	}
}
