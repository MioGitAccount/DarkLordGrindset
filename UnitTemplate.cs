using Godot;
using System;
[GlobalClass]
public partial class UnitTemplate : Resource
{

	[Export] public string Name = "Imp";
	[Export] public Texture2D Icon;
	[Export] public BattleStats DefaultStats;
}
