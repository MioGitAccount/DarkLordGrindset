using Godot;
using System;

public partial class PanelContainerForPlayerUnits : PanelContainer
{
	[Signal]
	public delegate void UnitSelectedEventHandler(unit unit);

	[Export] public PackedScene UnitEntryScene;

	[Export] private UnitStatsPanel StatsPanel;

	private Control _unitListContainer;

	public override void _Ready()
	{
		_unitListContainer = GetNode<Control>("HBoxContainer");
	}

	public void ShowUnits(Godot.Collections.Array<unit> units)
	{
		//_unitListContainer.QueueFreeChildren();
		foreach (var child in _unitListContainer.GetChildren())
			child.QueueFree();

		if(units == null) return;

		foreach (var unit in units)
		{
			var entry = UnitEntryScene.Instantiate<UnitEntry>();
			entry.Setup(unit);
			// Forward child signal
			entry.UnitSelected += OnUnitEntrySelected;

			// Hover signals
			entry.Hovered += OnUnitHovered;
			entry.HoverExited += OnUnitHoverExited;
			
			_unitListContainer.AddChild(entry);
		}
	}
	private void OnUnitEntrySelected(unit unit)
	{
		EmitSignal(SignalName.UnitSelected, unit);
	}

	private void OnUnitHovered(unit unit)
	{
		StatsPanel.ShowStats(unit.battleStats, unit.Template.Name);
	}

	private void OnUnitHoverExited()
	{
		StatsPanel.HideStats();
	}
	
}
