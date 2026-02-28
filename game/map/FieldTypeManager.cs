using System.Collections.Generic;
using Godot;

public class FieldTypeManager
{
	public static Dictionary<string, fieldType> FieldTypes = new Dictionary<string, fieldType>();

	static FieldTypeManager()
	{
		RegisterTypes();
	}

	private static void RegisterTypes()
	{
		FieldTypes["SpringForest"] = new fieldType("Spring Forest", "res://images/Bioms/SpringForest.png", FieldTypeEnum.SpringForest);
		FieldTypes["Cave"] = new fieldType("Cave", "res://images/Bioms/Cave.png", FieldTypeEnum.Cave);
		FieldTypes["AutumnForest"] = new fieldType("Fall Forest", "res://images/Bioms/AutumnForest.png", FieldTypeEnum.AutumnForest);
		FieldTypes["OreMine"] = new fieldType("Ore Mine", "res://images/Bioms/OreMine.png", FieldTypeEnum.OreMine);
		FieldTypes["Bridge"] = new fieldType("Bridge", "res://images/Bioms/Bridge.jpg", FieldTypeEnum.Bridge);
		FieldTypes["GrassPlains"] = new fieldType("Grass Plains", "res://images/Bioms/GrassPlains.png", FieldTypeEnum.GrassPlains);
		FieldTypes["Village"] = new fieldType("Village", "res://images/Bioms/Village.png", FieldTypeEnum.Village);
		FieldTypes["GrassCrossRoads"] = new fieldType("GrassCrossRoads", "res://images/Bioms/GrassCrossRoads.png", FieldTypeEnum.GrassCrossRoads);
		FieldTypes["GrassRoad"] = new fieldType("GrassRoad", "res://images/Bioms/GrassRoad.png", FieldTypeEnum.GrassCrossRoads);
	}

	public static fieldType GetFieldType(string typeName)
	{
		return FieldTypes.ContainsKey(typeName) ? FieldTypes[typeName] : null;
	}

}
