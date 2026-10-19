using Godot;
using System;
using System.Collections.Generic;

// Draws layer 1 (terrain). The shapes themselves are drawn live in the world with their
// terrain textures (so they stay sharp at any zoom). Coast and blight effects come from
// low-resolution passes rendered off-screen only when the terrain changes:
//   sea mask        - white where water is
//   blight mask     - white where blight is
//   row + distance  - exact distance to the coast on both sides (a row pass, then a column
//                     pass) + blurred blight, from the masks
// terrain.gdshader (on the live shapes) then adds shore rings, shallow water, dark coast
// lines and the blight stain.
public partial class TerrainRenderer : Node2D
{
	private const float MaxMaskScale = 0.5f;    // mask/distance pixels per world pixel...
	private const int MaxMaskSize = 2048;       // ...unless that makes the masks bigger than this
	public const float MaxDistance = 64f;       // world px; matches terrain*.gdshader

	private MapData _data;
	private readonly Dictionary<TerrainShape, TerrainGeometry.Mesh> _meshes = new();
	private int _meshSeed;

	private SubViewport _seaViewport;
	private SubViewport _blightViewport;
	private SubViewport _rowViewport;
	private SubViewport _distanceViewport;
	private Painter _colorPainter;
	private Painter _seaPainter;
	private Painter _blightPainter;
	private ColorRect _rowRect;
	private ColorRect _distanceRect;
	private int _passFramesLeft;
	private GroundInfluence _ground;

	public override void _Ready()
	{
		_ground = new GroundInfluence { Name = "Ground" };
		AddChild(_ground);
		_seaViewport = MakeViewport("SeaMask");
		_blightViewport = MakeViewport("BlightMask");
		_rowViewport = MakeViewport("RowDistance");
		_distanceViewport = MakeViewport("Distance");

		_colorPainter = new Painter(this, Painter.Mode.Color)
		{
			Name = "Shapes",
			TextureFilter = TextureFilterEnum.LinearWithMipmaps,
			Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://game/map/render/terrain.gdshader") },
		};
		_seaPainter = new Painter(this, Painter.Mode.Sea);
		_blightPainter = new Painter(this, Painter.Mode.Blight);
		AddChild(_colorPainter);
		_seaViewport.AddChild(_seaPainter);
		_blightViewport.AddChild(_blightPainter);

		_rowRect = new ColorRect
		{
			Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://game/map/render/terrain_rows.gdshader") },
		};
		_rowViewport.AddChild(_rowRect);
		_distanceRect = new ColorRect
		{
			Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://game/map/render/terrain_distance.gdshader") },
		};
		_distanceViewport.AddChild(_distanceRect);

		if (_data != null)
			Rebuild();
	}

	private SubViewport MakeViewport(string name)
	{
		var viewport = new SubViewport
		{
			Name = name,
			Disable3D = true,
			TransparentBg = false,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
			CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Linear,
			// Anti-aliased masks give the distance pass smooth (not stair-stepped) coastlines
			Msaa2D = name.EndsWith("Mask") ? Viewport.Msaa.Msaa8X : Viewport.Msaa.Disabled,
		};
		AddChild(viewport);
		return viewport;
	}

	public void SetMap(MapData data)
	{
		_data = data;
		_meshes.Clear();
		if (IsInsideTree())
			Rebuild();
	}

	// Call after changing the terrain list (or the map size/base) of the current map
	public void Refresh()
	{
		if (_data == null || !IsInsideTree())
			return;
		// Shapes are only ever added/removed by the editor, so cached meshes stay valid;
		// drop meshes of shapes that are gone
		var alive = new HashSet<TerrainShape>(_data.Terrain);
		foreach (var shape in new List<TerrainShape>(_meshes.Keys))
			if (!alive.Contains(shape))
				_meshes.Remove(shape);
		Rebuild();
	}

	// Call after map objects changed: the ground around them is drawn differently
	public void RefreshObjects() => _ground?.Refresh();

	// Forget the cached mesh of a shape that was edited in place
	public void Invalidate(TerrainShape shape) => _meshes.Remove(shape);

	private void Rebuild()
	{
		if (_meshSeed != _data.Seed)
		{
			_meshes.Clear();
			_meshSeed = _data.Seed;
		}
		var size = _data.Size;
		float maskScale = Math.Min(MaxMaskScale, MaxMaskSize / Math.Max(size.X, size.Y));
		_seaViewport.Size = (Vector2I)(size * maskScale).Ceil();
		_blightViewport.Size = _seaViewport.Size;
		_rowViewport.Size = _seaViewport.Size;
		_distanceViewport.Size = _seaViewport.Size;
		_seaPainter.Scale = Vector2.One * maskScale;
		_blightPainter.Scale = Vector2.One * maskScale;
		_rowRect.Size = _seaViewport.Size;
		_distanceRect.Size = _seaViewport.Size;
		int maxPx = Mathf.CeilToInt(MaxDistance * maskScale);
		var rowMaterial = (ShaderMaterial)_rowRect.Material;
		rowMaterial.SetShaderParameter("sea_mask", _seaViewport.GetTexture());
		rowMaterial.SetShaderParameter("max_px", maxPx);

		var distanceMaterial = (ShaderMaterial)_distanceRect.Material;
		distanceMaterial.SetShaderParameter("sea_mask", _seaViewport.GetTexture());
		distanceMaterial.SetShaderParameter("row_distance", _rowViewport.GetTexture());
		distanceMaterial.SetShaderParameter("max_px", maxPx);
		distanceMaterial.SetShaderParameter("blight_mask", _blightViewport.GetTexture());
		distanceMaterial.SetShaderParameter("world_per_pixel", 1f / maskScale);

		var displayMaterial = (ShaderMaterial)_colorPainter.Material;
		displayMaterial.SetShaderParameter("distance_tex", _distanceViewport.GetTexture());
		displayMaterial.SetShaderParameter("blight_tex", TerrainTextures.Get("blight"));
		displayMaterial.SetShaderParameter("blight_tile", TerrainTextures.TileSizeOf("blight"));
		displayMaterial.SetShaderParameter("rock_tex", TerrainTextures.Get("dirt"));
		displayMaterial.SetShaderParameter("rock_tile", TerrainTextures.TileSizeOf("dirt"));
		displayMaterial.SetShaderParameter("worn_tex", TerrainTextures.Get("road"));
		displayMaterial.SetShaderParameter("worn_tile", TerrainTextures.TileSizeOf("road"));
		displayMaterial.SetShaderParameter("variation_tex", TerrainTextures.Variation());
		displayMaterial.SetShaderParameter("world_size", size);
		_ground.SetMap(_data);
		displayMaterial.SetShaderParameter("ground_tex", _ground.Texture);

		_colorPainter.QueueRedraw();
		_seaPainter.QueueRedraw();
		_blightPainter.QueueRedraw();
		_seaViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		_blightViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		// Each pass reads the one before, so they run again on the next frames until all are done
		_passFramesLeft = 3;
		RunPasses();
	}

	public override void _Process(double delta)
	{
		if (_passFramesLeft <= 0)
			return;
		_passFramesLeft--;
		RunPasses();
	}

	private void RunPasses()
	{
		_rowViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		_distanceViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
	}

	private TerrainGeometry.Mesh MeshOf(TerrainShape shape)
	{
		var type = MapCatalog.Terrain(shape.Type);
		float feather = type.Kind == MapCatalog.TerrainKind.Land ? type.Feather : 0f;
		// Rebuilt when the shape's type changed to one with a different edge
		if (!_meshes.TryGetValue(shape, out var mesh) || mesh.Feather != feather)
		{
			mesh = TerrainGeometry.Build(shape, _data.Seed, feather);
			mesh.Feather = feather;
			_meshes[shape] = mesh;
		}
		return mesh;
	}

	// Draws every shape into one of the viewports
	private partial class Painter : Node2D
	{
		public enum Mode { Color, Sea, Blight }

		private readonly TerrainRenderer _owner;
		private readonly Mode _mode;

		public Painter(TerrainRenderer owner, Mode mode)
		{
			_owner = owner;
			_mode = mode;
			TextureRepeat = TextureRepeatEnum.Enabled;
			TextureFilter = TextureFilterEnum.Linear;
			// Masks are flat colors; only the live shapes use textures (filter set by the owner)
		}

		public override void _Draw()
		{
			var data = _owner._data;
			if (data == null)
				return;
			var size = data.Size;
			var rid = GetCanvasItem();
			var baseType = MapCatalog.Terrain(data.Base);
			// Base terrain over the whole map
			var corners = new[] { Vector2.Zero, new Vector2(size.X, 0), size, new Vector2(0, size.Y) };
			DrawFill(rid, baseType, corners, new[] { 0, 1, 2, 0, 2, 3 }, null, null);
			foreach (var shape in data.Terrain)
			{
				var mesh = _owner.MeshOf(shape);
				if (mesh.Indices.Length == 0)
					continue;
				DrawFill(rid, MapCatalog.Terrain(shape.Type), mesh.Points, mesh.Indices, mesh.FeatherIndices, mesh.Alpha);
			}
		}

		// featherIndices/alpha: the soft band around land shapes, drawn only in color
		private void DrawFill(Rid rid, MapCatalog.TerrainType type, Vector2[] points, int[] indices, int[] featherIndices, float[] alpha)
		{
			Color color;
			switch (_mode)
			{
				case Mode.Color:
					if (type.Kind == MapCatalog.TerrainKind.Overlay)
						return; // blight is drawn by terrain.gdshader from its mask
					var uvs = new Vector2[points.Length];
					float tile = TerrainTextures.TileSizeOf(type.Id);
					for (int i = 0; i < points.Length; i++)
						uvs[i] = points[i] / tile;
					var colors = Fill(points.Length, Colors.White);
					if (alpha != null)
						for (int i = 0; i < colors.Length; i++)
							colors[i].A = alpha[i];
					if (featherIndices != null && featherIndices.Length > 0)
					{
						var all = new int[indices.Length + featherIndices.Length];
						indices.CopyTo(all, 0);
						featherIndices.CopyTo(all, indices.Length);
						indices = all;
					}
					RenderingServer.CanvasItemAddTriangleArray(rid, indices, points, colors, uvs,
						texture: TerrainTextures.Get(type.Id).GetRid());
					return;
				case Mode.Sea:
					if (type.Kind == MapCatalog.TerrainKind.Overlay)
						return;
					color = type.Kind == MapCatalog.TerrainKind.Sea ? Colors.White : Colors.Black;
					break;
				default:
					color = type.Kind == MapCatalog.TerrainKind.Overlay ? Colors.White : Colors.Black;
					break;
			}
			RenderingServer.CanvasItemAddTriangleArray(rid, indices, points, Fill(points.Length, color));
		}

		private static Color[] Fill(int count, Color color)
		{
			var colors = new Color[count];
			Array.Fill(colors, color);
			return colors;
		}
	}
}
