// The four factions the Dark Lord unites. Each has its own layer in the castle: a base building
// and up to 3 slots. None = a neutral building, which fits a slot of any layer.
public enum Faction
{
	None,
	Chaos,
	Stronghold,
	DarkHumans,
	Undead,
}

public static class Factions
{
	// Order of the layers in the build window: two left of the tower, two right of it
	public static readonly Faction[] All = { Faction.Chaos, Faction.Stronghold, Faction.DarkHumans, Faction.Undead };

	public static string Name(Faction faction) => faction switch
	{
		Faction.Chaos => "Chaos",
		Faction.Stronghold => "Stronghold",
		Faction.DarkHumans => "Dark Humans",
		Faction.Undead => "Undead",
		_ => "Neutral",
	};
}
