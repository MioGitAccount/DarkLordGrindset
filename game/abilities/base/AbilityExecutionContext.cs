using Godot;
using System;

// One run of an ability by one unit. Ability resources are shared by every unit that has them,
// so anything that belongs to a single run lives here instead of on the ability.
public class AbilityExecutionContext
{
	public unit Source;
	public Ability Ability;
	public AbilityStats Stats;
	// The field the unit stood on when the ability started; map abilities stop if the unit leaves it
	public field Field;
	// Total amount moved to the player during this run
	public float Collected;
}
