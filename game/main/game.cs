using Godot;
using System;

public partial class game : Node2D
{
	public override void _Ready()
	{
		var map = GetNode<map>("Map");
		var ui = GetNode<UIManager>("UI");

		map.FieldSelected += ui.ShowField;
		map.UnitSelected += ui.ShowUnit;
		ui.UnitSelected += map.SelectUnit;
		map.FieldHovered += ui.ShowFieldHover;
		map.IsPointerOverUI = ui.IsMouseOverPanel;
	}

}
