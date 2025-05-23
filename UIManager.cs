using Godot;
using System;

public partial class UIManager : Node
{
	private Label fieldInfoLabel; // Reference to UI Label
	private Label fieldGoldLabel; // Reference to UI Label
	private TextureRect fieldImage;
	private TextureRect unitImage;
	private Control fieldNeutralUnits;
	public override void _Ready()
	{
		fieldGoldLabel = GetNode<Label>("../UI/BigHContainer/RightVContainer/Resources/GoldPanel/Label");
		fieldInfoLabel = GetNode<Label>("../UI/BigHContainer/RightVContainer/FieldLabel");
		fieldImage = GetNode<TextureRect>("../UI/BigHContainer/RightVContainer/Panel/FieldImage");
		fieldImage.CustomMinimumSize = new Vector2(156, 282); //wtf?
		fieldNeutralUnits = GetNode<TextureRect>("../UI/BigHContainer/RightVContainer/PanelContainerForNeutrals");

		unitImage = GetNode<TextureRect>("../UI/UnitVContainer/Panel/TextureRect");
		
	}
	public void UpdateSelectionInfo(field selectedField)
	{
		if (selectedField != null)
		{
			fieldInfoLabel.Text = $"Selected Node: {selectedField.Name}";

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

	}
	public void UpdateUnitSelectionInfo(unit selectedUnit)
	{
		if (selectedUnit != null)
		{
			Texture2D icon = selectedUnit.GetIcon();
			if(icon != null)
				unitImage.Texture = icon;

		}
	}
}
