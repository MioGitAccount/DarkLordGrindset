using Godot;

// A named group of skills, shown as one section of a skill tree. A unit's tree has a main class of
// its own (the centre section) and two subclasses; subclasses (Assassin, Sorcerer, ...) are shared,
// so several unit types can list the same class and learn the same skills, each unit on its own.
// A skill's Column counts from the left edge of its class's section.
[GlobalClass]
public partial class SkillClass : Resource
{
	[Export] public string Name = "Class";
	[Export(PropertyHint.MultilineText)] public string Description = "";
	[Export] public Godot.Collections.Array<SkillDefinition> Skills = new();
}
