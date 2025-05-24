using Godot;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

public partial class map : Node2D
{
	private Dictionary<String, field> fields= new Dictionary<String, field>();
	private List<(string, string)> connections = new List<(string, string)>
	{
		("Field","Field2"),("Field2","Field3"),("Field3","Field4"),
		("Field4","Field5"),("Field4","Field10"),("Field5","Field6"),
		("Field5","Field7"),("Field7","Field8"),("Field8","Field9"),
		("Field10","Field11"),("Field11","Field12"),("Field2","Field13"),
		("Field13","Field14"),("Field7","Field15"),("Field15","Field16"),
		("Field16","Field17"),("Field17","Field18"),("Field18","Field19"),
		("Field19","Field20"),("Field20","Field21"),("Field20","Field22"),
		("Field22","Field15")
	};
	private List<unit> units = new List<unit>();
	UnitManager unitManager;

	private field selectedField = null;
	private unit selectedUnit = null;
	//private Label fieldInfoLabel; // Reference to UI Label
	//private TextureRect fieldImage;
	private UIManager uIManager;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		//Find fields
		List<Node> listOfNodes = new List<Node>(GetChildren());
		foreach (Node node in listOfNodes)
		{
			if (node is field)
				fields.Add(node.Name, (field)node);
		}
		ConnectAllNodes();
		unitManager = GetNode<UnitManager>("UnitManager");
		units = unitManager.findAllUnits();
		unitManager.setUnitLocation(units, fields["Field"]);
		GD.Print("There are " + fields.Count + " fields on map");
		QueueRedraw();
		uIManager = GetNode<UIManager>("../UIManager");

	}
	public override void _Draw()
	{
		foreach (var connection in connections)
		{
			if (fields.ContainsKey(connection.Item1) && fields.ContainsKey(connection.Item2))
			{
				Vector2 pos1 = fields[connection.Item1].Position;
				Vector2 pos2 = fields[connection.Item2].Position;
				DrawDashedLine(pos1, pos2, Colors.Gray, 2);
			}
		}
	}
	public override void _Input(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
		{
			Vector2 mousePosition = GetGlobalMousePosition();
			bool found = false;

			foreach (var node in fields.Values)
			{
				if (node.IsPointInside(mousePosition))
				{
					if(mouseEvent.ButtonIndex == MouseButton.Left)
					{
						OnNodeClicked(node);
					}else if(mouseEvent.ButtonIndex == MouseButton.Right)
					{				
						OnNodeRightClicked(node);
					}
					found = true;
					break;
				}
			}
			//check for units units
			if(!found)
			{
				foreach (var node in units)
				{
					if (node.IsPointInside(mousePosition))
					{
						OnNodeClicked(node);
						found = true;
						break;
					}
				}

			}
		}
	}

	public void OnNodeClicked(Node2D clickedNode)
	{
		// Select the new node
		if(clickedNode is field){
			// Deselect the previous field
			if (selectedField != null)
			{
				selectedField.Deselect();
			}
			selectedField = (field)clickedNode;
			selectedField.Select();
			uIManager.UpdateSelectionInfo(selectedField);
		}
		else if(clickedNode is unit){
			// Deselect the previous unit
			if(selectedUnit != null)
			{
				selectedUnit.Deselect();
			}		
			// Select the new node
			GD.Print(clickedNode.Name + " is selected");
			unitManager.SelectUnit((unit)clickedNode);	
			uIManager.UpdateUnitSelectionInfo((unit)clickedNode);
		}
	}
	private void OnNodeRightClicked(Node2D node){
		if(node is field)
		unitManager.MoveSelectedUnitTo((field)node);
		GD.Print("right click on: " + node);
	}
	 
	private void ConnectAllNodes()
	{
		foreach(String key in fields.Keys)
		{
			List<String> neighborsNames = getAllNeighbors(key);
			
			List<field> neighbors = new List<field>();
			foreach(String s in neighborsNames)
			{
				neighbors.Add(fields[s]);
			}
			fields[key].Neighbors = neighbors;
		}
	}
	private List<String> getAllNeighbors(String nodeName)
	{
		List<String> neighbors = new List<string>();
		foreach(var con in connections)
		{
			if(con.Item1.Equals(nodeName))
			{
				neighbors.Add(con.Item2);
			}
			else if(con.Item2.Equals(nodeName))
			{
				neighbors.Add(con.Item1);
			}
		}
		return neighbors;
	}
}
