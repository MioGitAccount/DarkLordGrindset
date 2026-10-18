using Godot;
using System;
using System.Collections.Generic;

// Seamless tiling texture per terrain type in a painted style: broad color blotches, horizontal
// brush strokes, warm/cool shifts and fine grain, in the type's two colors.
// A hand-made texture at res://images/terrain/<id>.png (any size, must tile) replaces it.
public static class TerrainTextures
{
	public const float TileWorldSize = 256f; // world pixels covered by one texture tile
	private const int Pixels = 512;          // texture pixels per tile: 2 per world pixel, sharp when zoomed in

	private static readonly Dictionary<string, Texture2D> Cache = new();
	private static Texture2D _variation;

	public static Texture2D Get(string terrainId)
	{
		if (Cache.TryGetValue(terrainId, out var cached))
			return cached;
		string custom = $"res://images/terrain/{terrainId}.png";
		Texture2D texture = ResourceLoader.Exists(custom)
			? GD.Load<Texture2D>(custom)
			: ImageTexture.CreateFromImage(Generate(MapCatalog.Terrain(terrainId)));
		Cache[terrainId] = texture;
		return texture;
	}

	// Tiling grey noise used by terrain.gdshader for brushy edges and large-scale color variation
	public static Texture2D Variation()
	{
		if (_variation != null)
			return _variation;
		const int size = 256;
		var noise = new ArtNoise(4242);
		var bytes = new byte[size * size];
		for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
				bytes[y * size + x] = ToByte(0.5f + 0.5f * noise.FbmTiled(x / (float)size, y / (float)size, 4, 4, 4) * 1.4f);
		var image = Image.CreateFromData(size, size, false, Image.Format.L8, bytes);
		image.GenerateMipmaps();
		_variation = ImageTexture.CreateFromImage(image);
		return _variation;
	}

	private static Image Generate(MapCatalog.TerrainType type)
	{
		var noise = new ArtNoise(type.Id.GetHashCode());
		bool water = type.Kind == MapCatalog.TerrainKind.Sea;
		var pixels = new byte[Pixels * Pixels * 3];
		for (int y = 0; y < Pixels; y++)
		{
			float ty = y / (float)Pixels;
			for (int x = 0; x < Pixels; x++)
			{
				float tx = x / (float)Pixels;
				float blotch = noise.FbmTiled(tx, ty, 3, 3, 4);           // broad patches
				float medium = noise.FbmTiled(tx + 0.31f, ty, 9, 9, 3);   // smaller patches
				float strokes = noise.FbmTiled(tx, ty + 0.17f, 5, 36, 2); // long horizontal brush strokes
				float grain = noise.GetTiled(tx * 96, ty * 96, 96, 96);   // fine grain
				float hue = noise.FbmTiled(tx + 0.53f, ty + 0.71f, 2, 2, 2);
				Color color;
				if (water)
				{
					float t = 0.5f + blotch * 0.55f + strokes * 0.35f + grain * 0.05f;
					color = type.Dark.Lerp(type.Light, Mathf.Clamp(t, 0f, 1f));
				}
				else
				{
					float t = 0.5f + blotch * 0.75f + medium * 0.35f + strokes * 0.25f + grain * 0.12f;
					color = type.Dark.Lerp(type.Light, Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t, 0f, 1f)));
					// Warm (yellower) and cool (greener/greyer) areas, like layered paint
					float warm = Mathf.Clamp(hue * 1.6f, -1f, 1f);
					color = new Color(color.R * (1f + 0.05f * warm), color.G * (1f + 0.01f * warm), color.B * (1f - 0.08f * warm));
					// Sparse little flecks
					if (grain > 0.55f)
						color = color.Darkened(0.06f);
					else if (grain < -0.6f)
						color = color.Lightened(0.04f);
				}
				int i = (y * Pixels + x) * 3;
				pixels[i] = ToByte(color.R);
				pixels[i + 1] = ToByte(color.G);
				pixels[i + 2] = ToByte(color.B);
			}
		}
		var image = Image.CreateFromData(Pixels, Pixels, false, Image.Format.Rgb8, pixels);
		image.GenerateMipmaps();
		return image;
	}

	private static byte ToByte(float f) => (byte)Math.Clamp((int)(f * 255f + 0.5f), 0, 255);
}
