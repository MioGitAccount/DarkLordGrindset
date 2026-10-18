using Godot;
using System;

// A tiny software painter for map object sprites. Works in "units": u goes left/right from
// the object's middle, v from -1 (top of the object) to 0 (where it stands on the ground),
// so one unit is the object's height. Painted with supersampling, then shrunk for smooth edges.
public class ArtRaster
{
	public readonly float UMin, VMin, UMax, VMax;
	private readonly float _pxPerUnit; // supersampled pixels per unit
	private readonly int _ss;
	private readonly int _w, _h;       // supersampled size
	private float[] _px;               // premultiplied RGBA

	public ArtRaster(float uMin, float uMax, float vMin, float vMax, float pxPerUnit, int supersample = 2)
	{
		UMin = uMin; UMax = uMax; VMin = vMin; VMax = vMax;
		_ss = supersample;
		_pxPerUnit = pxPerUnit * supersample;
		_w = Math.Max(_ss, (int)MathF.Ceiling((uMax - uMin) * pxPerUnit) * _ss);
		_h = Math.Max(_ss, (int)MathF.Ceiling((vMax - vMin) * pxPerUnit) * _ss);
		_px = new float[_w * _h * 4];
	}

	// One supersampled pixel in units
	public float Pixel => 1f / _pxPerUnit;

	// Paints every pixel inside `bounds` for which `inside` is true with `color(u, v)`
	public void Paint(Rect2 bounds, Func<float, float, bool> inside, Func<float, float, Color> color, bool under = false)
	{
		int x0 = Math.Max(0, (int)((bounds.Position.X - UMin) * _pxPerUnit) - 1);
		int x1 = Math.Min(_w - 1, (int)((bounds.End.X - UMin) * _pxPerUnit) + 1);
		int y0 = Math.Max(0, (int)((bounds.Position.Y - VMin) * _pxPerUnit) - 1);
		int y1 = Math.Min(_h - 1, (int)((bounds.End.Y - VMin) * _pxPerUnit) + 1);
		for (int y = y0; y <= y1; y++)
		{
			float v = VMin + (y + 0.5f) / _pxPerUnit;
			for (int x = x0; x <= x1; x++)
			{
				float u = UMin + (x + 0.5f) / _pxPerUnit;
				if (inside(u, v))
					Blend((y * _w + x) * 4, color(u, v), under);
			}
		}
	}

	public void Polygon(Vector2[] pts, Func<float, float, Color> color, bool under = false)
	{
		var bounds = new Rect2(pts[0], Vector2.Zero);
		foreach (var p in pts)
			bounds = bounds.Expand(p);
		Paint(bounds, (u, v) => InPolygon(pts, u, v), color, under);
	}

	public void Polygon(Vector2[] pts, Color color) => Polygon(pts, (_, _) => color);

	public void Ellipse(Vector2 center, Vector2 radius, Func<float, float, Color> color, bool under = false)
	{
		Paint(new Rect2(center - radius, radius * 2), (u, v) =>
		{
			float dx = (u - center.X) / radius.X, dy = (v - center.Y) / radius.Y;
			return dx * dx + dy * dy <= 1f;
		}, color, under);
	}

	public void Line(Vector2 a, Vector2 b, float width, Color color)
	{
		float r = width / 2f;
		var bounds = new Rect2(a, Vector2.Zero).Expand(b).Grow(r);
		Paint(bounds, (u, v) => Geometry2D.GetClosestPointToSegment(new Vector2(u, v), a, b).DistanceSquaredTo(new Vector2(u, v)) <= r * r, (_, _) => color);
	}

	// Soft ground shadow, painted behind what's already there
	public void Shadow(float halfWidth, float alpha = 0.28f)
	{
		var c = new Vector2(halfWidth * 0.12f, 0.005f);
		var r = new Vector2(halfWidth, halfWidth * 0.2f);
		Paint(new Rect2(c - r, r * 2), (u, v) =>
		{
			float dx = (u - c.X) / r.X, dy = (v - c.Y) / r.Y;
			return dx * dx + dy * dy <= 1f;
		}, (u, v) =>
		{
			float dx = (u - c.X) / r.X, dy = (v - c.Y) / r.Y;
			float d = MathF.Sqrt(dx * dx + dy * dy);
			return new Color(0.05f, 0.04f, 0.02f, alpha * (1f - d * d));
		}, under: true);
	}

	// Ink line around everything painted so far, `widthPx` final sprite pixels wide
	public void Outline(Color ink, float widthPx)
	{
		int r = Math.Max(1, (int)MathF.Round(widthPx * _ss));
		// Grow the solid area by r pixels: a row pass then a column pass
		var rows = new bool[_w * _h];
		for (int y = 0; y < _h; y++)
		{
			int last = -100000; // last solid x seen
			for (int x = 0; x < _w + r; x++)
			{
				if (x < _w && _px[(y * _w + x) * 4 + 3] > 0.5f)
					last = x;
				int target = x - r;
				// solid within [target - r, target + r]: the last solid x up to target + r is >= target - r
				if (target >= 0 && last >= target - r)
					rows[y * _w + target] = true;
			}
		}
		var grown = new bool[_w * _h];
		for (int x = 0; x < _w; x++)
		{
			int last = -100000;
			for (int y = 0; y < _h + r; y++)
			{
				if (y < _h && rows[y * _w + x])
					last = y;
				int target = y - r;
				if (target >= 0 && last >= target - r)
					grown[target * _w + x] = true;
			}
		}
		var result = (float[])_px.Clone();
		for (int y = 0; y < _h; y++)
		{
			for (int x = 0; x < _w; x++)
			{
				int i = (y * _w + x) * 4;
				if (_px[i + 3] > 0.98f || !grown[y * _w + x])
					continue;
				// What's there stays on top of the ink
				float a = _px[i + 3];
				result[i] = _px[i] + ink.R * ink.A * (1 - a);
				result[i + 1] = _px[i + 1] + ink.G * ink.A * (1 - a);
				result[i + 2] = _px[i + 2] + ink.B * ink.A * (1 - a);
				result[i + 3] = a + ink.A * (1 - a);
			}
		}
		_px = result;
	}

	public Image ToImage()
	{
		int w = _w / _ss, h = _h / _ss;
		var bytes = new byte[w * h * 4];
		float n = _ss * _ss;
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				float r = 0, g = 0, b = 0, a = 0;
				for (int sy = 0; sy < _ss; sy++)
				{
					for (int sx = 0; sx < _ss; sx++)
					{
						int i = ((y * _ss + sy) * _w + x * _ss + sx) * 4;
						r += _px[i]; g += _px[i + 1]; b += _px[i + 2]; a += _px[i + 3];
					}
				}
				int o = (y * w + x) * 4;
				a /= n;
				// Back from premultiplied
				float k = a > 0.0001f ? 1f / (a * n) : 0;
				bytes[o] = ToByte(r * k);
				bytes[o + 1] = ToByte(g * k);
				bytes[o + 2] = ToByte(b * k);
				bytes[o + 3] = ToByte(a);
			}
		}
		var image = Image.CreateFromData(w, h, false, Image.Format.Rgba8, bytes);
		return image;
	}

	private static byte ToByte(float f) => (byte)Math.Clamp((int)(f * 255f + 0.5f), 0, 255);

	private void Blend(int i, Color c, bool under)
	{
		float a = Math.Clamp(c.A, 0f, 1f);
		if (a <= 0)
			return;
		if (under)
		{
			float da = _px[i + 3];
			_px[i] += c.R * a * (1 - da);
			_px[i + 1] += c.G * a * (1 - da);
			_px[i + 2] += c.B * a * (1 - da);
			_px[i + 3] = da + a * (1 - da);
			return;
		}
		_px[i] = c.R * a + _px[i] * (1 - a);
		_px[i + 1] = c.G * a + _px[i + 1] * (1 - a);
		_px[i + 2] = c.B * a + _px[i + 2] * (1 - a);
		_px[i + 3] = a + _px[i + 3] * (1 - a);
	}

	public static bool InPolygon(Vector2[] pts, float x, float y)
	{
		bool inside = false;
		for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
		{
			if ((pts[i].Y > y) != (pts[j].Y > y) &&
				x < (pts[j].X - pts[i].X) * (y - pts[i].Y) / (pts[j].Y - pts[i].Y) + pts[i].X)
				inside = !inside;
		}
		return inside;
	}
}
