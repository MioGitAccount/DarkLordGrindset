using Godot;
using System;
using System.Collections.Generic;

public partial class UnitManager : Node
{
	//[Export] public PackedScene UnitScene;  // Assign `Unit.tscn` in the inspector
	private Node unitsContainer;
	private unit selectedUnit;

	public override void _Ready()
	{
		//unitsContainer = GetNode<Node>("/Game/Map/UnitList");
	}

	// public void SpawnUnit(Vector2 position)
	// {
	// 	if (UnitScene == null) return;

	// 	unit newUnit = UnitScene.Instantiate<unit>();
	// 	newUnit.Position = position;
	// 	unitsContainer.AddChild(newUnit);
	// }

	

	public void SelectUnit(unit unit)
	{
		selectedUnit = unit;
		unit.Select();
	}

	public void MoveSelectedUnit(field targetField)
	{
		if (selectedUnit != null)
		{
			selectedUnit.MoveTo(targetField);
		}
	}
	public List<unit> findAllUnits()
	{
		List<unit> listOfUnits = new List<unit>();
		Node nodeParent = GetNode<Node>("../UnitList");
		GD.Print(nodeParent);
		List<Node> listOfNodes = new List<Node>(nodeParent.GetChildren());
		foreach (Node node in listOfNodes)
		{
			if(node is unit)
				listOfUnits.Add((unit)node);
		}
		return listOfUnits;
	}
	public void setUnitLocation(List<unit> listOfUnits, field place){
		foreach(unit aUnit in listOfUnits)
		{
			aUnit.currentField = place;
		}

	}
	public void MoveSelectedUnitTo(field targetNode)
	{
		if (selectedUnit == null || selectedUnit.currentField == null || selectedUnit.isMoving) return;

		List<field> path = PathFindingUtil.FindPath(selectedUnit.currentField, targetNode);
		GD.Print("Path size: " + path.Count);
		selectedUnit.MoveAlongPath(path);
	}
}
