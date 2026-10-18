using Godot;
using System;

public enum FogState
{
	Unexplored, // never seen: drawn black
	Explored,   // seen before, no unit in range now: drawn grey ("last seen")
	Visible,    // a player unit sees it right now
}

// Two-layer fog of war on a coarse grid (CellSize world pixels per cell).
// Each cell stores "explored" (permanent) and "visible" (recomputed from the units
// in ViewerGroup every UpdateInterval, easing in/out). fog.gdshader turns that into
// black / grey / clear on top of the map.
public partial class FogOfWar : Node2D
{
	public const string ViewerGroup = "fog_viewers"; // units (VisibilityRadius) and castle fields (Castle.VisionRadius)

	[Export] public int MapWidth = 3072;
	[Export] public int MapHeight = 3072;
	[Export] public int CellSize = 8;
	[Export] public float UpdateInterval = 0.1f;
	[Export] public float FadeSpeed = 2f; // visibility change per second (1 = one second fade)

	// Raised after every fog update, so others can re-check fog states
	public event Action Updated;

	private int _w;
	private int _h;
	private float[] _visible;
	private float[] _target;
	private float[] _explored;
	private byte[] _pixels;
	private Image _image;
	private ImageTexture _texture;
	private double _timer;

	private Sprite2D _sprite;

	public override void _EnterTree()
	{
		_sprite = new Sprite2D
		{
			Centered = true,
			Scale = new Vector2(CellSize, CellSize),
			TextureFilter = TextureFilterEnum.Linear,
			Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://game/map/fog.gdshader") },
		};
		AddChild(_sprite);
		Allocate();
	}

	// Covers a map of this size whose top-left corner is at the parent's origin; clears the fog
	public void Setup(Vector2 mapSize)
	{
		MapWidth = Mathf.CeilToInt(mapSize.X);
		MapHeight = Mathf.CeilToInt(mapSize.Y);
		Position = mapSize / 2f;
		Allocate();
	}

	private void Allocate()
	{
		_w = Mathf.CeilToInt(MapWidth / (float)CellSize);
		_h = Mathf.CeilToInt(MapHeight / (float)CellSize);
		_visible = new float[_w * _h];
		_target = new float[_w * _h];
		_explored = new float[_w * _h];
		_pixels = new byte[_w * _h * 2];
		_image = Image.CreateFromData(_w, _h, false, Image.Format.Rg8, _pixels);
		_texture = ImageTexture.CreateFromImage(_image);
		_sprite.Texture = _texture;
	}

	public override void _Process(double delta)
	{
		_timer += delta;
		if (_timer < UpdateInterval)
			return;
		Recompute((float)_timer);
		_timer = 0;
	}

	public FogState GetState(Vector2 worldPosition)
	{
		Vector2 cell = (ToLocal(worldPosition) + new Vector2(MapWidth, MapHeight) / 2f) / CellSize;
		int x = (int)cell.X;
		int y = (int)cell.Y;
		if (x < 0 || y < 0 || x >= _w || y >= _h)
			return FogState.Visible; // outside the fogged area
		int i = y * _w + x;
		if (_visible[i] >= 0.5f)
			return FogState.Visible;
		return _explored[i] >= 0.5f ? FogState.Explored : FogState.Unexplored;
	}

	public void ResetFog()
	{
		Array.Clear(_visible);
		Array.Clear(_explored);
		Recompute(0);
	}

	private void Recompute(float elapsed)
	{
		Array.Clear(_target);
		foreach (Node node in GetTree().GetNodesInGroup(ViewerGroup))
		{
			if (node is unit viewer)
				Stamp(viewer.GlobalPosition, viewer.VisibilityRadius);
			else if (node is field castleField && castleField.Castle != null)
				Stamp(castleField.GlobalPosition, castleField.Castle.VisionRadius);
		}

		float step = FadeSpeed * elapsed;
		for (int i = 0; i < _visible.Length; i++)
		{
			_visible[i] = Mathf.MoveToward(_visible[i], _target[i], step);
			_explored[i] = Mathf.Max(_explored[i], _visible[i]);
			_pixels[i * 2] = (byte)(_explored[i] * 255);
			_pixels[i * 2 + 1] = (byte)(_visible[i] * 255);
		}
		_image.SetData(_w, _h, false, Image.Format.Rg8, _pixels);
		_texture.Update(_image);
		Updated?.Invoke();
	}

	// Marks a circle as visible, with a soft edge two cells wide
	private void Stamp(Vector2 worldPosition, float radius)
	{
		Vector2 center = (ToLocal(worldPosition) + new Vector2(MapWidth, MapHeight) / 2f) / CellSize;
		float r = radius / CellSize;
		int x0 = Math.Max(0, (int)(center.X - r));
		int x1 = Math.Min(_w - 1, (int)(center.X + r));
		int y0 = Math.Max(0, (int)(center.Y - r));
		int y1 = Math.Min(_h - 1, (int)(center.Y + r));
		for (int y = y0; y <= y1; y++)
		{
			for (int x = x0; x <= x1; x++)
			{
				float d = new Vector2(x + 0.5f, y + 0.5f).DistanceTo(center);
				float v = Mathf.Clamp((r - d) / 2f, 0f, 1f);
				int i = y * _w + x;
				if (v > _target[i])
					_target[i] = v;
			}
		}
	}
}
