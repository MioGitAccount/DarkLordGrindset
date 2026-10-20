using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

public partial class unit : Node2D
{
	[Export] public string UnitType = "Warrior";  // Type of unit
	[Export] public UnitTemplate Template;
	// Base stats for this unit; falls back to Template.DefaultStats.
	// From _Ready on it holds the effective stats (base + attribute bonuses), see RecalculateStats.
	[Export] public BattleStats battleStats;
	// Per-unit attributes; falls back to Template.Attributes. From _Ready on: base + skill bonuses.
	[Export] public SpecialAttributes Attributes;

	// Field id (from the map file) the unit starts on; empty = the player's castle
	[Export] public string StartFieldId = "";

	[Export] public float VisibilityRadius = 200f; // Radius for visibility; skills can add to it

	// Abilities the unit starts with; skills add more
	[Export] public Godot.Collections.Array<Ability> Abilities = new Godot.Collections.Array<Ability>();

	// Level, experience, skill points and unlocked skills
	public UnitProgression Progression { get; private set; }
	public SkillTree SkillTree => Template?.SkillTree;
	// Bumped whenever stats, attributes, abilities or progression change, so UI knows when to refresh
	public int Revision { get; private set; }
	// Battle traits and actions from the template plus unlocked skills, see RecalculateStats
	public List<CombatPassive> CombatPassives { get; private set; } = new();
	public List<CombatAbility> CombatAbilities { get; private set; } = new();
	// Raised when the unit dies (in battle), right before it is removed from the game
	public event Action<unit> Died;

	private BattleStats _baseStats;
	private SpecialAttributes _baseAttributes;
	private float _baseVisibilityRadius;
	private Godot.Collections.Array<Ability> _baseAbilities;
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
	// Seconds to cross one field at Quickness 0; each Quickness point makes the unit 10% faster
	[Export] public float BaseFieldTravelTime = 30f;
	public float FieldTravelTime => BaseFieldTravelTime / Attributes.MoveSpeedMultiplier;
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

		// Own copies, so changing this unit's attributes or stats never touches the shared template
		_baseAttributes = (Attributes ?? Template?.Attributes)?.Duplicate() as SpecialAttributes ?? new SpecialAttributes();
		_baseStats = (battleStats ?? Template?.DefaultStats)?.Duplicate() as BattleStats ?? new BattleStats();
		_baseVisibilityRadius = VisibilityRadius;
		_baseAbilities = new Godot.Collections.Array<Ability>(Abilities);
		battleStats = null;

		Progression = new UnitProgression();
		Progression.Changed += skillsChanged =>
		{
			if (skillsChanged)
				RecalculateStats();
			else
				Revision++;
		};
		RecalculateStats();

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
	// Rebuilds attributes, stats, vision and abilities from the base values plus unlocked skills.
	// Damage taken and mana spent carry over.
	private void RecalculateStats()
	{
		var skills = new List<SkillDefinition>();
		if (SkillTree != null)
			foreach (var skill in SkillTree.Skills)
				if (Progression.IsUnlocked(skill))
					skills.Add(skill);

		var attributes = (SpecialAttributes)_baseAttributes.Duplicate();
		float vision = _baseVisibilityRadius;
		var abilities = new Godot.Collections.Array<Ability>(_baseAbilities);
		var combatPassives = new List<CombatPassive>();
		var combatAbilities = new List<CombatAbility>();
		if (Template != null)
		{
			foreach (var passive in Template.CombatPassives)
				if (passive != null)
					combatPassives.Add(passive);
			foreach (var combatAbility in Template.CombatAbilities)
				if (combatAbility != null)
					combatAbilities.Add(combatAbility);
		}
		foreach (var skill in skills)
		{
			for (int i = 0; i < SpecialAttributes.Count; i++)
				attributes.Set(i, attributes.Get(i) + skill.BonusAttribute(i));
			vision += skill.BonusVision;
			if (skill.GrantedAbility != null && !abilities.Contains(skill.GrantedAbility))
				abilities.Add(skill.GrantedAbility);
			if (skill.GrantedCombatPassive != null && !combatPassives.Contains(skill.GrantedCombatPassive))
				combatPassives.Add(skill.GrantedCombatPassive);
			if (skill.GrantedCombatAbility != null && !combatAbilities.Contains(skill.GrantedCombatAbility))
				combatAbilities.Add(skill.GrantedCombatAbility);
		}

		var stats = attributes.ApplyTo(_baseStats);
		if (battleStats != null)
		{
			if (battleStats.maxHealth > 0)
				stats.health = Math.Max(0, stats.maxHealth - (battleStats.maxHealth - battleStats.health));
			stats.mana = Math.Max(0, stats.maxMana - (battleStats.maxMana - battleStats.mana));
		}

		Attributes = attributes;
		battleStats = stats;
		VisibilityRadius = vision;
		Abilities = abilities;
		CombatPassives = combatPassives;
		CombatAbilities = combatAbilities;
		Revision++;
	}

	// Health and mana left after a battle the unit survived
	public void ApplyBattleOutcome(int health, int mana)
	{
		battleStats.health = Math.Clamp(health, 0, battleStats.maxHealth);
		battleStats.mana = Math.Clamp(mana, 0, battleStats.maxMana);
		Revision++;
	}

	// Killed in battle: leaves its field and the game
	public void Die()
	{
		abilityRunner?.StopAbility();
		currentField?.RemovePlayerUnit(this);
		currentField = null;
		Died?.Invoke(this);
		QueueFree();
	}

	public void AddExperience(int amount)
	{
		Progression.AddExperience(amount);
	}

	public bool UnlockSkill(SkillDefinition skill)
	{
		return Progression.TryUnlock(skill);
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
		float t = Mathf.Clamp(moveTimer / FieldTravelTime, 0f, 1f);

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
		abilityRunner.StopAbility(); // a new order ends whatever the unit was doing
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
	public bool IsUsingAbility(Ability ability) => ability != null && abilityRunner?.ActiveAbility == ability;

	// Starts the ability, or stops it when it is already running
	public void UseAbility(Ability ability)
	{
		if (IsUsingAbility(ability))
		{
			abilityRunner.StopAbility();
			return;
		}
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
