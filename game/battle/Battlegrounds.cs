using Godot;

// Painted backdrops for the battle screen, in res://images/battlegrounds/. A battle uses the
// first picture that exists of: <FieldType> (e.g. SpringForest.webp, for one field type), then
// <biome> (forest, village, mountain, plains). Fields without one keep the plain drawn backdrop.
// The pictures are shown scaled to cover the screen; keep the ground in the lower ~70%.
public static class Battlegrounds
{
	private const string Folder = "res://images/battlegrounds";
	private static readonly string[] Extensions = { "webp", "png", "jpg" };
	// Field types that fight on another type's backdrop
	private static readonly System.Collections.Generic.Dictionary<string, string> Borrowed = new()
	{
		["DarkForest"] = "Swamp",
	};

	public static Texture2D For(field battleField)
	{
		if (battleField == null)
			return null;
		var biome = battleField.type != null ? Biomes.Of(battleField.type.FieldTypeEnum) : Biome.None;
		string typeName = battleField.TypeName != null && Borrowed.TryGetValue(battleField.TypeName, out var other) ? other : battleField.TypeName;
		foreach (var name in new[] { typeName, biome == Biome.None ? null : biome.ToString().ToLower() })
		{
			if (string.IsNullOrEmpty(name))
				continue;
			foreach (var extension in Extensions)
			{
				string path = $"{Folder}/{name}.{extension}";
				if (ResourceLoader.Exists(path))
					return GD.Load<Texture2D>(path);
			}
		}
		return null;
	}
}
