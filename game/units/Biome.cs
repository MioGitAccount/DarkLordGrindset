// Where a neutral unit lives; the map editor suggests guards whose biome fits the field
public enum Biome
{
	None,     // fits anywhere (player unit types, guards placed by hand)
	Village,  // villages and towns
	Forest,   // spring and autumn forests
	Mountain, // ore mines and caves
	Plains,   // grass plains, roads, crossroads and bridges
	Castle,   // the player's castle (quests only, no neutrals live there)
	City,     // big cities; no field type uses it yet (arena quests already list it)
}

public static class Biomes
{
	public static Biome Of(FieldTypeEnum fieldType) => fieldType switch
	{
		FieldTypeEnum.Village => Biome.Village,
		FieldTypeEnum.SpringForest or FieldTypeEnum.AutumnForest => Biome.Forest,
		FieldTypeEnum.OreMine or FieldTypeEnum.Cave => Biome.Mountain,
		FieldTypeEnum.GrassPlains or FieldTypeEnum.GrassRoad or FieldTypeEnum.GrassCrossRoads or FieldTypeEnum.Bridge => Biome.Plains,
		FieldTypeEnum.Castle => Biome.Castle,
		_ => Biome.None,
	};

	// Biome of a field type by its name in the map file (e.g. "Village"); None for unknown names
	public static Biome Of(string fieldTypeName) =>
		FieldTypeManager.GetFieldType(fieldTypeName ?? "") is { } type ? Of(type.FieldTypeEnum) : Biome.None;
}
