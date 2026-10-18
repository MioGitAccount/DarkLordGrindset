using Godot;

// Shows layers 1 and 2 of a map (terrain and objects). Used by both the game and the editor;
// fields (layer 3) are drawn by whoever owns the view.
public partial class MapView : Node2D
{
	public TerrainRenderer Terrain { get; private set; }
	public ObjectLayer Objects { get; private set; }
	public MapData Data { get; private set; }
	private Sprite2D _underlay;
	private Paper _paper;

	public override void _Ready()
	{
		_underlay = new Sprite2D { Name = "Underlay", Centered = false, Visible = false };
		Terrain = new TerrainRenderer { Name = "Terrain" };
		Objects = new ObjectLayer { Name = "Objects" };
		AddChild(Terrain);
		AddChild(_underlay);
		AddChild(Objects);
		Objects.Changed += () => Terrain.RefreshObjects();
		_paper = new Paper { Name = "Paper" };
		AddChild(_paper);
		if (Data != null)
			SetMap(Data);
	}

	public void SetMap(MapData data)
	{
		Data = data;
		if (Terrain == null)
			return; // _Ready finishes the job
		Terrain.SetMap(data);
		Objects.SetMap(data);
		_paper.SetSize(data.Size);
		UpdateUnderlay();
	}

	// Editor only: the map's reference image, stretched over the whole map, drawn over the terrain
	public bool UnderlayVisible
	{
		get => _underlay.Visible;
		set => _underlay.Visible = value && _underlay.Texture != null;
	}

	public float UnderlayOpacity
	{
		get => _underlay.Modulate.A;
		set => _underlay.Modulate = new Color(1, 1, 1, value);
	}

	public void UpdateUnderlay()
	{
		Texture2D texture = null;
		string path = Data?.Underlay;
		if (!string.IsNullOrEmpty(path))
		{
			if (ResourceLoader.Exists(path))
				texture = GD.Load<Texture2D>(path);
			else if (FileAccess.FileExists(path))
			{
				var image = Image.LoadFromFile(path);
				if (image != null)
					texture = ImageTexture.CreateFromImage(image);
			}
		}
		_underlay.Texture = texture;
		if (texture != null)
			_underlay.Scale = Data.Size / texture.GetSize();
		else
			_underlay.Visible = false;
	}

	// Parchment multiplied over terrain and objects (paper.gdshader)
	private partial class Paper : Node2D
	{
		private Vector2 _size;

		public Paper()
		{
			TextureRepeat = TextureRepeatEnum.Enabled;
			Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://game/map/render/paper.gdshader") };
		}

		public void SetSize(Vector2 size)
		{
			_size = size;
			var material = (ShaderMaterial)Material;
			material.SetShaderParameter("variation_tex", TerrainTextures.Variation());
			material.SetShaderParameter("world_size", size);
			QueueRedraw();
		}

		public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, _size), Colors.White);
	}
}
