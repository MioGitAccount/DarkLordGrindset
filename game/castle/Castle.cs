using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public enum BuildingStatus
{
	Built,
	Available,      // can be built right now
	NoResources,    // requirements met, but the player can't pay for it
	NeedsTower,     // the tower is too low for the building's level
	NeedsBuildings, // a required building isn't built yet
}

// The player's castle on a field: which buildings stand, the tower level they give, how far
// the castle sees and which units can be recruited there. Created by a field whose perks
// include one with Buildings (see field.Castle).
public class Castle
{
	public field Field { get; }
	public BuildingTree Tree { get; }

	private readonly HashSet<BuildingDefinition> _built = new();
	public IReadOnlyCollection<BuildingDefinition> Built => _built;

	// Raised after a building is built
	public event Action Changed;
	// (unit type, starting level): the map spawns the unit on the castle's field
	public event Action<UnitTemplate, int> UnitRecruited;

	public Castle(field castleField, BuildingTree tree)
	{
		Field = castleField;
		Tree = tree;
		foreach (var building in tree.Buildings)
			if (building != null && building.BuiltAtStart)
				_built.Add(building);
	}

	public IEnumerable<BuildingDefinition> Buildings => Tree.Buildings.Where(b => b != null);

	public bool IsBuilt(BuildingDefinition building) => building != null && _built.Contains(building);

	public int TowerLevel => _built.Count == 0 ? 0 : _built.Max(b => b.TowerLevel);

	public float VisionRadius => Tree.BaseVision + _built.Sum(b => b.BonusVision);

	public float GoldPerTick => _built.Sum(b => b.GoldPerTick);

	// What this castle adds to PlayerAgents' caps
	public int AgentCap => _built.Sum(b => b.AgentCap);
	public int UnitCap(UnitTemplate type) => _built.Where(b => b.UnitCapFor == type).Sum(b => b.UnitCap);

	// Called by the castle's field every tick
	public void CollectIncome() => PlayerResources.Add(ResourceType.Gold, GoldPerTick);

	public BuildingStatus GetStatus(BuildingDefinition building)
	{
		if (IsBuilt(building))
			return BuildingStatus.Built;
		if (building.Requires.Any(required => required != null && !IsBuilt(required)))
			return BuildingStatus.NeedsBuildings;
		if (TowerLevel < building.RequiredTowerLevel)
			return BuildingStatus.NeedsTower;
		if (!ResourceCost.CanAfford(building.Cost))
			return BuildingStatus.NoResources;
		return BuildingStatus.Available;
	}

	public bool TryBuild(BuildingDefinition building)
	{
		if (building == null || GetStatus(building) != BuildingStatus.Available)
			return false;
		ResourceCost.TryPay(building.Cost);
		_built.Add(building);
		Changed?.Invoke();
		return true;
	}

	// Level a newly recruited unit of this type starts at: 1 plus the bonuses of built buildings
	public int RecruitLevel(UnitTemplate template)
	{
		int level = 1;
		foreach (var building in _built)
			if (building.BonusRecruitLevel != 0 && (building.BonusLevelFor == null || building.BonusLevelFor == template))
				level += building.BonusRecruitLevel;
		return Math.Min(level, UnitProgression.MaxLevel);
	}

	public bool CanRecruit(BuildingDefinition building) =>
		IsBuilt(building) && building.RecruitsUnit != null && ResourceCost.CanAfford(building.RecruitCost)
		&& PlayerAgents.RecruitBlockedReason(building.RecruitsUnit) == null;

	public bool TryRecruit(BuildingDefinition building)
	{
		if (!CanRecruit(building) || !ResourceCost.TryPay(building.RecruitCost))
			return false;
		UnitRecruited?.Invoke(building.RecruitsUnit, RecruitLevel(building.RecruitsUnit));
		return true;
	}
}
