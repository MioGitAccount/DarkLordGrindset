using Godot;
using System;
using System.Collections.Generic;

public partial class UnitManager : Node
{
	//[Export] public PackedScene UnitScene;  // Assign `Unit.tscn` in the inspector
	private Node unitsContainer;
	public unit selectedUnit{ get; set; }
	private AudioStreamPlayer _selectSound;

	public override void _Ready()
	{
		 _selectSound = GetNode<AudioStreamPlayer>("SelectionSound");
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
		if(selectedUnit != null)
		{
			selectedUnit.Deselect();
			var sound = unit.Template?.SelectSound;
			if (sound != null)
			{
				_selectSound.Stream = sound;
				_selectSound.Play();
			}
		}		
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
	public bool MoveSelectedUnitTo(field targetNode)
	{
		if (selectedUnit == null || selectedUnit.currentField == null || selectedUnit.isMoving) return false;

		List<field> path = PathFindingUtil.FindPath(selectedUnit.currentField, targetNode);
		GD.Print("Path size: " + path.Count);
		selectedUnit.MoveAlongPath(path);
		return true;
	}
}
