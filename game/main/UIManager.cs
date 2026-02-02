using Godot;
using System;
using System.Collections.Generic;

public partial class UIManager : Node
{
	private Label fieldInfoLabel; // Reference to UI Label
	private Label fieldGoldLabel; // Reference to UI Label
	private TextureRect fieldImage;
	private TextureRect unitImage;
	private Control fieldNeutralUnits;
	[Export] public PackedScene UnitEntryScene;

	private Control fieldPlayerUnits;
	public override void _Ready()
	{
		fieldGoldLabel = GetNode<Label>("../UI/BigHContainer/RightVContainer/Resources/GoldPanel/Label");
		fieldInfoLabel = GetNode<Label>("../UI/BigHContainer/RightVContainer/FieldLabel");
		fieldImage = GetNode<TextureRect>("../UI/BigHContainer/RightVContainer/Panel/FieldImage");
		fieldImage.CustomMinimumSize = new Vector2(156, 282); //wtf?
		fieldNeutralUnits = GetNode<Control>("../UI/BigHContainer/RightVContainer/Panel/PanelContainerForNeutrals/HBoxContainer");
		unitImage = GetNode<TextureRect>("../UI/UnitVContainer/Panel/TextureRect");
		fieldPlayerUnits = GetNode<Control>("../UI/BigHContainer/RightVContainer/PanelContainerForPlayerUnits/HBoxContainer");

	}
	public void UpdateSelectionInfo(field selectedField)
	{
		if (selectedField != null)
		{
			fieldInfoLabel.Text = $"Selected Node: {selectedField.Name}";
			UpdateNeutralUnitsInfo(selectedField);
			UpdatePlayerUnitsInfo(selectedField);

			if (selectedField.type != null)
			{
				fieldInfoLabel.Text += $"\nType: {selectedField.type.Name}";
				fieldImage.Texture = selectedField.type.FieldImage;
			}
			//resources
			if (selectedField.resourcesHolder != null)
			{
				fieldGoldLabel.Text = selectedField.resourcesHolder.gold + "/" + selectedField.resourcesHolder.maxGold;
			}
			else
			{
				fieldGoldLabel.Text = "0";
			}

		}
		else
		{
			fieldInfoLabel.Text = "No node selected";
			fieldImage.Texture = null;
		}
	}
	public void UpdateNeutralUnitsInfo(field selectedField)
	{
		Godot.Collections.Array<NeutralUnit> neutrals = selectedField.presentNeutralUnits;
		List<Node> panels = new List<Node>(fieldNeutralUnits.GetChildren());
		var panel = GetNode<PanelContainerForNeutrals>("../UI/BigHContainer/RightVContainer/Panel/PanelContainerForNeutrals");
		panel.ShowUnits(neutrals);


	}
	
	public void UpdatePlayerUnitsInfo(field selectedField)
	{
		Godot.Collections.Array<unit> playerUnits = selectedField.presentPlayerUnits;
		List<Node> panels = new List<Node>(fieldPlayerUnits.GetChildren());
		var panel = GetNode<PanelContainerForPlayerUnits>("../UI/BigHContainer/RightVContainer/PanelContainerForPlayerUnits");
		panel.ShowUnits(playerUnits);

	}
	public void UpdateUnitSelectionInfo(unit selectedUnit)
	{
		if (selectedUnit != null)
		{
			Texture2D icon = selectedUnit.GetIcon();
			if (icon != null)
				unitImage.Texture = icon;

		}
	}
	
}


