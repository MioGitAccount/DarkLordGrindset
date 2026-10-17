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
		("Field22","Field15"),("Field22","Field23"),("Field18","Field24"),
		("Field24","Field25"),("Field25","Field26"),("Field26","Field27"),
		("Field27","Field28"),("Field25","Field29"),("Field29","Field30")

	};
	private List<unit> units = new List<unit>();
	UnitManager unitManager;

	private field selectedField = null;
	private unit selectedUnit = null;
	// The UI listens to these (wired up in game.cs)
	public event Action<field> FieldSelected;
	public event Action<unit> UnitSelected;
	public event Action<field> FieldHovered;
	// Set by game.cs; hover is ignored while the mouse is over a UI panel
	public Func<bool> IsPointerOverUI = () => false;
	private field hoveredField;
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
		var fog = GetNode<FogOfWar>("FogOfWar");
		fog.Updated += () =>
		{
			foreach (var f in fields.Values)
				f.UpdateFog(fog.GetState(f.GlobalPosition));
		};

	}
	public override void _Process(double delta)
	{
		field underMouse = null;
		if (!IsPointerOverUI())
		{
			Vector2 mousePosition = GetGlobalMousePosition();
			foreach (var node in fields.Values)
			{
				// Nothing to show for fields that were never explored
				if (node.Fog != FogState.Unexplored && node.IsPointInside(mousePosition))
				{
					underMouse = node;
					break;
				}
			}
		}
		if (underMouse == hoveredField)
			return;

		hoveredField?.SetHovered(false);
		hoveredField = underMouse;
		hoveredField?.SetHovered(true);
		FieldHovered?.Invoke(hoveredField);
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
	// Unhandled input, so clicks on UI panels don't also select things on the map
	public override void _UnhandledInput(InputEvent inputEvent)
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
			// Fields never explored can't be inspected (right-click can still move there)
			if (((field)clickedNode).Fog == FogState.Unexplored)
				return;
			// Deselect the previous field
			if (selectedField != null)
			{
				selectedField.Deselect();
			}
			selectedField = (field)clickedNode;
			selectedField.Select();
			FieldSelected?.Invoke(selectedField);
		}
		else if(clickedNode is unit){
			// Select the new node
			SelectUnit((unit)clickedNode);
		}
	}
	public void SelectUnit(unit clickedUnit)
	{
		// Select the new node
		GD.Print(clickedUnit.Name + " is selected");
		unitManager.SelectUnit(clickedUnit);	
		UnitSelected?.Invoke(clickedUnit);
	}
	private void OnNodeRightClicked(Node2D node){
		if(node is field)
		{
			if(unitManager.MoveSelectedUnitTo((field)node))
			{
				GD.Print("Moving unit to: " + node);
			}
			else
			{
				GD.Print("Cannot move unit to: " + node);
			}
			
		}
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
