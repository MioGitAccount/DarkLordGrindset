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
		FieldTypes["SpringForest"] = new fieldType("Spring Forest", "res://images/SpringForest.png",FieldTypeEnum.SpringForest);
		FieldTypes["Cave"] = new fieldType("Cave", "res://images/Cave.png",FieldTypeEnum.Cave);
		FieldTypes["AutumnForest"] = new fieldType("Fall Forest", "res://images/AutumnForest.png",FieldTypeEnum.AutumnForest);
		FieldTypes["OreMine"] = new fieldType("Ore Mine", "res://images/OreMine.png",FieldTypeEnum.OreMine);
		FieldTypes["Bridge"] = new fieldType("Bridge", "res://images/Bridge.jpg",FieldTypeEnum.Bridge);
		FieldTypes["GrassPlains"] = new fieldType("Grass Plains", "res://images/GrassPlains.png",FieldTypeEnum.GrassPlains);
		FieldTypes["Village"] = new fieldType("Village", "res://images/Village.png",FieldTypeEnum.Village);
	}

	public static fieldType GetFieldType(string typeName)
	{
		return FieldTypes.ContainsKey(typeName) ? FieldTypes[typeName] : null;
	}
}
