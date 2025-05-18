using Godot;
using System;
using System.Collections.Generic;

public partial class unit : Node2D
{
	[Export] public string UnitType = "Warrior";  // Type of unit
	private Godot.Sprite2D sprite{ get;  set; }
	private bool isSelected;
	private float radius = 20f; // Set a radius for detection
	public field currentField{ get; set; }

	//MOVEMENT
	private List<field> path;
	private int pathIndex = 0;
	[Export] public float MoveSpeed = 100f;
	public bool isMoving;
	private Vector2 startPos;
	private Vector2 targetPos;
	private float moveTimer;

	public override void _Ready()
	{
		sprite = GetNode<Godot.Sprite2D>("Sprite2D");
		sprite.Scale = new Vector2(0.08f, 0.08f);
		
		
	}
	public override void _Process(double delta)
	{
		if (!isMoving) return;

		moveTimer += (float)delta;
		float t = Mathf.Clamp(moveTimer / 30f, 0f, 1f);

		// Interpolate between start and target
		Position = startPos.Lerp(targetPos, t);

		// If movement to current target is complete
		if (t >= 1f)
		{
			currentField = path[pathIndex]; 
			pathIndex++;
			MoveToNextNode(); // Go to next node
		}
	}
	public bool IsPointInside(Vector2 point)
	{
		return Position.DistanceTo(point) <= radius; // Check if within range
	}
	public void Select()
	{
		isSelected = true;
		sprite.Scale = new Vector2(0.1f, 0.1f); 
	}
	public void Deselect()
	{
		isSelected = false;
		sprite.Scale = new Vector2(0.08f, 0.08f); 
	}
	public void MoveAlongPath(List<field> newPath)
	{
		if (newPath == null || newPath.Count < 2) return;

		path = newPath;
		pathIndex = 1; // Start from the second node (first is current position)
		startPos = GlobalPosition;
		targetPos = path[pathIndex].GlobalPosition;
		moveTimer = 0f;
		isMoving = true;
	}

	private void MoveToNextNode()
	{
		if (pathIndex >= path.Count)
		{
			isMoving = false; // Reached final destination
			//update field units list
			return;
		}

		startPos = GlobalPosition;
		targetPos = path[pathIndex].GlobalPosition;
		moveTimer = 0f;
	}
	public void MoveTo(field newField)
	{
		Position = newField.Position; 
	}
	public void MoveTo(Vector2 position)
	{
		GD.Print("Moving to: " + position);
		Position = position;
	}

}
