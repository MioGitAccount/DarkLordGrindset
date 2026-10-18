using Godot;
using System;

[GlobalClass]
public partial class NeutralUnit : Resource
{
	[Export] public UnitTemplate Template;
	[Export] public BattleStats OverrideStats;
	[Export] public SpecialAttributes OverrideAttributes;

	private BattleStats _effectiveStats;

	public SpecialAttributes GetAttributes()
	{
		return OverrideAttributes ?? Template?.Attributes;
	}

	// Base stats plus attribute bonuses; built once so damage taken later sticks to this unit
	public BattleStats GetEffectiveStats()
	{
		var baseStats = OverrideStats ?? Template?.DefaultStats;
		var attributes = GetAttributes();
		if (_effectiveStats == null && baseStats != null)
			_effectiveStats = attributes != null ? attributes.ApplyTo(baseStats) : baseStats;
		return _effectiveStats;
	}

	public Texture2D GetIcon()
	{
		return Template?.Icon;
	}


}
