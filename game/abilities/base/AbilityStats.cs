using Godot;
using System;

[GlobalClass]
public partial class AbilityStats : Resource
{
	[Export] public int Power;
	[Export] public float Cooldown;
	[Export] public int Range;
	[Export] public float Duration;
	[Export] public float TickInterval = 1;
}
