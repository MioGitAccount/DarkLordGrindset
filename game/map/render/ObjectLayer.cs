using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Draws layer 2 (objects), sorted so things further down the map are drawn in front.
// Objects are split into horizontal bands so editing a few only redraws their band.
public partial class ObjectLayer : Node2D
{
	private const float BandHeight = 256f;

	private MapData _data;
	private readonly List<Band> _bands = new();
	// Raised after every Refresh (objects added, removed or moved)
	public event Action Changed;
	public override void _Ready()
	{
		// Sprites are painted larger than they are drawn; mipmaps keep them smooth when zoomed out
		TextureFilter = TextureFilterEnum.LinearWithMipmaps;
	}

	public void SetMap(MapData data)
	{
		_data = data;
		Refresh();
	}

	// Re-sorts all objects; only bands touching dirtyY (world y range, or everything) are redrawn
	public void Refresh(float dirtyFromY = float.NegativeInfinity, float dirtyToY = float.PositiveInfinity)
	{
		if (_data == null)
			return;
		int count = Math.Max(1, (int)MathF.Ceiling(_data.Height / BandHeight) + 1);
		while (_bands.Count < count)
		{
			var band = new Band();
			_bands.Add(band);
			AddChild(band);
		}
		while (_bands.Count > count)
		{
			_bands[^1].QueueFree();
			_bands.RemoveAt(_bands.Count - 1);
		}
		foreach (var band in _bands)
			band.Objects.Clear();
		foreach (var obj in _data.Objects)
		{
			int index = Math.Clamp((int)(MapData.V(obj.Pos).Y / BandHeight), 0, _bands.Count - 1);
			_bands[index].Objects.Add(obj);
		}
		for (int i = 0; i < _bands.Count; i++)
		{
			float top = i * BandHeight;
			// The last band also holds anything below the map
			if (top <= dirtyToY && (i == _bands.Count - 1 || top + BandHeight >= dirtyFromY))
			{
				_bands[i].Objects.Sort((a, b) => a.Pos[1].CompareTo(b.Pos[1]));
				_bands[i].QueueRedraw();
			}
		}
		Changed?.Invoke();
	}

	// Topmost object whose picture covers the point, or null
	public MapObject ObjectAt(Vector2 point)
	{
		MapObject best = null;
		if (_data == null)
			return null;
		foreach (var obj in _data.Objects)
		{
			var rect = ObjectArt.Bounds(obj);
			rect.Position += MapData.V(obj.Pos);
			// Lower objects are drawn in front
			if (rect.HasPoint(point) && (best == null || obj.Pos[1] >= best.Pos[1]))
				best = obj;
		}
		return best;
	}

	private partial class Band : Node2D
	{
		public readonly List<MapObject> Objects = new();

		public override void _Draw()
		{
			foreach (var obj in Objects)
				ObjectArt.Draw(this, obj);
		}
	}
}
