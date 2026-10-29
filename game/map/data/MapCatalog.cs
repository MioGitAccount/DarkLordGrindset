using Godot;
using System.Collections.Generic;
using System.Linq;

// Everything a map file can name: terrain types, object types, field perks and unit templates.
// Add a new terrain or object type here and it shows up in the editor palette.
public static class MapCatalog
{
	public enum TerrainKind
	{
		Sea,    // water: coastlines and shore rings are drawn around it
		Land,
		Overlay // drawn as a soft dark stain over whatever is below (blight)
	}

	public class TerrainType
	{
		public string Id;
		public string Name;
		public TerrainKind Kind;
		public Color Dark;  // the procedural texture mixes these two
		public Color Light;
		public float BrushWidth; // default brush width in the editor
		public float Feather;    // world px over which the edge fades into the terrain below (land only)
		public string Hint;
	}

	public class ObjectType
	{
		public string Id;
		public string Name;
		public string Category;
		public float Size;           // drawn height in world pixels at scale 1
		public float ScatterSpacing; // 0 = not offered for the scatter brush
		public string SuggestsField; // field type suggested for a field dropped next to it
		public bool Turnable;        // uses MapObject.Rot
		// How the ground around it changes (see GroundInfluence): kind, radius in multiples of Size, strength
		public GroundKind Ground;
		public float GroundRadius = 1f;
		public float GroundStrength = 0.5f;
	}

	public enum GroundKind { None, Forest, Rock, Settlement }

	public static readonly Dictionary<string, TerrainType> TerrainTypes = new[]
	{
		new TerrainType { Id = "ocean", Name = "Ocean", Kind = TerrainKind.Sea, Dark = new Color(0.27f, 0.38f, 0.42f), Light = new Color(0.34f, 0.45f, 0.48f), BrushWidth = 80, Hint = "Deep sea. Usually the map's base terrain." },
		new TerrainType { Id = "fjord", Name = "Fjord", Kind = TerrainKind.Sea, Dark = new Color(0.29f, 0.4f, 0.44f), Light = new Color(0.36f, 0.47f, 0.5f), BrushWidth = 30, Hint = "Narrow sea inlet cut into land." },
		new TerrainType { Id = "water", Name = "Water", Kind = TerrainKind.Sea, Dark = new Color(0.37f, 0.49f, 0.52f), Light = new Color(0.45f, 0.57f, 0.58f), BrushWidth = 18, Hint = "Lakes and rivers." },
		new TerrainType { Id = "grass", Name = "Grass", Kind = TerrainKind.Land, Dark = new Color(0.49f, 0.54f, 0.26f), Light = new Color(0.62f, 0.64f, 0.34f), BrushWidth = 80, Feather = 16 },
		new TerrainType { Id = "meadow", Name = "Meadow", Kind = TerrainKind.Land, Dark = new Color(0.55f, 0.63f, 0.26f), Light = new Color(0.67f, 0.72f, 0.34f), BrushWidth = 60, Feather = 22, Hint = "Bright lush grass." },
		new TerrainType { Id = "dirt", Name = "Dirt", Kind = TerrainKind.Land, Dark = new Color(0.6f, 0.51f, 0.33f), Light = new Color(0.73f, 0.64f, 0.45f), BrushWidth = 50, Feather = 20 },
		new TerrainType { Id = "desert", Name = "Desert", Kind = TerrainKind.Land, Dark = new Color(0.8f, 0.71f, 0.52f), Light = new Color(0.9f, 0.82f, 0.63f), BrushWidth = 80, Feather = 24 },
		new TerrainType { Id = "snow", Name = "Snow", Kind = TerrainKind.Land, Dark = new Color(0.78f, 0.8f, 0.85f), Light = new Color(0.93f, 0.94f, 0.96f), BrushWidth = 80, Feather = 24, Hint = "Snowfields, e.g. around snowy mountains." },
		new TerrainType { Id = "road", Name = "Road", Kind = TerrainKind.Land, Dark = new Color(0.66f, 0.57f, 0.4f), Light = new Color(0.76f, 0.68f, 0.5f), BrushWidth = 10, Feather = 3, Hint = "Paint as a thin stroke." },
		new TerrainType { Id = "blight", Name = "Blight", Kind = TerrainKind.Overlay, Dark = new Color(0.17f, 0.15f, 0.14f), Light = new Color(0.27f, 0.23f, 0.18f), BrushWidth = 90, Hint = "Dark Lord corruption: a soft dark stain." },
	}.ToDictionary(t => t.Id);

	public static readonly Dictionary<string, ObjectType> ObjectTypes = new[]
	{
		new ObjectType { Id = "pine", Name = "Pine tree", Category = "Nature", Size = 22, ScatterSpacing = 11, SuggestsField = "SpringForest", Ground = GroundKind.Forest, GroundRadius = 1.7f, GroundStrength = 0.35f },
		new ObjectType { Id = "oak", Name = "Leafy tree", Category = "Nature", Size = 20, ScatterSpacing = 12, SuggestsField = "AutumnForest", Ground = GroundKind.Forest, GroundRadius = 1.7f, GroundStrength = 0.35f },
		new ObjectType { Id = "spruce", Name = "Tall pine", Category = "Nature", Size = 28, ScatterSpacing = 12, SuggestsField = "SpringForest", Ground = GroundKind.Forest, GroundRadius = 1.6f, GroundStrength = 0.35f },
		new ObjectType { Id = "birch", Name = "Birch", Category = "Nature", Size = 24, ScatterSpacing = 11, SuggestsField = "SpringForest", Ground = GroundKind.Forest, GroundRadius = 1.7f, GroundStrength = 0.3f },
		new ObjectType { Id = "willow", Name = "Willow", Category = "Nature", Size = 22, ScatterSpacing = 14, Ground = GroundKind.Forest, GroundRadius = 1.6f, GroundStrength = 0.3f },
		new ObjectType { Id = "dead_tree", Name = "Dead tree", Category = "Nature", Size = 18, ScatterSpacing = 14 },
		new ObjectType { Id = "bush", Name = "Bush", Category = "Nature", Size = 8, ScatterSpacing = 10, Ground = GroundKind.Forest, GroundRadius = 2.5f, GroundStrength = 0.2f },
		new ObjectType { Id = "mountain", Name = "Mountain", Category = "Nature", Size = 70, ScatterSpacing = 45, Ground = GroundKind.Rock, GroundRadius = 1.45f, GroundStrength = 0.9f },
		new ObjectType { Id = "snow_mountain", Name = "Snowy mountain", Category = "Nature", Size = 70, ScatterSpacing = 45, Ground = GroundKind.Rock, GroundRadius = 1.45f, GroundStrength = 0.9f },
		new ObjectType { Id = "blight_mountain", Name = "Blighted mountain", Category = "Nature", Size = 70, ScatterSpacing = 45, Ground = GroundKind.Rock, GroundRadius = 1.45f, GroundStrength = 0.9f },
		new ObjectType { Id = "green_mountain", Name = "Grassy mountain", Category = "Nature", Size = 70, ScatterSpacing = 45, Ground = GroundKind.Rock, GroundRadius = 1.3f, GroundStrength = 0.45f },
		new ObjectType { Id = "hill", Name = "Hill", Category = "Nature", Size = 22, ScatterSpacing = 40, Ground = GroundKind.Rock, GroundRadius = 1.3f, GroundStrength = 0.5f },
		new ObjectType { Id = "rocks", Name = "Rocks", Category = "Nature", Size = 12, ScatterSpacing = 20, Ground = GroundKind.Rock, GroundRadius = 1.6f, GroundStrength = 0.5f },
		new ObjectType { Id = "mossy_rocks", Name = "Mossy rocks", Category = "Nature", Size = 14, ScatterSpacing = 22, Ground = GroundKind.Rock, GroundRadius = 1.5f, GroundStrength = 0.45f },
		new ObjectType { Id = "rock_spire", Name = "Rock spire", Category = "Nature", Size = 26, ScatterSpacing = 30, Ground = GroundKind.Rock, GroundRadius = 1.2f, GroundStrength = 0.6f },
		new ObjectType { Id = "castle", Name = "Castle", Category = "Buildings", Size = 85, SuggestsField = "Castle", Ground = GroundKind.Settlement, GroundRadius = 1.15f, GroundStrength = 1.0f },
		new ObjectType { Id = "dark_tower", Name = "Dark tower", Category = "Buildings", Size = 80, SuggestsField = "Castle", Ground = GroundKind.Rock, GroundRadius = 0.75f, GroundStrength = 0.7f },
		new ObjectType { Id = "town", Name = "Town", Category = "Buildings", Size = 55, SuggestsField = "Village", Ground = GroundKind.Settlement, GroundRadius = 1.25f, GroundStrength = 1.0f },
		new ObjectType { Id = "village", Name = "Village", Category = "Buildings", Size = 32, SuggestsField = "Village", Ground = GroundKind.Settlement, GroundRadius = 1.6f, GroundStrength = 0.9f },
		new ObjectType { Id = "tower", Name = "Watchtower", Category = "Buildings", Size = 46, SuggestsField = "GrassPlains", Ground = GroundKind.Settlement, GroundRadius = 0.8f, GroundStrength = 0.7f },
		new ObjectType { Id = "mine", Name = "Mine", Category = "Buildings", Size = 34, SuggestsField = "OreMine", Ground = GroundKind.Rock, GroundRadius = 1.2f, GroundStrength = 0.7f },
		new ObjectType { Id = "cave", Name = "Cave", Category = "Buildings", Size = 26, SuggestsField = "Cave", Ground = GroundKind.Rock, GroundRadius = 1.3f, GroundStrength = 0.6f },
		new ObjectType { Id = "bridge", Name = "Bridge", Category = "Buildings", Size = 14, SuggestsField = "Bridge", Turnable = true },
		new ObjectType { Id = "ship", Name = "Ship", Category = "Buildings", Size = 40 },
	}.ToDictionary(t => t.Id);

	public static TerrainType Terrain(string id) => TerrainTypes.GetValueOrDefault(id ?? "") ?? TerrainTypes["grass"];

	private static List<string> _perkNames;
	// Files in res://resources/field_perks without .tres
	public static List<string> PerkNames => _perkNames ??= ListResources("res://resources/field_perks");

	private static List<string> _questNames;
	// Files in res://resources/quests without .tres
	public static List<string> QuestNames => _questNames ??= ListResources("res://resources/quests");

	public static QuestDefinition LoadQuest(string name) =>
		ResourceLoader.Exists($"res://resources/quests/{name}.tres") ? GD.Load<QuestDefinition>($"res://resources/quests/{name}.tres") : null;

	private static List<string> _unitTemplates;
	// Templates usable as neutral units: neutral templates first, then the player unit templates
	public static List<string> UnitTemplateNames => _unitTemplates ??=
		ListResources("res://unit_templates/neutral_unit_templates").Concat(ListResources("res://unit_templates")).Distinct().ToList();

	public static FieldPerk LoadPerk(string name) =>
		ResourceLoader.Exists($"res://resources/field_perks/{name}.tres") ? GD.Load<FieldPerk>($"res://resources/field_perks/{name}.tres") : null;

	public static UnitTemplate LoadUnitTemplate(string name)
	{
		if (string.IsNullOrEmpty(name))
			return null;
		foreach (var dir in new[] { "res://unit_templates/neutral_unit_templates", "res://unit_templates" })
			if (ResourceLoader.Exists($"{dir}/{name}.tres"))
				return GD.Load<UnitTemplate>($"{dir}/{name}.tres");
		return null;
	}

	// Templates whose Biome is `biome` (none for Biome.None)
	public static List<string> UnitTemplatesFor(Biome biome) => biome == Biome.None ? new()
		: UnitTemplateNames.Where(name => LoadUnitTemplate(name)?.Biome == biome).ToList();

	private static List<string> ListResources(string dir)
	{
		var names = new List<string>();
		using var access = DirAccess.Open(dir);
		if (access == null)
			return names;
		foreach (var file in access.GetFiles())
		{
			// Exported builds list "x.tres.remap"
			string name = file.Replace(".remap", "");
			if (name.EndsWith(".tres"))
				names.Add(name[..^5]);
		}
		names.Sort();
		return names;
	}
}
