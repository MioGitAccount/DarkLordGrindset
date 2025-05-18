using Godot;
using System;

public class fieldType
{
	public string Name { get; private set; }
	public Texture2D FieldImage { get; private set; }

	public FieldTypeEnum FieldTypeEnum { get; private set; }

	public fieldType(string name, string imagePath,FieldTypeEnum fieldTypeEnum)
	{
		Name = name;
		FieldImage = GD.Load<Texture2D>(imagePath);
		FieldTypeEnum = fieldTypeEnum;
	}
}
