using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// The resources lying on one field. Which resources exist, how fast they regenerate and how
// much the field can hold all come from the field's perks (see ApplyPerks).
public partial class ResourcesHolder : Node2D
{
	// A field starts with this share of its capacity
	public const float StartingFill = 0.5f;

	private readonly SortedDictionary<ResourceType, ResourceInstance> _resources = new();

	// Only resources the field actually has, in ResourceType order
	public List<ResourceInstance> Resources => _resources.Values.ToList();

	public void ApplyPerks(IEnumerable<FieldPerk> perks)
	{
		var capacity = new Dictionary<ResourceType, float>();
		var perTick = new Dictionary<ResourceType, float>();
		foreach (var perk in perks)
		{
			foreach (var yield in perk.Yields)
			{
				if (yield == null)
					continue;
				capacity[yield.Type] = capacity.GetValueOrDefault(yield.Type) + yield.Capacity;
				perTick[yield.Type] = perTick.GetValueOrDefault(yield.Type) + yield.PerTick;
			}
		}

		foreach (var (type, max) in capacity)
		{
			if (max <= 0)
				continue; // generation without anywhere to store it: the field doesn't have this resource
			float regen = perTick.GetValueOrDefault(type);
			if (_resources.TryGetValue(type, out var existing))
			{
				existing.MaxAmount = max;
				existing.RegenRate = regen;
				existing.CurrentAmount = Mathf.Min(existing.CurrentAmount, max);
			}
			else
			{
				_resources[type] = new ResourceInstance(type, max * StartingFill, max, regen);
			}
		}
	}

	// Set by the field while one of its perks stops generation
	public bool GenerationStopped
	{
		get => _generationStopped;
		set
		{
			_generationStopped = value;
			foreach (var resource in _resources.Values)
				resource.Paused = value;
		}
	}
	private bool _generationStopped;

	public ResourceInstance GetResource(ResourceType type)
	{
		return _resources.GetValueOrDefault(type);
	}
}
