using Godot;
using System.Collections.Generic;

// Builds the round map marker for each field type: the biome picture cut into a
// circle with a dark and gold rim. Built once per type and cached.
public static class FieldMarkers
{
	public const int Size = 64;  // texture size; fields draw it at half scale

	private static readonly Color OuterRim = new Color(0.08f, 0.06f, 0.05f);
	private static readonly Color InnerRim = new Color(0.82f, 0.64f, 0.32f);
	private static readonly Dictionary<fieldType, Texture2D> Cache = new();

	public static Texture2D Get(fieldType type)
	{
		if (type?.FieldImage == null)
			return null;
		if (Cache.TryGetValue(type, out var cached))
			return cached;

		var texture = ImageTexture.CreateFromImage(Build(type.FieldImage.GetImage()));
		Cache[type] = texture;
		return texture;
	}

	private static Image Build(Image source)
	{
		var art = (Image)source.Duplicate();
		if (art.IsCompressed())
			art.Decompress();
		art.Convert(Image.Format.Rgba8);

		// Centre square of the (portrait) biome picture
		int side = Mathf.Min(art.GetWidth(), art.GetHeight());
		art = art.GetRegion(new Rect2I((art.GetWidth() - side) / 2, (art.GetHeight() - side) / 2, side, side));
		art.Resize(Size, Size, Image.Interpolation.Lanczos);

		var center = new Vector2(Size, Size) / 2f;
		float radius = Size / 2f - 0.5f;
		var result = Image.Create(Size, Size, false, Image.Format.Rgba8);
		for (int y = 0; y < Size; y++)
		{
			for (int x = 0; x < Size; x++)
			{
				float d = (new Vector2(x + 0.5f, y + 0.5f) - center).Length();
				Color c;
				if (d > radius - 4.5f)
					c = OuterRim;
				else if (d > radius - 7.5f)
					c = InnerRim;
				else
					c = art.GetPixel(x, y);
				// Soft 1px edge so the circle isn't jagged
				c.A = Mathf.Clamp(radius - d + 0.5f, 0f, 1f);
				result.SetPixel(x, y, c);
			}
		}
		return result;
	}
}
