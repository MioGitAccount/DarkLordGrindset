using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

public partial class unit : Node2D
{
	[Export] public string UnitType = "Warrior";  // Type of unit
	[Export] public UnitTemplate Template;
	[Export] public BattleStats battleStats;

	[Export] public float VisibilityRadius = 200f; // Radius for visibility

	[Export] public Godot.Collections.Array<Ability> Abilities = new Godot.Collections.Array<Ability>();
	private Godot.Sprite2D sprite { get; set; }
	public AbilityRunner abilityRunner { get; set; }
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

	private FogOfWar _fogOfWar;

	public override void _Ready()
	{
		_fogOfWar =  GetNode<FogOfWar>("/root/Game/Map/FogOfWar");
		
		sprite = GetNode<Godot.Sprite2D>("Sprite2D");
		sprite.Scale = new Vector2(0.08f, 0.08f);
		sprite.Texture = GetIcon();
		abilityRunner = GetNode<AbilityRunner>("AbilityRunner");
		RevealArea();
		
	}
	public Texture2D GetIcon()
	{
		return Template?.Icon;
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

	private void leaveCurrentField()
	{
		if (currentField != null)
		{
			currentField.RemovePlayerUnit(this);
			currentField = null;
		}
	}

	private void enterField(field newField)
	{
		if (newField != null)
		{
			newField.AddPlayerUnit(this);
			currentField = newField;
		}
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
		leaveCurrentField();
	}

	private void MoveToNextNode()
	{
		if (pathIndex >= path.Count)
		{
			isMoving = false; // Reached final destination
			enterField(path[path.Count - 1]);
			RevealArea(); // Reveal area at final position
			return;
		}

		startPos = GlobalPosition;
		targetPos = path[pathIndex].GlobalPosition;
		moveTimer = 0f;
		RevealArea(); // Reveal area at new position
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
	
	public void RevealArea()
	{
		if (_fogOfWar != null)
		{
			_fogOfWar.RevealAnimated(Position, VisibilityRadius);
		}
	}


}
