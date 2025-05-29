using Godot;
using System;

public partial class BattlePositionPanel : Panel
{
	public BattleUnit UnitOnTop { get; private set; }
	[Export] public int PanelIndex { get; set; }

	public bool IsOccupied => UnitOnTop != null;

	public Action<BattlePositionPanel> OnPanelClicked;

	public override void _Ready()
	{
		// React to mouse clicks
		MouseFilter = MouseFilterEnum.Stop;
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			OnPanelClicked?.Invoke(this);
		}
	}

	public void SetUnit(BattleUnit unit)
	{
		UnitOnTop = unit;

		foreach (var child in GetChildren())
			child.QueueFree();
		
		if (unit.GetParent() != null)
		{
			unit.GetParent().RemoveChild(unit);
		}
		if (unit != null)
		{
			AddChild(unit);
			unit.Position = Vector2.Zero;
			unit.battlePosition = PanelIndex;
		}
	}

	public void RemoveUnit()
	{
		if (UnitOnTop != null)
		{
			// UnitOnTop.QueueFree();
			UnitOnTop = null;
		}
	}
}
