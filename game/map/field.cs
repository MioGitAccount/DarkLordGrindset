using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

public partial class field : Node2D
{
	public List<field> Neighbors = new List<field>(); // Connected nodes
	private bool isSelected = false;
	private bool isHovered = false;
	private bool hasMarker = false;
	private const float MarkerScale = 0.5f; // 64px biome marker drawn at 32px
	private float radius = 20f; // Set a radius for detection
	[Export] public string TypeName = "Default"; // Set in Inspector
	public fieldType type { get; private set; }
	private Godot.Sprite2D sprite{ get;  set; }
	[Export] public ResourcesHolder resourcesHolder{ get; set; }
	public Godot.Collections.Array<unit> presentPlayerUnits = new();
	[Export] public Godot.Collections.Array<NeutralUnit> presentNeutralUnits = new();
	// Raised when units enter or leave this field, or its fog state changes
	public event Action ContentsChanged;
	public FogState Fog { get; private set; } = FogState.Unexplored;
	// Neutral units as they were the last time a player unit could see this field
	public Godot.Collections.Array<NeutralUnit> LastSeenNeutrals { get; private set; } = new();
	public override void _Ready()
	{
		type = FieldTypeManager.GetFieldType(TypeName);
		sprite = GetNode<Godot.Sprite2D>("Sprite2D"); // Get the child Sprite2D
		var marker = FieldMarkers.Get(type);
		if (marker != null)
		{
			sprite.Texture = marker;
			hasMarker = true;
		}
		UpdateMarker();
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
		UpdateMarker();
	}

	public void Deselect()
	{
		isSelected = false;
		UpdateMarker();
	}

	public void SetHovered(bool hovered)
	{
		isHovered = hovered;
		UpdateMarker();
	}

	private float Grow => isSelected ? 1.2f : isHovered ? 1.1f : 1f;

	private void UpdateMarker()
	{
		// Field nodes are scaled unevenly in the map scene; undo that so the marker stays round
		float size = (hasMarker ? MarkerScale : 0.8f) * Grow;
		sprite.Scale = new Vector2(size / Scale.X, size / Scale.Y);
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (!isSelected && !isHovered)
			return;

		DrawSetTransform(Vector2.Zero, 0, new Vector2(1 / Scale.X, 1 / Scale.Y));
		float r = FieldMarkers.Size / 2f * MarkerScale * Grow + 2.5f;
		if (isSelected)
			DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 48, new Color(1f, 0.82f, 0.35f, 0.95f), 2.5f, true);
		else
			DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 48, new Color(1f, 1f, 1f, 0.6f), 1.5f, true);
	}

	public bool IsPointInside(Vector2 point)
	{
		return Position.DistanceTo(point) <= radius; // Check if within range
	}

	public void UpdateFog(FogState state)
	{
		if (state == FogState.Visible)
			LastSeenNeutrals = presentNeutralUnits.Duplicate();
		if (state == Fog)
			return;
		Fog = state;
		ContentsChanged?.Invoke();
	}

	public void AddPlayerUnit(unit newUnit)
	{
		presentPlayerUnits.Add(newUnit);
		ContentsChanged?.Invoke();
	}
	public void RemovePlayerUnit(unit unitToRemove)
	{
		if (presentPlayerUnits.Remove(unitToRemove))
			ContentsChanged?.Invoke();
	}

}
