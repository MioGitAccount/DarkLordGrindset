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
	private AnimatedSprite2D animatedSprite;
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

	// Units are hidden while standing on a field and fade in/out when they leave/enter one
	[Export] public float FadeDuration = 1.0f;
	private float _visibility = 1f;
	private Tween _fadeTween;

	public override void _Ready()
	{
		AddToGroup(FogOfWar.ViewerGroup); // the fog reveals VisibilityRadius around this unit

		sprite = GetNode<Godot.Sprite2D>("Sprite2D");
		sprite.Scale = new Vector2(0.08f, 0.08f);
		sprite.Texture = GetIcon();
		animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite");
		if (Template?.MapSprite != null)
		{
			animatedSprite.SpriteFrames = Template.MapSprite;
			animatedSprite.Visible = true;
			sprite.Visible = false;
			animatedSprite.Play("idle");
		}
		abilityRunner = GetNode<AbilityRunner>("AbilityRunner");
	}
	public Texture2D GetIcon()
	{
		return Template?.Icon;
	}
	private bool HasAnimatedSprite => animatedSprite != null && animatedSprite.Visible;

	public override void _Draw()
	{
		if (!HasAnimatedSprite)
			return;

		// Flat ellipse at the feet: a soft shadow, plus a ring when selected.
		// Sizes are for a 64px sprite and follow the sprite's scale.
		float s = animatedSprite.Scale.Y;
		DrawSetTransform(new Vector2(0, 28 * s), 0, new Vector2(1, 0.35f));
		DrawCircle(Vector2.Zero, 18 * s, new Color(0, 0, 0, 0.35f * _visibility));
		// The ring stays visible while hidden, so the selected unit's field is still marked
		if (isSelected)
			DrawArc(Vector2.Zero, 22 * s, 0, Mathf.Tau, 40, new Color(1f, 0.8f, 0.3f), 2f);
		DrawSetTransform(Vector2.Zero, 0, Vector2.One);
	}

	private void UpdateAnimation()
	{
		if (!HasAnimatedSprite)
			return;

		StringName animation = isMoving ? "walk" : "idle";
		if (animatedSprite.Animation != animation)
			animatedSprite.Play(animation);
		if (isMoving && !Mathf.IsEqualApprox(targetPos.X, startPos.X))
			animatedSprite.FlipH = targetPos.X < startPos.X;
	}

	public override void _Process(double delta)
	{
		UpdateAnimation();
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
		if (_visibility < 0.5f)
			return false; // hidden on a field; pick it from the field panel instead
		return Position.DistanceTo(point) <= radius; // Check if within range
	}

	private void SetVisibility(float visibility)
	{
		_visibility = visibility;
		var modulate = new Color(1, 1, 1, visibility);
		sprite.Modulate = modulate;
		animatedSprite.Modulate = modulate;
		QueueRedraw();
	}

	private void FadeTo(float visibility, bool animate)
	{
		_fadeTween?.Kill();
		if (!animate || FadeDuration <= 0)
		{
			SetVisibility(visibility);
			return;
		}
		_fadeTween = CreateTween();
		_fadeTween.TweenMethod(Callable.From<float>(SetVisibility), _visibility, visibility, FadeDuration);
	}
	public void Select()
	{
		isSelected = true;
		sprite.Scale = new Vector2(0.1f, 0.1f); 
		QueueRedraw();
	}
	public void Deselect()
	{
		isSelected = false;
		sprite.Scale = new Vector2(0.08f, 0.08f); 
		QueueRedraw();
	}

	private void leaveCurrentField()
	{
		if (currentField != null)
		{
			currentField.RemovePlayerUnit(this);
			currentField = null;
			FadeTo(1f, true);
		}
	}

	public void EnterField(field newField, bool fade = true)
	{
		if (newField != null)
		{
			newField.AddPlayerUnit(this);
			currentField = newField;
			FadeTo(0f, fade);
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
			EnterField(path[path.Count - 1]);
			return;
		}

		startPos = GlobalPosition;
		targetPos = path[pathIndex].GlobalPosition;
		moveTimer = 0f;
	}
	public void UseAbility(Ability ability)
	{
		if (ability is TickingAbility tickingAbility)
		{
			abilityRunner.StartAbility(tickingAbility, new AbilityExecutionContext
			{
				Source = this,
				Ability = ability,
				Stats = ability.BaseStats
			});
		}
		else
		{
			GD.PushWarning($"Ability {ability?.Name} is not a TickingAbility and can't be started yet");
		}
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
