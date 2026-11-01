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
		ui.SetQuests(map.Quests);
		map.QuestClicked += ui.ShowQuest;

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

		PlayWorldMapMusic();
	}

	// World map theme (rendered by tools/music/render_score.py); the track is built to loop seamlessly
	private void PlayWorldMapMusic()
	{
		var stream = GD.Load<AudioStreamOggVorbis>("res://sound/music/beyond_the_ashen_crown.ogg");
		if (stream == null)
			return;
		stream.Loop = true;
		var music = new AudioStreamPlayer { Name = "Music", Stream = stream, VolumeDb = -8f };
		AddChild(music);
		music.Play();
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
