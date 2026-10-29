using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

// A whole world map as stored in a .json map file (see maps/MAP_FORMAT.md).
// Coordinates are world pixels, (0,0) is the top-left corner of the map.
// Layer 1: Terrain (painted in order, later shapes on top)
// Layer 2: Objects (decoration: trees, mountains, castles, ...)
// Layer 3: Fields (the nodes units walk between) and their Connections
public class MapData
{
	public int Version { get; set; } = 1;
	public string Name { get; set; } = "Untitled";
	public float Width { get; set; } = 3072;
	public float Height { get; set; } = 2048;
	// Terrain covering the whole map before any shape is drawn
	public string Base { get; set; } = "ocean";
	// Seed for the organic wobble of shape edges
	public int Seed { get; set; } = 1;
	// Optional reference image drawn under the map in the editor only (for tracing)
	public string Underlay { get; set; }
	public List<TerrainShape> Terrain { get; set; } = new();
	public List<MapObject> Objects { get; set; } = new();
	public List<FieldData> Fields { get; set; } = new();
	public List<string[]> Connections { get; set; } = new();

	public Vector2 Size => new Vector2(Width, Height);

	private static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
		WriteIndented = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		PropertyNameCaseInsensitive = true,
	};

	public static MapData FromJson(string json)
	{
		var data = JsonSerializer.Deserialize<MapData>(json, Options) ?? new MapData();
		data.Terrain ??= new();
		data.Objects ??= new();
		data.Fields ??= new();
		data.Connections ??= new();
		data.Terrain.RemoveAll(s => s == null);
		data.Objects.RemoveAll(o => o == null);
		data.Fields.RemoveAll(f => f == null);
		data.Connections.RemoveAll(c => c == null || c.Length != 2);
		return data;
	}

	public string ToJson()
	{
		string json = JsonSerializer.Serialize(this, Options);
		// One line per point list / pair instead of one line per number, so files stay readable
		json = Regex.Replace(json, @"\[[^\[\]\{\}]*\]", m => Regex.Replace(m.Value, @"\s*([\[\],])\s*", "$1"));
		json = Regex.Replace(json, @"\[(\s*\[[^\[\]\{\}]*\]\s*,?)+\s*\]", m => Regex.Replace(m.Value, @"\s*([\[\],])\s*", "$1"));
		return json;
	}

	// path: res://, user:// or an absolute file system path
	public static MapData Load(string path)
	{
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PushError($"Map: can't open {path} ({FileAccess.GetOpenError()})");
			return null;
		}
		try
		{
			return FromJson(file.GetAsText());
		}
		catch (JsonException e)
		{
			GD.PushError($"Map: {path} is not a valid map file: {e.Message}");
			return null;
		}
	}

	public bool Save(string path)
	{
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			GD.PushError($"Map: can't write {path} ({FileAccess.GetOpenError()})");
			return false;
		}
		file.StoreString(ToJson());
		return true;
	}

	public MapData Clone() => FromJson(ToJson());

	public FieldData FindField(string id) => Fields.FirstOrDefault(f => f.Id == id);

	public bool AreConnected(string a, string b) =>
		Connections.Any(c => (c[0] == a && c[1] == b) || (c[0] == b && c[1] == a));

	// Adds the connection if missing, removes it if present; returns true when it now exists
	public bool ToggleConnection(string a, string b)
	{
		if (a == b)
			return false;
		int removed = Connections.RemoveAll(c => (c[0] == a && c[1] == b) || (c[0] == b && c[1] == a));
		if (removed > 0)
			return false;
		Connections.Add(new[] { a, b });
		return true;
	}

	public void RemoveField(FieldData field)
	{
		Fields.Remove(field);
		Connections.RemoveAll(c => c[0] == field.Id || c[1] == field.Id);
	}

	public void RenameField(FieldData field, string newId)
	{
		foreach (var c in Connections)
			for (int i = 0; i < 2; i++)
				if (c[i] == field.Id)
					c[i] = newId;
		field.Id = newId;
	}

	public string NewFieldId()
	{
		int n = Fields.Count + 1;
		while (FindField($"F{n}") != null)
			n++;
		return $"F{n}";
	}

	// Problems worth telling the author about (unknown types, broken connections, ...)
	public List<string> Validate()
	{
		var problems = new List<string>();
		if (Width < 256 || Height < 256)
			problems.Add($"map size {Width}x{Height} is too small");
		if (!MapCatalog.TerrainTypes.ContainsKey(Base))
			problems.Add($"base terrain '{Base}' is unknown");
		for (int i = 0; i < Terrain.Count; i++)
		{
			var s = Terrain[i];
			if (!MapCatalog.TerrainTypes.ContainsKey(s.Type ?? ""))
				problems.Add($"terrain[{i}]: unknown type '{s.Type}'");
			if (s.Shape is not ("polygon" or "stroke" or "circle"))
				problems.Add($"terrain[{i}]: unknown shape '{s.Shape}'");
			// A stroke with one point is a round dab (a single click with the brush)
			else if (s.Shape == "circle" ? s.Center == null : (s.Points == null || s.Points.Count < (s.Shape == "polygon" ? 3 : 1)))
				problems.Add($"terrain[{i}]: {s.Shape} is missing its points");
		}
		for (int i = 0; i < Objects.Count; i++)
			if (!MapCatalog.ObjectTypes.ContainsKey(Objects[i].Type ?? ""))
				problems.Add($"objects[{i}]: unknown type '{Objects[i].Type}'");
		var ids = new HashSet<string>();
		foreach (var f in Fields)
		{
			if (string.IsNullOrEmpty(f.Id) || !ids.Add(f.Id))
				problems.Add($"field id '{f.Id}' is empty or used twice");
			if (FieldTypeManager.GetFieldType(f.Type ?? "") == null)
				problems.Add($"field {f.Id}: unknown type '{f.Type}'");
			foreach (var perk in f.Perks ?? new())
				if (!MapCatalog.PerkNames.Contains(perk))
					problems.Add($"field {f.Id}: unknown perk '{perk}'");
			foreach (var n in f.Neutrals ?? new())
				if (MapCatalog.LoadUnitTemplate(n.Template) == null)
					problems.Add($"field {f.Id}: unknown unit template '{n.Template}'");
		}
		foreach (var c in Connections)
			if (!ids.Contains(c[0]) || !ids.Contains(c[1]))
				problems.Add($"connection {c[0]} - {c[1]} points to a missing field");
		return problems;
	}

	public static float[] P(Vector2 v) => new[] { MathF.Round(v.X, 1), MathF.Round(v.Y, 1) };
	public static Vector2 V(float[] p) => p == null || p.Length < 2 ? Vector2.Zero : new Vector2(p[0], p[1]);
}

// One piece of painted terrain
public class TerrainShape
{
	public string Type { get; set; }
	// "polygon": filled area, "stroke": line with Width (rivers, roads, brush paint), "circle": Center + Radius
	public string Shape { get; set; } = "polygon";
	public List<float[]> Points { get; set; }
	public float Width { get; set; }
	public float[] Center { get; set; }
	public float Radius { get; set; }
	// Smooth corners and wobble the edges a bit so hand-typed shapes look natural (default on)
	public bool? Rough { get; set; }

	[JsonIgnore] public bool IsRough => Rough ?? true;
}

// A decoration on the map
public class MapObject
{
	public string Type { get; set; }
	public float[] Pos { get; set; }
	public float Scale { get; set; } = 1f;
	// Picks one of the type's looks (any number)
	public int Variant { get; set; }
	public bool Flip { get; set; }
	// Degrees, only used by objects that can turn (bridges)
	public float Rot { get; set; }
}

// A node on the map that units can stand on
public class FieldData
{
	public string Id { get; set; }
	// A FieldTypeManager type: Castle, SpringForest, Village, ...
	public string Type { get; set; } = "GrassPlains";
	public float[] Pos { get; set; }
	// Extra perks on top of the type's (files in resources/field_perks without .tres)
	public List<string> Perks { get; set; }
	public List<NeutralData> Neutrals { get; set; }
	// Story quests on this field, no timer (files in resources/quests without .tres)
	public List<string> Quests { get; set; }
}

public class NeutralData
{
	// Unit template name: a file in unit_templates/neutral_unit_templates or unit_templates
	public string Template { get; set; }
	public int Count { get; set; } = 1;
}
