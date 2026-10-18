using Godot;
using System;
using System.Collections.Generic;

public class fieldType
{
	public string Name { get; private set; }
	public Texture2D FieldImage { get; private set; }

	public FieldTypeEnum FieldTypeEnum { get; private set; }
	// Perks every field of this type has (resources it generates and can hold)
	public IReadOnlyList<FieldPerk> Perks { get; private set; }

	// perkNames: files in res://resources/field_perks/ without the .tres
	public fieldType(string name, string imagePath, FieldTypeEnum fieldTypeEnum, params string[] perkNames)
	{
		Name = name;
		FieldImage = GD.Load<Texture2D>(imagePath);
		FieldTypeEnum = fieldTypeEnum;
		var perks = new List<FieldPerk>();
		foreach (var perkName in perkNames)
		{
			var perk = GD.Load<FieldPerk>($"res://resources/field_perks/{perkName}.tres");
			if (perk != null)
				perks.Add(perk);
			else
				GD.PushWarning($"Field type {name}: perk '{perkName}' not found");
		}
		Perks = perks;
	}
}
