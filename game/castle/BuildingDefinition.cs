using Godot;
using System.Collections.Generic;

// One building of a castle's BuildingTree. Which of them are built lives in the field's Castle.
// Three kinds: towers (TowerLevel > 0, the middle column), faction bases (IsFactionBase, the bottom
// of a faction's layer, levels 1-3, one slot per level) and slot buildings (everything else, built
// into a free slot of their Faction's layer, or of any layer when Faction is None).
[GlobalClass]
public partial class BuildingDefinition : Resource
{
	public const int MinLevel = 1;
	public const int MaxLevel = 3;

	[Export] public string Name = "Building";
	[Export(PropertyHint.MultilineText)] public string Description = "";
	// The building's picture on the castle screen (images/castle/); a base uses LevelIcons instead
	[Export] public Texture2D Icon;
	// Base only: its picture at levels 1, 2 and 3 (missing ones fall back to Icon)
	[Export] public Godot.Collections.Array<Texture2D> LevelIcons = new();

	// Needs a Tower of this level (a tower: of the level below)
	[Export(PropertyHint.Range, "1,3,1")] public int Level = 1;
	[Export] public Godot.Collections.Array<ResourceCost> Cost = new();
	// All of these must be built first
	[Export] public Godot.Collections.Array<BuildingDefinition> Requires = new();
	// Standing from the start of the game (the Tower I, the faction bases); a map can override bases
	[Export] public bool BuiltAtStart;
	// Seconds to build it at level 1, and how many more each level above that takes (a tower's
	// level, the level a base is raised to, a slot building's Level)
	[Export] public int BuildSeconds = 120;
	[Export] public int BuildSecondsPerLevel = 60;

	// Factions
	// A base: the layer it heads. A slot building: the layer it fits (None = any layer).
	[Export] public Faction Faction;
	// The bottom building of its faction's layer: levels 1-3, each level opens one slot
	[Export] public bool IsFactionBase;
	// Base only: what raising it to level 2 and to level 3 costs
	[Export] public Godot.Collections.Array<ResourceCost> UpgradeCostLevel2 = new();
	[Export] public Godot.Collections.Array<ResourceCost> UpgradeCostLevel3 = new();
	// Base only: added to UnitCap for every level above 1
	[Export(PropertyHint.Range, "0,10,1")] public int UnitCapPerLevel;
	// Slot building only: its faction's base must be at least this level
	[Export(PropertyHint.Range, "1,3,1")] public int MinBaseLevel = 1;

	// Effects
	// Raises the castle's tower level to this, unlocking buildings up to that level (0 = not a tower)
	[Export(PropertyHint.Range, "0,3,1")] public int TowerLevel;
	// Added to the castle's vision range (fog of war)
	[Export] public float BonusVision;
	// Gold the castle earns every tick while this stands
	[Export] public float GoldPerTick;
	// Lets the player recruit this unit at the castle
	[Export] public UnitTemplate RecruitsUnit;
	[Export] public Godot.Collections.Array<ResourceCost> RecruitCost = new();
	[Export] public int RecruitSeconds = 40;
	// Units of this type recruited at the castle start this many levels higher (all types when empty)
	[Export] public UnitTemplate BonusLevelFor;
	[Export(PropertyHint.Range, "0,10,1")] public int BonusRecruitLevel;
	// Added to how many units (agents) the player may have in total
	[Export(PropertyHint.Range, "0,20,1")] public int AgentCap;
	// Added to how many units of UnitCapFor the player may have
	[Export] public UnitTemplate UnitCapFor;
	[Export(PropertyHint.Range, "0,10,1")] public int UnitCap;
	// Player units standing on the castle field heal 1 health every this many ticks (0 = no healing)
	[Export(PropertyHint.Range, "0,60,1")] public int HealInterval;
	// Quests this building hands out on the castle (or near it, for quests of other biomes), one at
	// a time, a new one every QuestIntervalMin-Max seconds. They no longer appear at random.
	[Export] public Godot.Collections.Array<QuestDefinition> QuestPool = new();
	[Export] public int QuestIntervalMin = 180;
	[Export] public int QuestIntervalMax = 240;

	public bool IsTower => TowerLevel > 0;
	public bool IsSlotBuilding => !IsTower && !IsFactionBase;

	// The picture for this level (bases change with their level); null when there is no art yet
	public Texture2D IconAt(int level) =>
		level >= 1 && level <= LevelIcons.Count && LevelIcons[level - 1] != null ? LevelIcons[level - 1] : Icon;

	// A tower is unlocked by the tower below it, every other building by the tower of its own level
	public int RequiredTowerLevel => TowerLevel >= Level ? Level - 1 : Level;

	// Base only: what raising it from `level` to the next level costs
	public Godot.Collections.Array<ResourceCost> UpgradeCost(int level) => level <= 1 ? UpgradeCostLevel2 : UpgradeCostLevel3;

	// Short effect lines for tooltips and cards
	public List<string> EffectLines()
	{
		var lines = new List<string>();
		if (TowerLevel > 0)
			lines.Add($"Unlocks level {TowerLevel} buildings");
		if (BonusVision != 0)
			lines.Add($"+{BonusVision:0} vision range");
		if (RecruitsUnit != null)
			lines.Add($"Recruit {RecruitsUnit.Name} ({ResourceCost.Text(RecruitCost)})");
		if (BonusRecruitLevel != 0)
			lines.Add($"{BonusLevelFor?.Name ?? "Units"} recruited at +{BonusRecruitLevel} level{(BonusRecruitLevel == 1 ? "" : "s")}");
		if (AgentCap != 0)
			lines.Add($"+{AgentCap} agent cap");
		if (UnitCap != 0 && UnitCapFor != null)
			lines.Add(UnitCapPerLevel != 0
				? $"+{UnitCap} {UnitCapFor.Name} cap, +{UnitCapPerLevel} per level"
				: $"+{UnitCap} {UnitCapFor.Name} cap");
		if (IsFactionBase)
			lines.Add("One building slot per level");
		if (GoldPerTick != 0)
			lines.Add($"+{GoldPerTick:0.##} gold per tick");
		if (HealInterval > 0)
			lines.Add($"Units on the castle heal 1 health every {HealInterval} ticks");
		if (QuestPool.Count > 0)
			lines.Add($"Gives a quest every {QuestIntervalMin / 60f:0.#}-{QuestIntervalMax / 60f:0.#} min");
		return lines;
	}
}
