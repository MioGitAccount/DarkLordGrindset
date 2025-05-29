using Godot;
using System;
using System.Collections.Generic;

public partial class BattleUIControler : Control
{
	private BattleUnit selectedUnit = null;
	private BattlePositionPanel selectedUnitPanel = null;

	private List<BattleUnit> BattleUnitListLeft = new List<BattleUnit>();
	private List<BattleUnit> BattleUnitListRight = new List<BattleUnit>();

	public override void _Ready()
	{
		var button = GetNode<Button>("HBoxContainer/AttackButton");
		button.Pressed += OnSpawnButtonPressed;

		LoadUnits();

		// Place right units immediately into PanelContainerRight
		var panelRight = GetNode<Panel>("PanelContainerRight");
		LoadUnitsIntoPanels(panelRight, BattleUnitListRight, isSelectable: false);

		// Place left units into selection area
		var panelAbove = GetNode<Panel>("PanelContainerAbove");
		LoadUnitsIntoPanels(panelAbove, BattleUnitListLeft, isSelectable: true);

		// Setup left-side positions
		var panelLeft = GetNode<Panel>("PanelContainerLeft");
		SetupDropPanels(panelLeft);
	}

	private void OnSpawnButtonPressed()
	{
		var battleGround = GetNode("..") as BattleGround;
		battleGround.LoadUnits(BattleUnitListLeft,BattleUnitListRight);
		battleGround?.Battle();
	}

	private void LoadUnits()
	{
		Node nodeParent = GetNode<Node>("../BattleUnitListLeft");
		List<Node> listOfNodes = new List<Node>(nodeParent.GetChildren());
		foreach (Node node in listOfNodes)
		{
			if (node is BattleUnit)
			{
				BattleUnit unit = (BattleUnit)node;
				BattleUnitListLeft.Add(unit);
			}
		}
		Node nodeParentR = GetNode<Node>("../BattleUnitListRight");
		List<Node> listOfNodesR = new List<Node>(nodeParentR.GetChildren());
		foreach (Node node in listOfNodesR)
		{
				if (node is BattleUnit)
				{
					BattleUnit unit = (BattleUnit)node;
					BattleUnitListRight.Add(unit);
				}
		}
		
	}

	private void LoadUnitsIntoPanels(Panel container, List<BattleUnit> units, bool isSelectable)
	{
		for (int i = 0; i < container.GetChildCount() && i < units.Count; i++)
		{
			
			var panel = container.GetChild(i) as BattlePositionPanel;
			var unit = units[i];
			panel.SetUnit(unit);

			if (isSelectable)
			{
				panel.OnPanelClicked = (clickedPanel) =>
				{
					selectedUnit = clickedPanel.UnitOnTop;
					selectedUnitPanel = clickedPanel;
				};
			}
		}
	}

	private void SetupDropPanels(Panel container)
	{
		for (int i = 0; i < container.GetChildCount(); i++)
		{
			var panel = container.GetChild(i) as BattlePositionPanel;

			panel.OnPanelClicked = (targetPanel) =>
			{
				if (selectedUnit != null && !targetPanel.IsOccupied)
				{
					// Move the selected unit
					targetPanel.SetUnit(selectedUnit);
					selectedUnitPanel.RemoveUnit();
					selectedUnit = null;
					selectedUnitPanel = null;
				}
			};
		}
	}
}
