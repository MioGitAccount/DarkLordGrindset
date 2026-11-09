using Godot;
using System.Collections.Generic;
using System.Linq;

// The skill tree of one unit type (set on UnitTemplate.SkillTree): its own main class in the
// middle and two shared subclasses beside it. Older trees list their skills directly in Skills
// and show as one section.
[GlobalClass]
public partial class SkillTree : Resource
{
	[Export] public string Name = "Skills";
	[Export] public SkillClass MainClass;
	// Shown left and right of the main class
	[Export] public Godot.Collections.Array<SkillClass> SubClasses = new();
	// Skills outside any class (trees not split into classes yet)
	[Export] public Godot.Collections.Array<SkillDefinition> Skills = new();

	public bool HasClasses => MainClass != null;

	// Sections left to right: first subclass, main class, second subclass
	public List<SkillClass> SectionOrder()
	{
		var sections = new List<SkillClass>();
		var subs = SubClasses.Where(c => c != null).ToList();
		if (subs.Count > 0)
			sections.Add(subs[0]);
		if (MainClass != null)
			sections.Add(MainClass);
		sections.AddRange(subs.Skip(1));
		return sections;
	}

	// Every skill of the tree: those of its classes and the loose ones
	public IEnumerable<SkillDefinition> AllSkills() =>
		SectionOrder().SelectMany(c => c.Skills).Concat(Skills).Where(s => s != null).Distinct();
}
