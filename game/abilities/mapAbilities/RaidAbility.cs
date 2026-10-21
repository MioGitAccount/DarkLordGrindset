using Godot;
using System;

// Plunders a resource (gold) much faster than stealing, but leaves the field "Raided":
// a timed perk that stops all its resource generation. Every tick of raiding adds more raided time.
[GlobalClass]
public partial class RaidAbility : ResourceDrainAbility
{
	[Export] public FieldPerk RaidedPerk;
	// Raided time (in ticks) added to the field for every tick spent raiding
	[Export] public int RaidedTicksPerTick = 3;
	// Upper limit of the field's raided time
	[Export] public int MaxRaidedTicks = 300;
	// Only towns (Village fields) can be raided
	[Export] public bool TownsOnly = true;

	public override bool Start(AbilityExecutionContext context)
	{
		if (TownsOnly && context.Source?.currentField?.IsTown != true)
		{
			GD.Print($"{Name}: only towns can be raided");
			return false;
		}
		return base.Start(context);
	}

	protected override void OnCollected(AbilityExecutionContext context, field field, float taken)
	{
		if (RaidedPerk != null)
			field.AddTimedPerk(RaidedPerk, RaidedTicksPerTick, MaxRaidedTicks);
	}
}
