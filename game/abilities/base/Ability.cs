using Godot;
using System;
[GlobalClass]
public abstract partial class Ability : Resource
{
	[Export] public string Name;
	[Export(PropertyHint.MultilineText)] public string Description = "";
	[Export] public AbilityStats BaseStats;
	[Export] public Texture2D Icon;
	// A weaker version of this ability (e.g. Raid for Raid II); a unit that has both keeps only this one
	[Export] public Ability Replaces;
	// Work with this ability stops giving experience once the unit reaches this level (0 = never stops)
	[Export] public int MaxExperienceLevel;

	public abstract void Execute(AbilityExecutionContext context);
}
