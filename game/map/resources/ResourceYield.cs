using Godot;

// What a FieldPerk does for one resource on its field. A field adds up the yields of all
// its perks: a resource exists on the field only when its total Capacity is above 0.
[GlobalClass]
public partial class ResourceYield : Resource
{
	[Export] public ResourceType Type = ResourceType.Wood;
	// Generated on the field every global tick
	[Export] public float PerTick;
	// Added to the most of this resource the field can hold
	[Export] public float Capacity;
}
