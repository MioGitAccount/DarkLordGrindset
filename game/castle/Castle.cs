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
	NeedsBase,      // its faction's base is too low (or the slot is still locked)
	WrongLayer,     // a faction building offered for another faction's slot
	MaxLevel,       // a tower or base that can't go any higher
}

// The player's castle on a field: the tower in the middle and the four faction layers around it.
// Each faction has a base (levels 1-3, one slot per level) and slots for buildings; a slot
// building is unique in the whole castle and can be demolished for half its cost back. Also the
// tower level, how far the castle sees, its income, caps and which units can be recruited there.
// Created by a field whose perks include one with Buildings (see field.Castle).
public class Castle
{
	public const int MaxSlots = BuildingDefinition.MaxLevel;
	// Share of a building's cost paid back when it is demolished
	public const float DemolishRefund = 0.5f;

	public field Field { get; }
	public BuildingTree Tree { get; }

	private readonly HashSet<BuildingDefinition> _built = new();
	public IReadOnlyCollection<BuildingDefinition> Built => _built;
	// Faction -> base level (0 = not built)
	private readonly Dictionary<Faction, int> _baseLevels = new();
	// Faction -> what stands in each of its slots (null = empty)
	private readonly Dictionary<Faction, BuildingDefinition[]> _slots = new();

	// Raised after something is built, upgraded or demolished
	public event Action Changed;
	// (unit type, starting level): the map spawns the unit on the castle's field
	public event Action<UnitTemplate, int> UnitRecruited;

	public Castle(field castleField, BuildingTree tree)
	{
		Field = castleField;
		Tree = tree;
		foreach (var faction in Factions.All)
			_slots[faction] = new BuildingDefinition[MaxSlots];
		foreach (var building in Buildings)
			if (building.BuiltAtStart && !building.IsSlotBuilding)
			{
				_built.Add(building);
				if (building.IsFactionBase)
					_baseLevels[building.Faction] = 1;
			}
	}

	public IEnumerable<BuildingDefinition> Buildings => Tree.Buildings.Where(b => b != null);
	public IEnumerable<BuildingDefinition> Towers => Buildings.Where(b => b.IsTower).OrderBy(b => b.TowerLevel);
	public IEnumerable<BuildingDefinition> SlotBuildings => Buildings.Where(b => b.IsSlotBuilding);

	public bool IsBuilt(BuildingDefinition building) => building != null && _built.Contains(building);

	public int TowerLevel => _built.Count == 0 ? 0 : _built.Max(b => b.TowerLevel);

	// The next tower to build, or the top tower once all are built
	public BuildingDefinition NextTower => Towers.FirstOrDefault(t => !IsBuilt(t)) ?? Towers.LastOrDefault();

	public float VisionRadius => Tree.BaseVision + _built.Sum(b => b.BonusVision);

	public float GoldPerTick => _built.Sum(b => b.GoldPerTick);

	// What this castle adds to PlayerAgents' caps
	public int AgentCap => _built.Sum(b => b.AgentCap);
	public int UnitCap(UnitTemplate type) =>
		_built.Where(b => b.UnitCapFor == type).Sum(b => b.UnitCap + (b.IsFactionBase ? (BaseLevel(b.Faction) - 1) * b.UnitCapPerLevel : 0));

	// Fastest healing of the castle's buildings (ticks per 1 health), 0 = none
	public int HealInterval => _built.Where(b => b.HealInterval > 0).Select(b => b.HealInterval).DefaultIfEmpty(0).Min();

	// Built buildings that hand out quests (see QuestManager)
	public IEnumerable<BuildingDefinition> QuestBuildings => _built.Where(b => b.QuestPool.Count > 0);

	// Called by the castle's field every tick
	public void CollectIncome() => PlayerResources.Add(ResourceType.Gold, GoldPerTick);

	// --- Faction bases ---------------------------------------------------------------------------

	public BuildingDefinition Base(Faction faction) => Buildings.FirstOrDefault(b => b.IsFactionBase && b.Faction == faction);

	public int BaseLevel(Faction faction) => _baseLevels.GetValueOrDefault(faction);

	// A map can say which bases stand at the start (all of them when it says nothing)
	public void SetStartingBases(IEnumerable<Faction> factions)
	{
		var keep = factions.ToHashSet();
		foreach (var faction in Factions.All)
		{
			var building = Base(faction);
			if (building == null)
				continue;
			if (keep.Contains(faction))
			{
				_built.Add(building);
				_baseLevels[faction] = Math.Max(1, BaseLevel(faction));
			}
			else
			{
				_built.Remove(building);
				_baseLevels.Remove(faction);
			}
		}
		Changed?.Invoke();
	}

	// What building (level 0) or upgrading the base costs next
	public Godot.Collections.Array<ResourceCost> NextBaseCost(Faction faction)
	{
		var building = Base(faction);
		int level = BaseLevel(faction);
		return building == null ? new() : level == 0 ? building.Cost : building.UpgradeCost(level);
	}

	// Whether the base can be built or raised to its next level right now
	public BuildingStatus BaseStatus(Faction faction)
	{
		var building = Base(faction);
		int level = BaseLevel(faction);
		if (building == null || level >= BuildingDefinition.MaxLevel)
			return BuildingStatus.MaxLevel;
		if (TowerLevel < level + 1)
			return BuildingStatus.NeedsTower;
		if (!ResourceCost.CanAfford(NextBaseCost(faction)))
			return BuildingStatus.NoResources;
		return BuildingStatus.Available;
	}

	public bool TryUpgradeBase(Faction faction)
	{
		if (BaseStatus(faction) != BuildingStatus.Available || !ResourceCost.TryPay(NextBaseCost(faction)))
			return false;
		_baseLevels[faction] = BaseLevel(faction) + 1;
		_built.Add(Base(faction));
		Changed?.Invoke();
		return true;
	}

	// --- Slots -----------------------------------------------------------------------------------

	// Open slots of a faction: one per base level
	public int SlotCount(Faction faction) => Math.Min(BaseLevel(faction), MaxSlots);

	public BuildingDefinition SlotAt(Faction faction, int slot) =>
		_slots.TryGetValue(faction, out var slots) && slot >= 0 && slot < slots.Length ? slots[slot] : null;

	// Where a built slot building stands
	public (Faction faction, int slot)? SlotOf(BuildingDefinition building)
	{
		foreach (var (faction, slots) in _slots)
			for (int i = 0; i < slots.Length; i++)
				if (slots[i] == building)
					return (faction, i);
		return null;
	}

	// Whether this slot building can go into this faction's layer right now
	public BuildingStatus SlotStatus(BuildingDefinition building, Faction faction)
	{
		if (IsBuilt(building))
			return BuildingStatus.Built;
		if (building.Faction != Faction.None && building.Faction != faction)
			return BuildingStatus.WrongLayer;
		if (building.Requires.Any(required => required != null && !IsBuilt(required)))
			return BuildingStatus.NeedsBuildings;
		if (TowerLevel < building.RequiredTowerLevel)
			return BuildingStatus.NeedsTower;
		if (BaseLevel(faction) < building.MinBaseLevel)
			return BuildingStatus.NeedsBase;
		if (!ResourceCost.CanAfford(building.Cost))
			return BuildingStatus.NoResources;
		return BuildingStatus.Available;
	}

	// Slot buildings that could ever go into this faction's layer: its own, then the neutral ones
	public IEnumerable<BuildingDefinition> SlotOptions(Faction faction) =>
		SlotBuildings.Where(b => b.Faction == faction).Concat(SlotBuildings.Where(b => b.Faction == Faction.None));

	public bool TryBuildInSlot(BuildingDefinition building, Faction faction, int slot)
	{
		if (building == null || !building.IsSlotBuilding || slot < 0 || slot >= SlotCount(faction) || SlotAt(faction, slot) != null
			|| SlotStatus(building, faction) != BuildingStatus.Available || !ResourceCost.TryPay(building.Cost))
			return false;
		_slots[faction][slot] = building;
		_built.Add(building);
		Changed?.Invoke();
		return true;
	}

	// Tears down a slot building and pays back DemolishRefund of its cost
	public bool TryDemolish(Faction faction, int slot)
	{
		var building = SlotAt(faction, slot);
		if (building == null)
			return false;
		_slots[faction][slot] = null;
		_built.Remove(building);
		foreach (var cost in building.Cost)
			if (cost != null)
				PlayerResources.Add(cost.Type, Mathf.Floor(cost.Amount * DemolishRefund));
		Changed?.Invoke();
		return true;
	}

	// --- Towers ----------------------------------------------------------------------------------

	public BuildingStatus TowerStatus(BuildingDefinition tower)
	{
		if (IsBuilt(tower))
			return tower == Towers.LastOrDefault() ? BuildingStatus.MaxLevel : BuildingStatus.Built;
		if (tower.Requires.Any(required => required != null && !IsBuilt(required)))
			return BuildingStatus.NeedsBuildings;
		if (TowerLevel < tower.RequiredTowerLevel)
			return BuildingStatus.NeedsTower;
		if (!ResourceCost.CanAfford(tower.Cost))
			return BuildingStatus.NoResources;
		return BuildingStatus.Available;
	}

	public bool TryBuildTower(BuildingDefinition tower)
	{
		if (tower == null || !tower.IsTower || TowerStatus(tower) != BuildingStatus.Available || !ResourceCost.TryPay(tower.Cost))
			return false;
		_built.Add(tower);
		Changed?.Invoke();
		return true;
	}

	// --- Recruiting ------------------------------------------------------------------------------

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
