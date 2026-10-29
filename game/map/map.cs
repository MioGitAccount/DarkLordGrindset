using Godot;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;

public partial class map : Node2D
{
	private Dictionary<String, field> fields= new Dictionary<String, field>();
	// Built from the map file (layer 3 of the Map Editor)
	private List<(string, string)> connections = new List<(string, string)>();
	private static readonly PackedScene FieldScene = GD.Load<PackedScene>("res://game/map/field.tscn");
	// The map being played (see MapSelection)
	public MapData Data { get; private set; }
	// Where the player's starting units begin: the first Castle field
	public field StartField { get; private set; }
	private List<unit> units = new List<unit>();
	private static readonly PackedScene UnitScene = GD.Load<PackedScene>("res://game/units/unit.tscn");
	UnitManager unitManager;

	private field selectedField = null;
	private unit selectedUnit = null;
	// The UI listens to these (wired up in game.cs)
	public event Action<field> FieldSelected;
	public event Action<unit> UnitSelected;
	public event Action<field> FieldHovered;
	// A quest marker on the map was clicked
	public event Action<Quest> QuestClicked;
	// Every quest on this map (random and story quests)
	public QuestManager Quests { get; private set; }
	private QuestMarkers _questMarkers;
	public IEnumerable<field> Fields => fields.Values;
	// Set by game.cs; hover is ignored while the mouse is over a UI panel
	public Func<bool> IsPointerOverUI = () => false;
	private field hoveredField;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Data = MapSelection.LoadSelected() ?? new MapData();
		PlayerAgents.Reset();
		var view = new MapView { Name = "MapView", ShowBehindParent = true };
		AddChild(view);
		MoveChild(view, 0);
		view.SetMap(Data);
		CreateFields();
		// Fields placed by hand in the scene still work too
		foreach (Node node in GetChildren())
		{
			if (node is field placed)
				fields.TryAdd(node.Name, placed);
		}
		foreach (var c in Data.Connections)
			connections.Add((c[0], c[1]));
		ConnectAllNodes();
		unitManager = GetNode<UnitManager>("UnitManager");
		units = unitManager.findAllUnits();
		foreach (var existing in units)
		{
			existing.Died += OnUnitDied;
			PlayerAgents.AddUnit(existing);
		}
		StartField = fields.Values.FirstOrDefault(f => f.TypeName == "Castle") ?? fields.Values.FirstOrDefault();
		if (StartField != null)
			unitManager.setUnitLocation(units.Where(u => !fields.ContainsKey(u.StartFieldId)).ToList(), StartField);
		foreach (var placed in units.Where(u => fields.ContainsKey(u.StartFieldId)))
		{
			placed.Position = fields[placed.StartFieldId].Position;
			placed.EnterField(fields[placed.StartFieldId], fade: false);
		}
		foreach (var f in fields.Values)
		{
			var castleField = f;
			if (castleField.Castle == null)
				continue;
			castleField.Castle.UnitRecruited += (template, level) => SpawnUnit(template, castleField, level);
			PlayerAgents.AddCastle(castleField.Castle);
		}
		GD.Print("There are " + fields.Count + " fields on map");
		QueueRedraw();
		var fog = GetNode<FogOfWar>("FogOfWar");
		fog.Setup(Data.Size);
		MoveChild(fog, GetChildCount() - 1); // fog covers the fields created above
		fog.Updated += () =>
		{
			foreach (var f in fields.Values)
				f.UpdateFog(fog.GetState(f.GlobalPosition));
		};
		CreateQuests();

	}
	private void CreateQuests()
	{
		Quests = new QuestManager { Name = "Quests" };
		AddChild(Quests);
		Quests.SetFields(fields.Values);
		Quests.SpawnUnit = SpawnUnit;
		Quests.PlayerUnits = () => units;
		Quests.Home = StartField;
		foreach (var data in Data.Fields)
		{
			if (data.Quests == null || data.Id == null || !fields.TryGetValue(data.Id, out var place))
				continue;
			foreach (var name in data.Quests)
			{
				var definition = MapCatalog.LoadQuest(name);
				if (definition != null)
					Quests.AddStoryQuest(definition, place);
				else
					GD.PushWarning($"Field {data.Id}: unknown quest {name}");
			}
		}
		// Above the fog, so quests on grey fields stay visible
		_questMarkers = new QuestMarkers { Name = "QuestMarkers" };
		AddChild(_questMarkers);
		_questMarkers.Setup(Quests);
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

			var quest = mouseEvent.ButtonIndex == MouseButton.Left ? _questMarkers?.QuestAt(mousePosition) : null;
			if (quest != null)
			{
				OnNodeClicked(quest.Field);
				QuestClicked?.Invoke(quest);
				return;
			}
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

	// A new player unit standing on `place` (e.g. recruited in a castle), already at `level`
	public unit SpawnUnit(UnitTemplate template, field place, int level = 1)
	{
		var newUnit = UnitScene.Instantiate<unit>();
		newUnit.Template = template;
		newUnit.Name = template?.Name ?? "Unit";
		newUnit.Position = place.Position;
		GetNode("UnitList").AddChild(newUnit, forceReadableName: true);
		newUnit.Progression.LevelUpTo(level);
		units.Add(newUnit);
		newUnit.Died += OnUnitDied;
		PlayerAgents.AddUnit(newUnit);
		newUnit.EnterField(place, fade: false);
		GD.Print($"Recruited {newUnit.Name} (level {level}) at {place.Name}");
		return newUnit;
	}

	private void OnUnitDied(unit deadUnit)
	{
		units.Remove(deadUnit);
		PlayerAgents.RemoveUnit(deadUnit);
		if (unitManager.selectedUnit == deadUnit)
			unitManager.selectedUnit = null;
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
	 
	// One field node per field in the map file
	private void CreateFields()
	{
		foreach (var data in Data.Fields)
		{
			if (string.IsNullOrEmpty(data.Id) || fields.ContainsKey(data.Id))
				continue;
			var newField = FieldScene.Instantiate<field>();
			newField.Name = data.Id;
			newField.TypeName = data.Type;
			newField.Position = MapData.V(data.Pos);
			foreach (var perkName in data.Perks ?? new List<string>())
			{
				var perk = MapCatalog.LoadPerk(perkName);
				if (perk != null)
					newField.ExtraPerks.Add(perk);
			}
			foreach (var neutral in data.Neutrals ?? new List<NeutralData>())
			{
				var template = MapCatalog.LoadUnitTemplate(neutral.Template);
				if (template == null)
					continue;
				for (int i = 0; i < neutral.Count; i++)
					newField.presentNeutralUnits.Add(new NeutralUnit { Template = template });
			}
			AddChild(newField);
			fields[data.Id] = newField;
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
				if (fields.ContainsKey(s))
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
