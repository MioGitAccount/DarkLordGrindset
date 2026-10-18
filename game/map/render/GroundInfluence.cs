using Godot;

// Low-resolution picture of how map objects change the ground around them, rendered off-screen
// whenever objects change: a soft blob under every object, added up per channel
//   R: forest floor (trees)   G: rocky ground (mountains, caves...)   B: worn dirt (settlements)
// so a dense forest gets a strong forest floor and a lone tree barely any.
// terrain.gdshader reads it as `ground_tex`. What each object type does is in MapCatalog (Ground*).
public partial class GroundInfluence : Node
{
	private const float Scale = 0.125f; // influence pixels per world pixel

	private MapData _data;
	private SubViewport _viewport;
	private Blobs _blobs;

	public Texture2D Texture => _viewport.GetTexture();

	public override void _Ready()
	{
		_viewport = new SubViewport
		{
			Name = "GroundInfluence",
			Disable3D = true,
			TransparentBg = true, // clears to black: blobs are added on top
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
			CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Linear,
		};
		AddChild(_viewport);
		_blobs = new Blobs { Owner_ = this, Scale = Vector2.One * Scale, Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
		_viewport.AddChild(_blobs);
	}

	public void SetMap(MapData data)
	{
		_data = data;
		_viewport.Size = (Vector2I)(data.Size * Scale).Ceil() + Vector2I.One;
		Refresh();
	}

	// Call after objects were added, removed or moved
	public void Refresh()
	{
		if (_data == null)
			return;
		_blobs.QueueRedraw();
		_viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
	}

	private partial class Blobs : Node2D
	{
		public GroundInfluence Owner_;
		private static Texture2D _blob;

		public override void _Draw()
		{
			var data = Owner_._data;
			if (data == null)
				return;
			_blob ??= MakeBlob();
			foreach (var obj in data.Objects)
			{
				if (!MapCatalog.ObjectTypes.TryGetValue(obj.Type ?? "", out var type) || type.Ground == MapCatalog.GroundKind.None)
					continue;
				float size = type.Size * obj.Scale;
				float radius = size * type.GroundRadius;
				// Tall things (mountains, buildings) spread around their foot, a bit up into their picture
				var center = MapData.V(obj.Pos) + new Vector2(0, -size * 0.15f);
				float k = type.GroundStrength;
				var color = type.Ground switch
				{
					MapCatalog.GroundKind.Forest => new Color(k, 0, 0),
					MapCatalog.GroundKind.Rock => new Color(0, k, 0),
					_ => new Color(0, 0, k),
				};
				// Wider than tall: the ground is seen from slightly above
				DrawTextureRect(_blob, new Rect2(center - new Vector2(radius, radius * 0.75f), new Vector2(radius * 2, radius * 1.5f)), false, color);
			}
		}

		// Round soft spot: 1 in the middle, smoothly 0 at the edge
		private static Texture2D MakeBlob()
		{
			const int size = 64;
			var bytes = new byte[size * size * 4];
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
					float d = Mathf.Min(1f, Mathf.Sqrt(dx * dx + dy * dy));
					float v = (1 - d * d) * (1 - d * d);
					int i = (y * size + x) * 4;
					bytes[i] = bytes[i + 1] = bytes[i + 2] = 255;
					bytes[i + 3] = (byte)(v * 255);
				}
			}
			return ImageTexture.CreateFromImage(Image.CreateFromData(size, size, false, Image.Format.Rgba8, bytes));
		}
	}
}
