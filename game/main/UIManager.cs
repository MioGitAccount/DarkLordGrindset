using Godot;
using System;
using System.Collections.Generic;

public partial class UIManager : Node
{

	private TextureRect fieldImage;
	private TextureRect unitImage;
	private Control fieldNeutralUnits;
	private Label fieldInfoLabel;
	[Export] public PackedScene UnitEntryScene;

	private Control fieldPlayerUnits;
	public override void _Ready()
	{
		fieldImage = GetNode<TextureRect>("../UI/BigHContainer/RightVContainer/Panel/FieldImage");
		fieldImage.CustomMinimumSize = new Vector2(156, 282); //wtf?
		fieldNeutralUnits = GetNode<Control>("../UI/BigHContainer/RightVContainer/Panel/PanelContainerForNeutrals/HBoxContainer");
		unitImage = GetNode<TextureRect>("../UI/UnitVContainer/Panel/TextureRect");
		fieldPlayerUnits = GetNode<Control>("../UI/BigHContainer/RightVContainer/PanelContainerForPlayerUnits/HBoxContainer");
		fieldInfoLabel = GetNode<Label>("../UI/BigHContainer/RightVContainer/FieldLabel");
	}
	public void UpdateSelectionInfo(field selectedField)
	{
		if (selectedField != null)
		{
			fieldInfoLabel.Text = $"Selected Node: {selectedField.Name}";
			UpdateNeutralUnitsInfo(selectedField);
			UpdatePlayerUnitsInfo(selectedField);
			UpdatePlayerResourcesInfo(selectedField);

			if (selectedField.type != null)
			{
				fieldInfoLabel.Text += $"\nType: {selectedField.type.Name}";
				fieldImage.Texture = selectedField.type.FieldImage;
			}
			//resources


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
		var panel = GetNode<PanelContainerForNeutrals>("../UI/BigHContainer/RightVContainer/Panel/PanelContainerForNeutrals");
		panel.ShowUnits(neutrals);


	}
	public void UpdatePlayerUnitsInfo(field selectedField)
	{
		Godot.Collections.Array<unit> playerUnits = selectedField.presentPlayerUnits;
		var panel = GetNode<PanelContainerForPlayerUnits>("../UI/BigHContainer/RightVContainer/PanelContainerForPlayerUnits");
		//List<Node> panels = new List<Node>(fieldPlayerUnits.GetChildren());
		panel.ShowUnits(playerUnits);

	}
	public void UpdatePlayerResourcesInfo(field selectedField)
	{
		List<ResourceInstance> resources = selectedField.resourcesHolder.Resources;
		var panel = GetNode<PanelContainerForResources>("../UI/BigHContainer/RightVContainer/PanelContainerForResources");
		panel.ShowResources(resources);	
	}
	
	public void UpdateUnitSelectionInfo(unit selectedUnit)
	{
		if (selectedUnit != null)
		{
			Texture2D icon = selectedUnit.GetIcon();
			if (icon != null)
				unitImage.Texture = icon;
			UpdateUnitAbilitiesPanel(selectedUnit);

		}
	}
	public void UpdateUnitAbilitiesPanel(unit unit)
	{
		Godot.Collections.Array<Ability> abilities = unit.Abilities;
		var panel = GetNode<UnitAbilityContainer>("../UI/UnitVContainer/UnitAbilityContainer");
		panel.ShowAbilities(unit, abilities);

	}
	
}


