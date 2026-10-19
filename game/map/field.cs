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
	// Perks of this one field on top of its type's perks (e.g. a forest with an extra ore vein)
	[Export] public Godot.Collections.Array<FieldPerk> ExtraPerks = new();
	// Perks that wear off: perk -> ticks left (e.g. "Raided")
	private readonly Dictionary<FieldPerk, int> _timedPerks = new();
	public Godot.Collections.Array<unit> presentPlayerUnits = new();
	[Export] public Godot.Collections.Array<NeutralUnit> presentNeutralUnits = new();
	// Raised when units enter or leave this field, or its fog state changes
	public event Action ContentsChanged;
	public FogState Fog { get; private set; } = FogState.Unexplored;
	// Neutral units as they were the last time a player unit could see this field
	public Godot.Collections.Array<NeutralUnit> LastSeenNeutrals { get; private set; } = new();
	// The player's castle when one of the field's perks has Buildings; null on other fields
	public Castle Castle { get; private set; }
	public bool IsPlayerOwned => Castle != null;
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
		resourcesHolder.ApplyPerks(Perks);
		foreach (var perk in Perks)
		{
			if (perk.Buildings == null)
				continue;
			Castle = new Castle(this, perk.Buildings);
			Castle.Changed += () => ContentsChanged?.Invoke();
			AddToGroup(FogOfWar.ViewerGroup); // the castle sees around itself, see Castle.VisionRadius
			break;
		}
		GlobalTickManager.Instance.OnTick += TickTimedPerks;
	}

	public override void _ExitTree()
	{
		if (GlobalTickManager.Instance != null)
			GlobalTickManager.Instance.OnTick -= TickTimedPerks;
	}

	// The field type's perks followed by this field's ExtraPerks
	public IEnumerable<FieldPerk> Perks
	{
		get
		{
			if (type != null)
				foreach (var perk in type.Perks)
					yield return perk;
			foreach (var perk in ExtraPerks)
				if (perk != null)
					yield return perk;
			foreach (var perk in _timedPerks.Keys)
				yield return perk;
		}
	}

	// Ticks until a timed perk wears off; 0 for permanent perks and perks the field doesn't have
	public int GetTimedPerkTicksLeft(FieldPerk perk)
	{
		return _timedPerks.GetValueOrDefault(perk);
	}

	// Gives the field a perk for `ticks` more ticks (stacking on time it already has, up to maxTicks)
	public void AddTimedPerk(FieldPerk perk, int ticks, int maxTicks = int.MaxValue)
	{
		bool isNew = !_timedPerks.ContainsKey(perk);
		_timedPerks[perk] = Math.Min(_timedPerks.GetValueOrDefault(perk) + ticks, maxTicks);
		if (isNew)
		{
			UpdateGeneration();
			ContentsChanged?.Invoke();
		}
	}

	private void TickTimedPerks()
	{
		if (_timedPerks.Count == 0)
			return;
		bool removed = false;
		foreach (var perk in new List<FieldPerk>(_timedPerks.Keys))
		{
			int left = _timedPerks[perk] - 1;
			if (left > 0)
				_timedPerks[perk] = left;
			else
			{
				_timedPerks.Remove(perk);
				removed = true;
			}
		}
		if (removed)
		{
			UpdateGeneration();
			ContentsChanged?.Invoke();
		}
	}

	private void UpdateGeneration()
	{
		bool stopped = false;
		foreach (var perk in Perks)
			stopped |= perk.StopsGeneration;
		resourcesHolder.GenerationStopped = stopped;
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
	// Neutral units killed in battle
	public void RemoveNeutralUnits(IEnumerable<NeutralUnit> killed)
	{
		bool removed = false;
		foreach (var neutral in killed)
			removed |= presentNeutralUnits.Remove(neutral);
		if (!removed)
			return;
		if (Fog == FogState.Visible)
			LastSeenNeutrals = presentNeutralUnits.Duplicate();
		ContentsChanged?.Invoke();
	}

	// Lets the UI know something about the field's units changed (e.g. damage taken in battle)
	public void NotifyContentsChanged() => ContentsChanged?.Invoke();

	public void RemovePlayerUnit(unit unitToRemove)
	{
		if (presentPlayerUnits.Remove(unitToRemove))
			ContentsChanged?.Invoke();
	}

}
