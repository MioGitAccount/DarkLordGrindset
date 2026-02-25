using Godot;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
[GlobalClass]
public partial class ResourceDrainAbility : TickingAbility
{
	[Export] public int DrainAmount = 1; // Amount of resource to drain each tick
	public override void OnTick(AbilityExecutionContext context)
	{
		var field = context.Source.currentField as field;
		if (field != null)
		{
			float drained = field.resourcesHolder.tryDrainResource(ResourceType.Wood,context.Source,DrainAmount);

			if(drained > 0)
			{
				//context.Source.OwnerResources.AddWood(context.Stats.Power);
			}
			else
			{
				Stop();
			}

		}
	}
	public override void Execute(AbilityExecutionContext context)
	{
		OnTick(context);
	}

}
