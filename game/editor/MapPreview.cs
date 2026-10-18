using Godot;
using System;

// Renders a map file to a PNG and quits. Lets an external tool (or an AI session writing maps)
// see what a map looks like without opening the editor. Needs a window (not --headless):
//   Godot --path . res://game/editor/map_preview.tscn -- --map maps/x.json --out preview.png [--width 2048] [--scale 2.3] [--no-fields]
// --scale renders at that many pixels per world pixel instead (2.3 is about the in-game zoom)
public partial class MapPreview : Node
{
	private SubViewport _viewport;
	private string _out;
	private int _frames;

	public override void _Ready()
	{
		var args = OS.GetCmdlineUserArgs();
		string mapPath = Arg(args, "--map") ?? "res://maps/deathland.json";
		_out = Arg(args, "--out") ?? "user://map_preview.png";
		int width = int.TryParse(Arg(args, "--width"), out int w) ? w : 2048;
		bool fields = Array.IndexOf(args, "--no-fields") < 0;

		var data = MapData.Load(mapPath);
		if (data == null)
		{
			GetTree().Quit(1);
			return;
		}
		foreach (var problem in data.Validate())
			GD.PrintErr("map problem: " + problem);

		float scale = float.TryParse(Arg(args, "--scale"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s)
			? s : Math.Min(1f, width / data.Width);
		_viewport = new SubViewport
		{
			Size = (Vector2I)(data.Size * scale).Ceil(),
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			Msaa2D = Viewport.Msaa.Msaa4X,
		};
		AddChild(_viewport);
		var world = new Node2D { Scale = Vector2.One * scale, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
		_viewport.AddChild(world);
		var view = new MapView();
		world.AddChild(view);
		view.SetMap(data);
		if (fields)
			world.AddChild(new FieldOverlay { Data = data });
	}

	public override void _Process(double delta)
	{
		// A few frames so the terrain passes have all rendered
		if (_viewport == null || ++_frames < 8)
			return;
		var image = _viewport.GetTexture().GetImage();
		string path = _out.StartsWith("res://") || _out.StartsWith("user://") ? ProjectSettings.GlobalizePath(_out) : _out;
		var error = image.SavePng(path);
		GD.Print(error == Error.Ok ? $"saved {path}" : $"can't save {path}: {error}");
		GetTree().Quit(error == Error.Ok ? 0 : 1);
		_viewport = null;
	}

	private static string Arg(string[] args, string name)
	{
		int i = Array.IndexOf(args, name);
		return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
	}
}
