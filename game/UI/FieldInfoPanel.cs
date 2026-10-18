using Godot;
using System;

// Right-side panel describing the selected field: biome, resources, neutral and player units.
// Refreshes itself while units enter or leave the shown field and when its fog changes.
// In grey fog it shows the hostiles as last seen and hides live resource amounts.
public partial class FieldInfoPanel : PanelContainer
{
	public event Action<unit> UnitClicked;
	// The "Enter castle" button of a castle field
	public event Action<Castle> CastleRequested;

	[Export] public PackedScene IconSlotScene;
	[Export] public PackedScene ResourceCounterScene;

	[Export] private Label TitleLabel;
	[Export] private TextureRect FieldImage;
	[Export] private Container ResourcesRow;
	[Export] private Control NeutralSection;
	[Export] private Container NeutralUnitsRow;
	[Export] private Label PlayerUnitsEmptyLabel;
	[Export] private Container PlayerUnitsRow;

	private field _field;
	private Label _neutralHeader;
	private Label _perksLabel;
	private Button _castleButton;

	public override void _Ready()
	{
		_neutralHeader = NeutralSection.GetNode<Label>("NeutralHeader");
		// Created in code so saving UI.tscn in the editor can't drop it; sits right under the resources
		_perksLabel = new Label
		{
			Name = "PerksLabel",
			ThemeTypeVariation = "MutedLabel",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Pass, // so the tooltip shows
		};
		ResourcesRow.GetParent().AddChild(_perksLabel);
		ResourcesRow.GetParent().MoveChild(_perksLabel, ResourcesRow.GetIndex() + 1);
		// Shown on fields with the Castle perk; opens the build window
		_castleButton = new Button { Name = "CastleButton", Text = "Enter castle", FocusMode = FocusModeEnum.None };
		_castleButton.Pressed += () => CastleRequested?.Invoke(_field?.Castle);
		ResourcesRow.GetParent().AddChild(_castleButton);
		ResourcesRow.GetParent().MoveChild(_castleButton, _perksLabel.GetIndex() + 1);
		ShowField(null);
	}

	public override void _ExitTree()
	{
		if (_field != null)
			_field.ContentsChanged -= Refresh;
	}

	public override void _Process(double delta)
	{
		// Timed perks (e.g. "Raided") count down; refresh their seconds left a few times a second
		_perksRefreshTimer -= delta;
		if (_field == null || _perksRefreshTimer > 0)
			return;
		_perksRefreshTimer = 0.5;
		RefreshPerks();
	}
	private double _perksRefreshTimer;

	public void ShowField(field selectedField)
	{
		if (_field != null)
			_field.ContentsChanged -= Refresh;

		_field = selectedField;
		Visible = _field != null;
		if (_field == null)
			return;

		_field.ContentsChanged += Refresh;
		FieldImage.Texture = _field.type?.FieldImage;
		Refresh();
	}

	private bool InSight => _field.Fog == FogState.Visible;

	private void Refresh()
	{
		TitleLabel.Text = (_field.type?.Name ?? _field.Name) + (_field.IsPlayerOwned ? " (yours)" : InSight ? "" : " (last seen)");
		_castleButton.Visible = _field.Castle != null;
		if (_field.Castle != null)
			_castleButton.Text = $"Enter castle (Tower {_field.Castle.TowerLevel})";
		RefreshResources();
		RefreshPerks();
		RefreshNeutralUnits();
		RefreshPlayerUnits();
	}

	private void RefreshResources()
	{
		UIText.ClearChildren(ResourcesRow);
		var resources = _field.resourcesHolder?.Resources;
		ResourcesRow.Visible = InSight && resources != null && resources.Count > 0;
		if (!ResourcesRow.Visible)
			return;

		foreach (var resource in resources)
		{
			var counter = ResourceCounterScene.Instantiate<ResourceCounter>();
			counter.Bind(resource);
			ResourcesRow.AddChild(counter);
		}
	}

	// Perks are part of the land, so they stay known in fog
	private void RefreshPerks()
	{
		var perks = new System.Collections.Generic.List<FieldPerk>(_field.Perks);
		_perksLabel.Visible = perks.Count > 0;
		if (perks.Count == 0)
			return;
		var names = new System.Collections.Generic.List<string>();
		var details = new System.Collections.Generic.List<string>();
		foreach (var perk in perks)
		{
			int ticksLeft = _field.GetTimedPerkTicksLeft(perk);
			names.Add(ticksLeft > 0 ? $"{perk.Name} ({ticksLeft}s)" : perk.Name);
			details.Add($"{perk.Name}: {perk.Description}\n  {perk.EffectText()}");
		}
		_perksLabel.Text = "Perks: " + string.Join(", ", names);
		_perksLabel.TooltipText = string.Join("\n", details);
	}

	private void RefreshNeutralUnits()
	{
		UIText.ClearChildren(NeutralUnitsRow);
		var neutrals = InSight ? _field.presentNeutralUnits : _field.LastSeenNeutrals;
		_neutralHeader.Text = InSight ? "Hostiles" : "Hostiles (last seen)";
		NeutralSection.Visible = neutrals != null && neutrals.Count > 0;
		if (!NeutralSection.Visible)
			return;

		foreach (var neutral in neutrals)
		{
			var slot = IconSlotScene.Instantiate<IconSlot>();
			string name = neutral.Template?.Name ?? "Unknown";
			slot.Setup(neutral.GetIcon(), () => UIText.UnitTooltip(name, neutral.GetEffectiveStats(), neutral.GetAttributes()));
			NeutralUnitsRow.AddChild(slot);
		}
	}

	private void RefreshPlayerUnits()
	{
		UIText.ClearChildren(PlayerUnitsRow);
		var units = _field.presentPlayerUnits;
		bool hasUnits = units != null && units.Count > 0;
		PlayerUnitsRow.Visible = hasUnits;
		PlayerUnitsEmptyLabel.Visible = !hasUnits;
		if (!hasUnits)
			return;

		foreach (var playerUnit in units)
		{
			var slot = IconSlotScene.Instantiate<IconSlot>();
			string name = playerUnit.Template?.Name ?? playerUnit.Name;
			slot.Setup(playerUnit.GetIcon(), () => UIText.UnitTooltip(name, playerUnit.battleStats, playerUnit.Attributes) + "\nClick to select");
			slot.Pressed += () => UnitClicked?.Invoke(playerUnit);
			PlayerUnitsRow.AddChild(slot);
		}
	}
}
