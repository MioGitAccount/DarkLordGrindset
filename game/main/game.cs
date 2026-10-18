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

		PlayerResources.Changed += ui.SetPlayerResource;
		PlayerResources.Reset();

		// Camera starts on the player's castle and stays on the map
		var camera = GetNode<Camera2D>("CameraController");
		camera.LimitLeft = 0;
		camera.LimitTop = 0;
		camera.LimitRight = (int)map.Data.Width;
		camera.LimitBottom = (int)map.Data.Height;
		if (map.StartField != null)
			camera.Position = map.StartField.Position;
	}

	// F10 opens the World Map Editor on the map being played
	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (inputEvent is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F10 })
			GetTree().ChangeSceneToFile(MapSelection.EditorScene);
	}

	public override void _ExitTree()
	{
		PlayerResources.Changed -= GetNode<UIManager>("UI").SetPlayerResource;
	}

}
