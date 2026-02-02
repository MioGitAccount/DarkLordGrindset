using Godot;
using System;

[GlobalClass]
public partial class NeutralUnit : Resource
{
	[Export] public UnitTemplate Template;
	[Export] public BattleStats OverrideStats;
	
	public BattleStats GetEffectiveStats()
	{
		return OverrideStats ?? Template?.DefaultStats;
	}

	public Texture2D GetIcon()
	{
		return Template?.Icon;
	}

	
}
