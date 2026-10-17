using Godot;

// Bottom-left panel for the selected player unit: portrait, stats and ability buttons
public partial class SelectedUnitPanel : PanelContainer
{
	[Export] public PackedScene IconSlotScene;

	[Export] private TextureRect Portrait;
	[Export] private Label NameLabel;
	[Export] private Label StatsLabel;
	[Export] private Label LocationLabel;
	[Export] private Control AbilitiesSection;
	[Export] private Container AbilitiesRow;

	private unit _unit;
	private double _refreshTimer;

	public override void _Ready()
	{
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
		RefreshDetails();
		RefreshAbilities();
	}

	private void RefreshDetails()
	{
		if (!IsInstanceValid(_unit))
		{
			ShowUnit(null);
			return;
		}

		StatsLabel.Text = UIText.Stats(_unit.battleStats);
		if (_unit.isMoving)
			LocationLabel.Text = "Moving...";
		else
			LocationLabel.Text = _unit.currentField?.type?.Name ?? "";
	}

	private void RefreshAbilities()
	{
		UIText.ClearChildren(AbilitiesRow);
		var abilities = _unit.Abilities;
		AbilitiesSection.Visible = abilities != null && abilities.Count > 0;
		if (!AbilitiesSection.Visible)
			return;

		var owner = _unit;
		foreach (var ability in abilities)
		{
			var slot = IconSlotScene.Instantiate<IconSlot>();
			slot.Setup(ability.Icon, () => ability.Name ?? "Ability");
			slot.Pressed += () => owner.UseAbility(ability);
			AbilitiesRow.AddChild(slot);
		}
	}
}
