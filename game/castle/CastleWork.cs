using System.Collections.Generic;
using System.Linq;

// Something a castle is building or a unit it is training. Paid for when it starts (and paid
// back in full when cancelled); counts down one second per game tick (see Castle.Tick).
public class CastleWork
{
	// What is being built, or the building that trains the unit
	public BuildingDefinition Building;
	// Set when training a unit
	public UnitTemplate Unit;
	// A faction base: its layer and the level it reaches. A slot building: its layer and slot.
	public Faction Faction;
	public int Level;
	public int Slot = -1;
	public List<ResourceCost> Paid;
	public int TotalSeconds;
	public int SecondsLeft;

	public float Progress => TotalSeconds <= 0 ? 1 : 1 - SecondsLeft / (float)TotalSeconds;
	public string Name => Unit?.Name ?? (Building.IsFactionBase && Level > 1 ? $"{Building.Name} level {Level}" : Building.Name);
	public string TimeLeft => Clock(SecondsLeft);

	public static string Clock(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

	public static List<ResourceCost> Copy(IEnumerable<ResourceCost> costs) =>
		costs.Where(c => c != null).Select(c => new ResourceCost { Type = c.Type, Amount = c.Amount }).ToList();
}
