using Godot;

// The skill tree of one unit type (set on UnitTemplate.SkillTree)
[GlobalClass]
public partial class SkillTree : Resource
{
	[Export] public string Name = "Skills";
	[Export] public Godot.Collections.Array<SkillDefinition> Skills = new();
}
