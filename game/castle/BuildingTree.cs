using Godot;

// Every building a castle can have (set on the castle's FieldPerk.Buildings)
[GlobalClass]
public partial class BuildingTree : Resource
{
	[Export] public string Name = "Castle";
	// Vision range of the castle before any tower bonus
	[Export] public float BaseVision = 300f;
	[Export] public Godot.Collections.Array<BuildingDefinition> Buildings = new();
}
