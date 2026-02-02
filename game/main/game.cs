using Godot;
using System;

public partial class game : Node2D
{
	public override void _Ready()
	{
		var map = GetNode<map>("Map");
		var panel = GetNode<PanelContainerForPlayerUnits>("UI/BigHContainer/RightVContainer/PanelContainerForPlayerUnits");

		panel.UnitSelected += map.UnitSelected;

	}

}
