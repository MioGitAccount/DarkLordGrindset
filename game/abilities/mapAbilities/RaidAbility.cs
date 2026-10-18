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

	protected override void OnCollected(AbilityExecutionContext context, field field, float taken)
	{
		if (RaidedPerk != null)
			field.AddTimedPerk(RaidedPerk, RaidedTicksPerTick, MaxRaidedTicks);
	}
}
