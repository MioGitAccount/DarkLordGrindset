using Godot;
using System;

// Root of the in-game HUD. The rest of the game talks to the UI only through this class.
public partial class UIManager : CanvasLayer
{
	// Raised when the player picks a unit from the UI (e.g. from the field panel)
	public event Action<unit> UnitSelected;

	[Export] private FieldInfoPanel FieldInfo;
	[Export] private SelectedUnitPanel SelectedUnit;
	[Export] private ResourceCounter PlayerGold;
	[Export] private ResourceCounter PlayerWood;
	[Export] private Control BattleOverlay;
	[Export] private BaseButton BattleButton;
	[Export] private BaseButton CloseBattleButton;
	// Created in code (it builds its own contents), so it doesn't depend on UI.tscn
	private FieldHoverCard FieldHoverCard;

	public override void _Ready()
	{
		FieldHoverCard = new FieldHoverCard { Name = "FieldHoverCard" };
		FieldInfo.GetParent().AddChild(FieldHoverCard);
		FieldInfo.UnitClicked += selected => UnitSelected?.Invoke(selected);
		BattleButton.Pressed += () => SetBattleVisible(!BattleOverlay.Visible);
		CloseBattleButton.Pressed += () => SetBattleVisible(false);
		SetBattleVisible(false);
	}

	public void ShowField(field selectedField)
	{
		FieldInfo.ShowField(selectedField);
	}

	public void ShowFieldHover(field hoveredField)
	{
		FieldHoverCard.ShowFor(hoveredField);
	}

	// True when the mouse is over a visible HUD panel (the map shouldn't react then)
	public bool IsMouseOverPanel()
	{
		Vector2 mouse = GetViewport().GetMousePosition();
		foreach (var child in FieldInfo.GetParent().GetChildren())
		{
			if (child is Control panel && panel.Visible && panel.MouseFilter != Control.MouseFilterEnum.Ignore
					&& panel.GetGlobalRect().HasPoint(mouse))
				return true;
		}
		return false;
	}

	public void ShowUnit(unit selectedUnit)
	{
		SelectedUnit.ShowUnit(selectedUnit);
	}

	public void SetPlayerResource(ResourceType type, float amount)
	{
		switch (type)
		{
			case ResourceType.Gold:
				PlayerGold.SetAmount(amount);
				break;
			case ResourceType.Wood:
				PlayerWood.SetAmount(amount);
				break;
		}
	}

	public void SetBattleVisible(bool visible)
	{
		BattleOverlay.Visible = visible;
	}
}
