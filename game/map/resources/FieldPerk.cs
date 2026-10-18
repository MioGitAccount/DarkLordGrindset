using Godot;
using System.Collections.Generic;

// A trait of a field that generates resources and/or raises how much of them the field can hold,
// e.g. "Forest": +1 wood per tick, up to 300 wood. Field types get default perks in FieldTypeManager;
// single fields can get more through field.ExtraPerks.
[GlobalClass]
public partial class FieldPerk : Resource
{
	[Export] public string Name = "Perk";
	[Export(PropertyHint.MultilineText)] public string Description = "";
	[Export] public Godot.Collections.Array<ResourceYield> Yields = new();
	// While the field has this perk none of its resources grow back (e.g. "Raided")
	[Export] public bool StopsGeneration;
	// Makes the field a player castle with these buildings (see field.Castle)
	[Export] public BuildingTree Buildings;

	// e.g. "+1 Wood per tick, +300 Wood capacity"
	public string EffectText()
	{
		var parts = new List<string>();
		foreach (var yield in Yields)
		{
			if (yield == null)
				continue;
			if (yield.PerTick != 0)
				parts.Add($"+{yield.PerTick:0.##} {yield.Type} per tick");
			if (yield.Capacity != 0)
				parts.Add($"+{yield.Capacity:0} {yield.Type} capacity");
		}
		if (StopsGeneration)
			parts.Add("no resources grow back");
		if (Buildings != null)
			parts.Add("can build castle buildings");
		return string.Join(", ", parts);
	}
}
