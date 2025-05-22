using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

public partial class field : Node2D
{
	public List<field> Neighbors = new List<field>(); // Connected nodes
	private bool isSelected = false;
	private float radius = 20f; // Set a radius for detection
	[Export] public string TypeName = "Default"; // Set in Inspector
	public fieldType type { get; private set; }
	private Godot.Sprite2D sprite{ get;  set; }
	[Export] public ResourcesHolder resourcesHolder{ get; set; }
	public Godot.Collections.Array<unit> presentPlayerUnits = new();
	[Export] public Godot.Collections.Array<NeutralUnit> presentNeutralUnits = new();
	public override void _Ready()
	{
		type = FieldTypeManager.GetFieldType(TypeName);
		sprite = GetNode<Godot.Sprite2D>("Sprite2D"); // Get the child Sprite2D
		sprite.Scale = new Vector2(0.8f, 0.8f);
		resourcesHolder = GetNode<ResourcesHolder>("ResourcesHolder");
		resourcesHolder.setResourcesBasedOnType(this.type.FieldTypeEnum);
	}
	public void AddNeighbor(field node)
	{
		if (!Neighbors.Contains(node))
			Neighbors.Add(node);
	}
	public void Select()
	{
		isSelected = true;
		sprite.Scale = new Vector2(1.0f, 1.0f); 
	}

	public void Deselect()
	{
		isSelected = false;
		sprite.Scale = new Vector2(0.8f, 0.8f); 
	}

	public bool IsPointInside(Vector2 point)
	{
		return Position.DistanceTo(point) <= radius; // Check if within range
	}

}
