using Godot;
using System;

// An ability that keeps working once per second until it has nothing left to do
// or the unit is given another order. AbilityRunner drives it.
public abstract partial class TickingAbility : Ability
{
	// Return false when the ability can't be used right now (the run doesn't start)
	public virtual bool Start(AbilityExecutionContext context)
	{
		return true;
	}

	public virtual void Stop(AbilityExecutionContext context)
	{
	}

	// One second of work. Return false to finish the run.
	public abstract bool OnTick(AbilityExecutionContext context);

	public override void Execute(AbilityExecutionContext context)
	{
		OnTick(context);
	}
}
