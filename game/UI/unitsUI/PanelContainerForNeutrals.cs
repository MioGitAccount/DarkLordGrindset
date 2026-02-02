using Godot;
using System;

public partial class PanelContainerForNeutrals : PanelContainer
{

	[Export] public PackedScene NeutralUnitEntryScene;

	[Export] private UnitStatsPanel StatsPanel;

	private Control _unitListContainer;

	public override void _Ready()
	{
		_unitListContainer = GetNode<Control>("HBoxContainer");
	}

	public void ShowUnits(Godot.Collections.Array<NeutralUnit> neutralUnits)
	{
		//_unitListContainer.QueueFreeChildren();
		foreach (var child in _unitListContainer.GetChildren())
			child.QueueFree();

		if(neutralUnits == null) return;

		foreach (var unit in neutralUnits)
		{
			var entry = NeutralUnitEntryScene.Instantiate<NeutralUnitEntry>();
			entry.Setup(unit);

			// Hover signals
			entry.Hovered += OnUnitHovered;
			entry.HoverExited += OnUnitHoverExited;
			
			_unitListContainer.AddChild(entry);
		}
	}

	private void OnUnitHovered(NeutralUnit neutralUnit)
	{
		StatsPanel.ShowStats(neutralUnit.GetEffectiveStats(), neutralUnit.Template.Name);
	}

	private void OnUnitHoverExited()
	{
		StatsPanel.HideStats();
	}
}
