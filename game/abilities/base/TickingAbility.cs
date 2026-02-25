using Godot;
using System;

public abstract partial class TickingAbility : Ability
{
	public bool IsActive{get; protected set;}

	public virtual void Start(AbilityExecutionContext context)
	{
		IsActive = true;
	}

	public virtual void Stop()
	{
		IsActive = false;
	}

	public abstract void OnTick(AbilityExecutionContext context);
}
