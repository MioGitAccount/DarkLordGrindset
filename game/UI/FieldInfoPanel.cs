using Godot;
using System;

// Right-side panel describing the selected field: biome, resources, neutral and player units.
// Refreshes itself while units enter or leave the shown field and when its fog changes.
// In grey fog it shows the hostiles as last seen and hides live resource amounts.
public partial class FieldInfoPanel : PanelContainer
{
	public event Action<unit> UnitClicked;

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

	public override void _Ready()
	{
		_neutralHeader = NeutralSection.GetNode<Label>("NeutralHeader");
		ShowField(null);
	}

	public override void _ExitTree()
	{
		if (_field != null)
			_field.ContentsChanged -= Refresh;
	}

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
		TitleLabel.Text = (_field.type?.Name ?? _field.Name) + (InSight ? "" : " (last seen)");
		RefreshResources();
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
			slot.Setup(neutral.GetIcon(), () => UIText.UnitTooltip(name, neutral.GetEffectiveStats()));
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
			slot.Setup(playerUnit.GetIcon(), () => UIText.UnitTooltip(name, playerUnit.battleStats) + "\nClick to select");
			slot.Pressed += () => UnitClicked?.Invoke(playerUnit);
			PlayerUnitsRow.AddChild(slot);
		}
	}
}
