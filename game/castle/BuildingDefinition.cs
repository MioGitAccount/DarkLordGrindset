using Godot;
using System.Collections.Generic;

// One building card of a castle's BuildingTree. Which of them are built lives in the field's Castle.
[GlobalClass]
public partial class BuildingDefinition : Resource
{
	public const int MinLevel = 1;
	public const int MaxLevel = 3;

	[Export] public string Name = "Building";
	[Export(PropertyHint.MultilineText)] public string Description = "";
	[Export] public Texture2D Icon;

	// Row in the build window, 1 (bottom) to 3 (top); needs a Tower of the same level
	[Export(PropertyHint.Range, "1,3,1")] public int Level = 1;
	// Horizontal slot in the build window (0 = leftmost)
	[Export(PropertyHint.Range, "0,6,1")] public int Column;
	[Export] public Godot.Collections.Array<ResourceCost> Cost = new();
	// All of these must be built first
	[Export] public Godot.Collections.Array<BuildingDefinition> Requires = new();
	// Standing from the start of the game (the Tower I)
	[Export] public bool BuiltAtStart;

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
	// Units of this type recruited at the castle start this many levels higher (all types when empty)
	[Export] public UnitTemplate BonusLevelFor;
	[Export(PropertyHint.Range, "0,10,1")] public int BonusRecruitLevel;

	// A tower is unlocked by the tower below it, every other building by the tower of its own level
	public int RequiredTowerLevel => TowerLevel >= Level ? Level - 1 : Level;

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
		return lines;
	}
}
