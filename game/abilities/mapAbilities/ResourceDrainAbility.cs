using Godot;
using System;

// Takes a resource from the field the unit stands on and gives it to the player, every tick,
// until the field runs dry or the unit moves away. Used for cutting trees, mining, stealing, ...
[GlobalClass]
public partial class ResourceDrainAbility : TickingAbility
{
	[Export] public ResourceType ResourceType = ResourceType.Wood;
	[Export] public float DrainAmount = 1; // taken from the field each tick

	public override bool Start(AbilityExecutionContext context)
	{
		context.Field = context.Source?.currentField;
		var resource = context.Field?.resourcesHolder?.GetResource(ResourceType);
		if (resource == null || resource.CurrentAmount <= 0)
		{
			GD.Print($"{Name}: no {ResourceType} on this field");
			return false;
		}
		return true;
	}

	public override bool OnTick(AbilityExecutionContext context)
	{
		var field = context.Field;
		if (field == null || context.Source?.currentField != field)
			return false; // the unit left the field

		var resource = field.resourcesHolder?.GetResource(ResourceType);
		float taken = resource?.Take(DrainAmount) ?? 0;
		if (taken <= 0)
			return false;

		PlayerResources.Add(ResourceType, taken);
		context.Collected += taken;
		OnCollected(context, field, taken);
		return true;
	}

	// Hook for abilities that do more than collect (see RaidAbility)
	protected virtual void OnCollected(AbilityExecutionContext context, field field, float taken)
	{
	}
}
