using Godot;
using System.Collections.Generic;

// Where each building stands on the castle screen, from resources/castle/castle_layout.json.
// A spot is its feet (bottom centre) and its width, all as fractions of the background, so the
// background can be any size. Keys: "tower", "<Faction>/base", "<Faction>/slot1".."slot3".
// The build window's layout mode (F9, debug builds) moves the spots and saves them back here.
public class CastleLayout
{
	public const string Path = "res://resources/castle/castle_layout.json";

	public struct Spot
	{
		public float X, Y, Width;
	}

	public string Background = "res://images/castle/castle_background.png";
	// Faction names are drawn across the top at this height
	public float LabelY = 0.03f;
	public readonly Dictionary<string, Spot> Spots = new();

	public static string BaseKey(Faction faction) => $"{faction}/base";
	public static string SlotKey(Faction faction, int slot) => $"{faction}/slot{slot + 1}";
	public const string TowerKey = "tower";

	public static CastleLayout Load()
	{
		var layout = new CastleLayout();
		if (!FileAccess.FileExists(Path))
			return layout;
		var data = Json.ParseString(FileAccess.GetFileAsString(Path)).AsGodotDictionary();
		if (data.TryGetValue("background", out var background))
			layout.Background = background.AsString();
		if (data.TryGetValue("label_y", out var labelY))
			layout.LabelY = (float)labelY.AsDouble();
		if (data.TryGetValue("spots", out var spots))
			foreach (var (key, value) in spots.AsGodotDictionary())
			{
				var spot = value.AsGodotDictionary();
				layout.Spots[key.AsString()] = new Spot { X = (float)spot["x"].AsDouble(), Y = (float)spot["y"].AsDouble(), Width = (float)spot["w"].AsDouble() };
			}
		return layout;
	}

	public Spot Get(string key) => Spots.TryGetValue(key, out var spot) ? spot : new Spot { X = 0.5f, Y = 0.9f, Width = 0.1f };

	// Writes the file in the project folder (works when running from the editor)
	public void Save()
	{
		var spots = new Godot.Collections.Dictionary();
		var keys = new List<string>(Spots.Keys);
		keys.Sort();
		foreach (var key in keys)
		{
			var spot = Spots[key];
			spots[key] = new Godot.Collections.Dictionary { ["x"] = Mathf.Snapped(spot.X, 0.001), ["y"] = Mathf.Snapped(spot.Y, 0.001), ["w"] = Mathf.Snapped(spot.Width, 0.001) };
		}
		var data = new Godot.Collections.Dictionary { ["background"] = Background, ["label_y"] = LabelY, ["spots"] = spots };
		using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
		file?.StoreString(Json.Stringify(data, "\t"));
		GD.Print($"[Castle] layout saved to {Path}");
	}
}
