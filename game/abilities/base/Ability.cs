using Godot;
using System;
[GlobalClass]
public abstract partial class Ability : Resource
{
	[Export] public string Name;
	[Export(PropertyHint.MultilineText)] public string Description = "";
	[Export] public AbilityStats BaseStats;
	[Export] public Texture2D Icon;

	public abstract void Execute(AbilityExecutionContext context);
}
