using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

// Painted sprites for map objects, generated once per type and look (variant) with shading,
// texture and an ink outline, in the style of a hand-painted fantasy map. Light comes from the
// top left. Generated sprites are cached in user://map_art so later runs load them instantly.
public static class ObjectSprites
{
	public const float PxPerWorld = 3f; // sprite pixels per world pixel: sharp up to ~3x zoom
	public const int Variants = 6;      // different looks per type
	private const int ArtVersion = 4;   // bump when the painting code changes, to refresh the cache

	public class Sprite
	{
		public Texture2D Texture;
		public Rect2 Units; // area the texture covers, in units of the object's height (see ArtRaster)
	}

	private static readonly Color Ink = new(0.19f, 0.13f, 0.09f, 0.95f);
	private static readonly Dictionary<(string, int), Sprite> Cache = new();

	// Area each type's sprite covers, in units: u across (0 = middle), v up (-1 = top, 0 = ground)
	public static Rect2 UnitsOf(string type) => type switch
	{
		"mountain" or "snow_mountain" => R(-0.8f, 0.8f, -1.06f, 0.1f),
		"hill" => R(-0.85f, 0.85f, -1.06f, 0.12f),
		"pine" => R(-0.4f, 0.4f, -1.06f, 0.1f),
		"oak" => R(-0.55f, 0.55f, -1.06f, 0.1f),
		"dead_tree" => R(-0.5f, 0.5f, -1.06f, 0.1f),
		"bush" => R(-0.95f, 0.95f, -1.08f, 0.15f),
		"rocks" => R(-0.95f, 0.95f, -1.08f, 0.12f),
		"castle" => R(-0.78f, 0.78f, -1.06f, 0.1f),
		"dark_tower" => R(-0.5f, 0.5f, -1.06f, 0.1f),
		"town" => R(-0.85f, 0.85f, -1.06f, 0.1f),
		"village" => R(-0.8f, 0.8f, -1.06f, 0.12f),
		"tower" => R(-0.38f, 0.38f, -1.06f, 0.1f),
		"mine" => R(-0.85f, 0.85f, -1.06f, 0.12f),
		"cave" => R(-0.9f, 0.9f, -1.06f, 0.12f),
		"bridge" => R(-1.62f, 1.62f, -0.62f, 0.62f), // lies flat, centred on its position
		"ship" => R(-0.75f, 0.75f, -1.06f, 0.15f),
		_ => R(-0.5f, 0.5f, -1.06f, 0.1f),
	};

	private static Rect2 R(float u0, float u1, float v0, float v1) => new(u0, v0, u1 - u0, v1 - v0);

	public static Sprite Get(string type, int variant)
	{
		variant = ((variant % Variants) + Variants) % Variants;
		if (Cache.TryGetValue((type, variant), out var sprite))
			return sprite;
		var units = UnitsOf(type);
		string dir = $"user://map_art/v{ArtVersion}";
		string path = $"{dir}/{type}_{variant}.png";
		Image image = null;
		if (FileAccess.FileExists(path))
			image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
		if (image == null)
		{
			var watch = Stopwatch.StartNew();
			image = Paint(type, variant, units);
			DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(dir));
			image.SavePng(ProjectSettings.GlobalizePath(path));
			GD.Print($"Map art: painted {type} #{variant} in {watch.ElapsedMilliseconds} ms");
		}
		image.GenerateMipmaps();
		sprite = new Sprite { Texture = ImageTexture.CreateFromImage(image), Units = units };
		Cache[(type, variant)] = sprite;
		return sprite;
	}

	private static Image Paint(string type, int variant, Rect2 units)
	{
		float size = MapCatalog.ObjectTypes.TryGetValue(type, out var t) ? t.Size : 20f;
		float pxPerUnit = Math.Max(size * PxPerWorld, 48f);
		var raster = new ArtRaster(units.Position.X, units.End.X, units.Position.Y, units.End.Y, pxPerUnit);
		var painter = new Painter(raster, variant, type.GetHashCode(), 1f / pxPerUnit);
		switch (type)
		{
			case "mountain":
			case "snow_mountain": painter.Mountain(); break;
			case "hill": painter.Hill(); break;
			case "pine": painter.Pine(); break;
			case "oak": painter.Oak(); break;
			case "dead_tree": painter.DeadTree(); break;
			case "bush": painter.Bush(); break;
			case "rocks": painter.Rocks(); break;
			case "castle": painter.Castle(); break;
			case "dark_tower": painter.DarkTower(); break;
			case "town": painter.Town(); break;
			case "village": painter.Village(); break;
			case "tower": painter.Tower(); break;
			case "mine": painter.Mine(); break;
			case "cave": painter.Cave(); break;
			case "bridge": painter.Bridge(); break;
			case "ship": painter.Ship(); break;
		}
		raster.Outline(Ink, type == "bridge" ? 1f : 1.3f);
		if (type != "bridge" && type != "ship")
			raster.Shadow(painter.ShadowWidth);
		return raster.ToImage();
	}

	private class Painter
	{
		private readonly ArtRaster _r;
		private readonly ArtNoise _n;
		private readonly Random _rng;
		private readonly int _variant;
		private readonly float _px; // one final sprite pixel in units
		public float ShadowWidth = 0.4f;

		public Painter(ArtRaster raster, int variant, int typeSeed, float px)
		{
			_r = raster;
			_variant = variant;
			_rng = new Random(typeSeed * 31 + variant * 7919);
			_n = new ArtNoise(typeSeed + variant * 101);
			_px = px;
		}

		// ------------------------------------------------------------ helpers

		private float Rnd(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
		private static float Sm(float a, float b, float x) => Mathf.SmoothStep(a, b, x);
		private static Color C(float r, float g, float b) => new(r, g, b);
		private static Color Mul(Color c, float f) => new(c.R * f, c.G * f, c.B * f, c.A);
		private static Color Mix(Color a, Color b, float t) => a.Lerp(b, Math.Clamp(t, 0f, 1f));
		private Color Grain(Color c, float u, float v, float freq, float amount) => Mul(c, 1f + _n.Fbm(u * freq, v * freq, 3) * amount);

		// Lit from the top left: 1 on the lit side of something round at `center`, 0 on the far side
		private static float RoundLight(float u, float v, Vector2 center, Vector2 radius)
		{
			float nx = (u - center.X) / radius.X, ny = (v - center.Y) / radius.Y;
			float nz = MathF.Sqrt(MathF.Max(0f, 1f - nx * nx - ny * ny));
			return Math.Clamp(-0.55f * nx - 0.5f * ny + 0.65f * nz, 0f, 1f);
		}

		// Brick/stone pattern: returns a brightness factor
		private float Stones(float u, float v, float bw, float bh)
		{
			float row = MathF.Floor(v / bh);
			float offset = (row % 2 == 0) ? 0 : bw / 2;
			float col = MathF.Floor((u + offset) / bw);
			float fu = (u + offset) / bw - col, fv = v / bh - row;
			float mortar = Math.Min(Math.Min(fu, 1 - fu) * bw, Math.Min(fv, 1 - fv) * bh);
			float brick = 0.92f + 0.16f * Hash(col, row);
			return mortar < _px * 0.9f ? 0.72f : brick;
		}

		private static float Hash(float a, float b)
		{
			float h = MathF.Sin(a * 127.1f + b * 311.7f) * 43758.5453f;
			return h - MathF.Floor(h);
		}

		// Face of a building: stone or plaster, darker to the right and at the bottom
		private void Wall(float u0, float u1, float v0, float v1, Color color, bool stone, float stoneSize = 0.06f)
		{
			_r.Polygon(new[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) }, (u, v) =>
			{
				float across = (u - u0) / Math.Max(0.001f, u1 - u0);
				var c = Mul(color, 1.08f - 0.25f * across - 0.12f * Sm(v0, v1, v));
				if (stone)
					c = Mul(c, Stones(u, v, stoneSize * 1.6f, stoneSize));
				return Grain(c, u, v, 40, 0.06f);
			});
		}

		// Round tower body with cylinder shading
		private void RoundBody(float cx, float hw, float v0, float v1, Color color, float taper = 0f)
		{
			var pts = new[] { new Vector2(cx - hw * (1 - taper), v0), new Vector2(cx + hw * (1 - taper), v0), new Vector2(cx + hw, v1), new Vector2(cx - hw, v1) };
			_r.Polygon(pts, (u, v) =>
			{
				float s = (u - cx) / hw; // -1 .. 1
				float light = 1.12f - 0.45f * Sm(-0.6f, 1f, s) - 0.15f * MathF.Abs(MathF.Min(0, s + 0.6f));
				return Grain(Mul(color, light * Stones(u, v, 0.075f, 0.045f)), u, v, 40, 0.05f);
			});
		}

		// Pointed (cone or gable) roof, lit on the left, with rows of shingles
		private void Roof(float cx, float hw, float baseV, float apexV, Color color, float overhang = 0.03f)
		{
			var pts = new[] { new Vector2(cx - hw - overhang, baseV), new Vector2(cx, apexV), new Vector2(cx + hw + overhang, baseV) };
			_r.Polygon(pts, (u, v) =>
			{
				float side = Sm(-0.01f, 0.03f, u - cx);
				float rows = (v - apexV) / 0.035f;
				float shingle = (rows - MathF.Floor(rows)) < 0.18f ? 0.8f : 1f;
				return Grain(Mul(color, (1.1f - 0.38f * side) * shingle), u, v, 30, 0.07f);
			});
		}

		private void Crenels(float u0, float u1, float topV, float h, Color color)
		{
			float w = 0.055f;
			for (float x = u0; x + w <= u1 + 0.001f; x += w * 2)
				Wall(x, x + w, topV - h, topV + 0.005f, color, false);
		}

		private void Opening(float cx, float hw, float bottomV, float topV, bool arch = true)
		{
			var pts = new List<Vector2> { new(cx - hw, bottomV), new(cx - hw, topV + (arch ? hw : 0)) };
			if (arch)
				for (int i = 1; i < 8; i++)
				{
					float a = Mathf.Pi + Mathf.Pi * i / 8f;
					pts.Add(new Vector2(cx + MathF.Cos(a) * hw, topV + hw + MathF.Sin(a) * hw));
				}
			pts.Add(new Vector2(cx + hw, topV + (arch ? hw : 0)));
			pts.Add(new Vector2(cx + hw, bottomV));
			_r.Polygon(pts.ToArray(), (u, v) => Mix(C(0.06f, 0.05f, 0.05f), C(0.2f, 0.15f, 0.12f), Sm(topV, bottomV, v)));
		}

		private void Window(float cx, float cy, float hw, float hh, Color glow = default)
		{
			var c = glow.A > 0 ? glow : C(0.1f, 0.08f, 0.08f);
			_r.Polygon(new[] { new Vector2(cx - hw, cy - hh), new Vector2(cx + hw, cy - hh), new Vector2(cx + hw, cy + hh), new Vector2(cx - hw, cy + hh) }, c);
		}

		// Blob outline with a noisy edge, for tree crowns and bushes
		private Func<float, float, bool> Blob(Vector2 c, float r, float bumps, float seed)
		{
			return (u, v) =>
			{
				float dx = u - c.X, dy = v - c.Y;
				float d = MathF.Sqrt(dx * dx + dy * dy);
				float a = MathF.Atan2(dy, dx);
				float edge = r * (1f + bumps * _n.Get(MathF.Cos(a) * 2.2f + seed, MathF.Sin(a) * 2.2f + seed * 1.7f));
				return d <= edge;
			};
		}

		private void LeafBlob(Vector2 c, float r, Color dark, Color light, float seed)
		{
			_r.Paint(new Rect2(c - Vector2.One * r * 1.4f, Vector2.One * r * 2.8f), Blob(c, r, 0.14f, seed), (u, v) =>
			{
				float l = RoundLight(u, v, c, new Vector2(r, r) * 1.15f);
				float leaves = _n.Get(u * 55, v * 55) * 0.5f + _n.Get(u * 110 + 3, v * 110) * 0.3f;
				return Mix(dark, light, l * 0.95f + leaves * 0.18f);
			});
		}

		// ------------------------------------------------------------ nature

		public void Mountain()
		{
			ShadowWidth = 0.72f;
			float side = _variant % 2 == 0 ? 1 : -1; // where the smaller peak and the ridge lean
			var peaks = new List<(float x, float h, float w)> { (Rnd(-0.1f, 0.1f), 1f, Rnd(0.64f, 0.74f)) };
			peaks.Add((peaks[0].x + side * Rnd(0.3f, 0.42f), Rnd(0.5f, 0.66f), Rnd(0.34f, 0.42f)));
			if (_variant % 3 == 0)
				peaks.Add((peaks[0].x - side * Rnd(0.32f, 0.42f), Rnd(0.34f, 0.44f), Rnd(0.28f, 0.34f)));
			bool snow = _variant % 2 == 0;
			var lit = C(0.87f, 0.7f, 0.5f);
			var shade = C(0.56f, 0.38f, 0.25f);
			var snowLit = C(0.97f, 0.97f, 0.98f);
			var snowShade = C(0.7f, 0.74f, 0.83f);
			var ink = C(0.24f, 0.15f, 0.09f);

			// Jagged silhouette; depends only on the column, so it's remembered per column
			var jagMemo = new Dictionary<(int, float), float>();
			float Jag(int i, float u)
			{
				if (!jagMemo.TryGetValue((i, u), out float j))
				{
					j = _n.Fbm(u * 6f + i * 3, 3.3f, 3) * 0.055f + _n.Get(u * 22f + i, 7.7f) * 0.012f;
					jagMemo[(i, u)] = j;
				}
				return j;
			}
			float PeakHeight(int i, float u)
			{
				float d = MathF.Abs(u - peaks[i].x) / peaks[i].w;
				if (d >= 1f)
					return 0f;
				return peaks[i].h * MathF.Pow(1f - d, 1.12f) + Jag(i, u) * (1f - d) * 2f;
			}
			// Front-most peak covering this pixel (the main peak is in front)
			int Dominant(float u, float v)
			{
				for (int i = 0; i < peaks.Count; i++)
					if (-v <= PeakHeight(i, u) && PeakHeight(i, u) > 0.004f)
						return i;
				return -1;
			}
			float Ground(float u) => 0.015f * _n.Get(u * 7f, 1.1f);
			// The ridge runs from the summit down towards the shadow side, winding a little
			float Ridge(int i, float t) => peaks[i].x + (1f - t) * peaks[i].w * 0.32f * (i == 0 ? side : -side * 0.5f + 0.5f)
				+ _n.Fbm(t * 4f, i * 7.1f, 3) * 0.05f * (1f - t);

			_r.Paint(new Rect2(-0.8f, -1.08f, 1.6f, 1.1f), (u, v) => v <= Ground(u) && Dominant(u, v) >= 0, (u, v) =>
			{
				int i = Dominant(u, v);
				var (px, h, w) = peaks[i];
				float height = MathF.Max(0f, -v);
				float t = height / h; // 0 at the foot, 1 at the summit
				float ridge = Ridge(i, t);
				float s = Sm(-0.01f, 0.01f, u - ridge); // 0 lit face, 1 shadow face
				// Distance from the ridge relative to the face width: strokes run down and outwards
				float across = (u - ridge) / MathF.Max(0.06f, (1f - t) * w + 0.06f);
				float gully = _n.Ridged(across * 4.5f + i * 5f, height * 1.6f + across * 0.8f, 3);
				var c = Mix(lit, shade, s);
				// Lit face darkens a little away from the ridge, shadow face darkens near it
				c = Mul(c, s < 0.5f ? 1.02f - 0.12f * MathF.Min(1f, -across) : 0.92f + 0.1f * MathF.Min(1f, across));
				c = Mix(c, Mul(shade, s < 0.5f ? 1f : 0.75f), 0.55f * Sm(0.78f, 0.95f, gully));
				// Snow on high peaks, reaching further down in the gullies
				float snowLine = 0.6f + _n.Fbm(u * 7, v * 7, 3) * 0.1f - gully * 0.16f;
				if (snow && h > 0.75f && t > snowLine)
					c = Mix(snowLit, snowShade, s);
				// Feet blend into the ground
				c = Mix(c, Mul(C(0.62f, 0.56f, 0.36f), 1f - 0.25f * s), 0.55f * Sm(0.1f, 0f, height));
				c = Grain(c, u, v, 50, 0.05f);
				// Ink on the ridge and where a front peak overlaps one behind it
				if (MathF.Abs(u - ridge) < _px * 0.9f && t > 0.1f && t < 0.98f)
					c = Mix(c, ink, 0.6f * Sm(0.1f, 0.3f, t));
				for (int j = 0; j < i; j++)
					if (MathF.Abs(height - PeakHeight(j, u)) < _px * 1.3f)
						c = Mix(c, ink, 0.85f);
				return c;
			});
		}

		public void Hill()
		{
			ShadowWidth = 0.75f;
			float hw = Rnd(0.68f, 0.78f), h = Rnd(0.75f, 0.95f);
			var lit = C(0.76f, 0.71f, 0.44f);
			var shade = C(0.5f, 0.45f, 0.27f);
			float Top(float u) => h * MathF.Pow(MathF.Max(0f, 1f - (u / hw) * (u / hw)), 0.75f) + _n.Fbm(u * 6, 1.3f, 3) * 0.04f;
			_r.Paint(new Rect2(-0.85f, -1.06f, 1.7f, 1.08f), (u, v) => v <= 0.01f * _n.Get(u * 7, 2) && -v <= Top(u), (u, v) =>
			{
				float t = -v / h;
				float s = Sm(-0.12f, 0.12f, u / hw - 0.25f * (1f - t) + 0.05f);
				var c = Mix(lit, shade, s);
				c = Mul(c, 1.04f - 0.1f * MathF.Abs(u / hw));
				float tufts = _n.Ridged(u * 16, v * 26, 2);
				c = Mix(c, Mul(shade, 0.85f), 0.45f * Sm(0.82f, 0.97f, tufts));
				return Grain(c, u, v, 40, 0.06f);
			});
		}

		public void Pine()
		{
			ShadowWidth = 0.32f;
			var dark = Mix(C(0.11f, 0.22f, 0.16f), C(0.14f, 0.25f, 0.14f), Rnd(0, 1));
			var light = Mix(C(0.26f, 0.42f, 0.24f), C(0.32f, 0.45f, 0.22f), Rnd(0, 1));
			// Trunk
			Wall(-0.045f, 0.045f, -0.2f, 0f, C(0.4f, 0.27f, 0.16f), false);
			int tiers = 3 + (_variant % 3 == 0 ? 1 : 0);
			float top = -1f;
			for (int i = tiers - 1; i >= 0; i--)
			{
				float apex = top + i * (0.82f / tiers) * 0.95f;
				float bottom = -0.14f - (tiers - 1 - i) * (0.82f / tiers) * 0.98f;
				bottom = MathF.Max(bottom, apex + 0.3f);
				if (i == tiers - 1)
					bottom = -0.12f;
				float w = 0.2f + 0.14f * i / Math.Max(1, tiers - 1) + Rnd(-0.02f, 0.02f);
				float tierTop = apex, tierBottom = bottom, tierW = w;
				_r.Paint(new Rect2(-0.4f, tierTop, 0.8f, tierBottom - tierTop + 0.06f), (u, v) =>
				{
					float k = (v - tierTop) / (tierBottom - tierTop);
					float saw = 0.035f * MathF.Abs((u * 14f) - MathF.Round(u * 14f));
					if (v > tierBottom - saw * 1.5f + 0.02f)
						return false;
					return MathF.Abs(u) <= tierW * k * (1f + 0.1f * _n.Get(u * 25, v * 25));
				}, (u, v) =>
				{
					float k = (v - tierTop) / (tierBottom - tierTop);
					float s = Sm(-0.06f, 0.1f, u);
					float needles = _n.Get(u * 70, v * 40) * 0.5f;
					var c = Mix(light, dark, s * 0.85f + 0.15f);
					// Darker right under the tier above
					c = Mul(c, 0.8f + 0.25f * Sm(0f, 0.35f, k) + needles * 0.15f);
					return c;
				});
			}
		}

		public void Oak()
		{
			ShadowWidth = 0.4f;
			bool autumn = _variant % 3 == 2;
			var dark = autumn ? C(0.38f, 0.19f, 0.07f) : C(0.16f, 0.27f, 0.12f);
			var light = autumn ? C(0.9f, 0.6f, 0.22f) : C(0.55f, 0.63f, 0.27f);
			Wall(-0.06f, 0.06f, -0.4f, 0f, C(0.38f, 0.26f, 0.16f), false);
			var blobs = new List<(Vector2 c, float r)>();
			int count = 5 + _variant % 2;
			for (int i = 0; i < count; i++)
			{
				float a = Mathf.Tau * i / count + Rnd(-0.3f, 0.3f);
				blobs.Add((new Vector2(MathF.Cos(a) * 0.2f, -0.66f + MathF.Sin(a) * 0.17f), Rnd(0.17f, 0.22f)));
			}
			blobs.Add((new Vector2(Rnd(-0.04f, 0.04f), -0.68f), 0.24f));
			blobs.Sort((a, b) => a.c.Y.CompareTo(b.c.Y));
			int seed = 0;
			foreach (var (c, r) in blobs)
				LeafBlob(c, r, dark, light, seed++ * 3.1f);
		}

		public void DeadTree()
		{
			ShadowWidth = 0.3f;
			var wood = C(0.3f, 0.25f, 0.21f);
			_r.Polygon(new[] { new Vector2(-0.06f, 0), new Vector2(0.06f, 0), new Vector2(0.028f, -0.55f), new Vector2(-0.028f, -0.55f) }, (u, v) => Mul(wood, 1.1f - 0.4f * Sm(-0.05f, 0.05f, u)));
			void Branch(Vector2 from, float angle, float length, float width, int depth)
			{
				var to = from + Vector2.FromAngle(angle) * length;
				_r.Line(from, to, width, Mul(wood, 1f - depth * 0.05f));
				if (depth >= 3)
					return;
				Branch(to, angle - Rnd(0.35f, 0.7f), length * 0.62f, width * 0.68f, depth + 1);
				Branch(to, angle + Rnd(0.35f, 0.7f), length * 0.58f, width * 0.68f, depth + 1);
			}
			Branch(new Vector2(0, -0.5f), -Mathf.Pi / 2 - Rnd(0.25f, 0.45f), 0.3f, 0.055f, 1);
			Branch(new Vector2(0, -0.45f), -Mathf.Pi / 2 + Rnd(0.25f, 0.5f), 0.28f, 0.05f, 1);
			Branch(new Vector2(0, -0.58f), -Mathf.Pi / 2 + Rnd(-0.1f, 0.1f), 0.32f, 0.05f, 1);
		}

		public void Bush()
		{
			ShadowWidth = 0.8f;
			var dark = C(0.15f, 0.25f, 0.11f);
			var light = C(0.5f, 0.6f, 0.26f);
			LeafBlob(new Vector2(-0.38f, -0.42f), 0.42f, dark, light, 1);
			LeafBlob(new Vector2(0.4f, -0.44f), 0.44f, dark, light, 2);
			LeafBlob(new Vector2(0.02f, -0.52f), 0.5f, dark, light, 3);
		}

		private void Boulder(Vector2 c, Vector2 r, Color color, float seed)
		{
			var pts = new Vector2[11];
			for (int i = 0; i < pts.Length; i++)
			{
				float a = Mathf.Tau * i / pts.Length;
				float k = 1f + 0.18f * _n.Get(MathF.Cos(a) * 1.5f + seed, MathF.Sin(a) * 1.5f);
				pts[i] = c + new Vector2(MathF.Cos(a) * r.X, MathF.Min(MathF.Sin(a) * r.Y, 0.02f)) * k;
			}
			_r.Polygon(pts, (u, v) =>
			{
				float l = RoundLight(u, v, c + new Vector2(0, r.Y * 0.2f), r * 1.1f);
				float facet = MathF.Floor(l * 4f) / 4f; // flat faces like cut stone
				var col = Mul(color, 0.55f + 0.6f * (l * 0.4f + facet * 0.6f));
				float crack = _n.Ridged(u * 14 + seed, v * 14, 2);
				return Grain(Mul(col, 1f - 0.25f * Sm(0.85f, 0.98f, crack)), u, v, 40, 0.06f);
			});
		}

		public void Rocks()
		{
			ShadowWidth = 0.85f;
			var stone = C(0.66f, 0.63f, 0.58f);
			Boulder(new Vector2(-0.3f, -0.05f), new Vector2(0.45f, 0.85f), stone, 1);
			Boulder(new Vector2(0.38f, -0.02f), new Vector2(0.38f, 0.55f), Mul(stone, 0.92f), 5);
		}

		// ------------------------------------------------------------ buildings

		public void Castle()
		{
			ShadowWidth = 0.75f;
			var stone = C(0.66f, 0.65f, 0.66f);
			var roof = _variant % 2 == 0 ? C(0.72f, 0.2f, 0.15f) : C(0.28f, 0.33f, 0.62f);
			// Keep at the back
			RoundBody(0.02f, 0.17f, -0.8f, -0.3f, Mul(stone, 1.04f));
			Roof(0.02f, 0.17f, -0.8f, -1.04f, roof);
			Window(0.0f, -0.62f, 0.025f, 0.045f);
			// Curtain wall with battlements and gate
			Wall(-0.5f, 0.5f, -0.38f, 0f, stone, true);
			Crenels(-0.5f, 0.5f, -0.38f, 0.06f, stone);
			Opening(0.02f, 0.09f, 0f, -0.22f);
			// Corner towers
			foreach (float x in new[] { -0.55f, 0.55f })
			{
				RoundBody(x, 0.14f, -0.64f, 0.0f, stone, 0.05f);
				Roof(x, 0.14f, -0.64f, -0.92f, roof);
				Window(x - 0.01f, -0.42f, 0.02f, 0.04f);
			}
			// Flag on the keep
			_r.Line(new Vector2(0.02f, -1.04f), new Vector2(0.02f, -1.14f), _px * 1.5f, C(0.25f, 0.2f, 0.15f));
		}

		public void DarkTower()
		{
			ShadowWidth = 0.45f;
			var stone = C(0.25f, 0.21f, 0.3f);
			// Buttresses
			RoundBody(-0.25f, 0.1f, -0.38f, 0f, Mul(stone, 0.9f), 0.2f);
			RoundBody(0.25f, 0.1f, -0.32f, 0f, Mul(stone, 0.9f), 0.2f);
			// Tapered tower
			RoundBody(0f, 0.2f, -0.8f, 0f, stone, 0.35f);
			// Spiked crown and spire
			for (int i = -2; i <= 2; i++)
				_r.Polygon(new[] { new Vector2(i * 0.06f - 0.03f, -0.79f), new Vector2(i * 0.06f, -0.88f - (i == 0 ? 0 : 0.02f)), new Vector2(i * 0.06f + 0.03f, -0.79f) }, Mul(stone, 0.8f));
			Roof(0f, 0.08f, -0.84f, -1.05f, C(0.18f, 0.14f, 0.22f), 0f);
			// Glowing window with a halo
			_r.Ellipse(new Vector2(0, -0.6f), new Vector2(0.12f, 0.12f), (u, v) =>
			{
				float d = new Vector2(u, v + 0.6f).Length() / 0.12f;
				return new Color(0.95f, 0.35f, 0.85f, 0.45f * (1 - d) * (1 - d));
			});
			Window(0f, -0.6f, 0.025f, 0.05f, C(1f, 0.6f, 0.95f));
			Opening(0f, 0.06f, 0f, -0.13f);
		}

		private void House(float x, float baseV, float w, float h, Color walls, Color roof, bool timber, bool thatch = false)
		{
			Wall(x - w / 2, x + w / 2, baseV - h, baseV, walls, false);
			if (timber)
			{
				var beam = C(0.35f, 0.24f, 0.15f);
				_r.Line(new Vector2(x - w / 2, baseV - h * 0.5f), new Vector2(x + w / 2, baseV - h * 0.5f), _px * 1.3f, beam);
				_r.Line(new Vector2(x - w * 0.25f, baseV - h), new Vector2(x - w * 0.25f, baseV), _px * 1.2f, beam);
				_r.Line(new Vector2(x + w * 0.25f, baseV - h), new Vector2(x + w * 0.25f, baseV), _px * 1.2f, beam);
			}
			Window(x + w * 0.25f, baseV - h * 0.62f, w * 0.08f, h * 0.1f);
			Opening(x - w * 0.18f, w * 0.1f, baseV, baseV - h * 0.45f, false);
			if (thatch)
			{
				var pts = new[] { new Vector2(x - w / 2 - 0.04f, baseV - h + 0.02f), new Vector2(x - w * 0.15f, baseV - h - h * 0.75f), new Vector2(x + w * 0.15f, baseV - h - h * 0.75f), new Vector2(x + w / 2 + 0.04f, baseV - h + 0.02f) };
				_r.Polygon(pts, (u, v) =>
				{
					float side = Sm(-0.02f, 0.06f, u - x);
					float straw = 1f + 0.18f * _n.Get(u * 90, v * 12);
					return Mul(roof, (1.1f - 0.35f * side) * straw);
				});
			}
			else
				Roof(x, w / 2, baseV - h, baseV - h - h * 0.85f, roof);
		}

		public void Town()
		{
			ShadowWidth = 0.82f;
			var walls = C(0.89f, 0.83f, 0.69f);
			var roof = C(0.3f, 0.33f, 0.6f);
			// Back row, church, front row
			House(-0.5f, -0.3f, 0.26f, 0.22f, walls, Mul(roof, 0.9f), true);
			House(0.48f, -0.32f, 0.26f, 0.22f, walls, Mul(roof, 0.9f), true);
			House(-0.22f, -0.38f, 0.22f, 0.2f, walls, Mul(roof, 0.95f), true);
			RoundBody(0.12f, 0.09f, -0.7f, -0.25f, C(0.82f, 0.76f, 0.62f));
			Roof(0.12f, 0.09f, -0.7f, -1.04f, Mul(roof, 0.85f));
			Window(0.12f, -0.55f, 0.02f, 0.04f);
			House(-0.6f, 0f, 0.24f, 0.24f, walls, roof, true);
			House(-0.3f, 0f, 0.26f, 0.27f, walls, Mul(roof, 1.08f), true);
			House(0.02f, 0f, 0.24f, 0.24f, walls, roof, true);
			House(0.32f, 0f, 0.26f, 0.28f, walls, Mul(roof, 1.05f), true);
			House(0.62f, 0f, 0.22f, 0.22f, walls, roof, true);
		}

		public void Village()
		{
			ShadowWidth = 0.75f;
			var walls = C(0.86f, 0.78f, 0.62f);
			var roof = _variant % 2 == 0 ? C(0.78f, 0.5f, 0.24f) : C(0.7f, 0.56f, 0.3f);
			House(-0.32f, -0.3f, 0.4f, 0.32f, walls, Mul(roof, 0.92f), true, true);
			House(0.36f, -0.22f, 0.36f, 0.3f, walls, roof, false, true);
			House(-0.02f, 0f, 0.44f, 0.34f, walls, Mul(roof, 1.05f), true, true);
		}

		public void Tower()
		{
			ShadowWidth = 0.32f;
			var stone = C(0.6f, 0.5f, 0.43f);
			RoundBody(0f, 0.2f, -0.82f, 0f, stone, 0.15f);
			// Overhanging top with battlements
			RoundBody(0f, 0.23f, -0.9f, -0.78f, Mul(stone, 1.05f));
			for (int i = 0; i < 4; i++)
			{
				float x = -0.23f + i * 0.13f;
				Wall(x, x + 0.07f, -0.99f, -0.89f, stone, false);
			}
			Window(0f, -0.55f, 0.022f, 0.06f);
			Window(-0.05f, -0.3f, 0.02f, 0.05f);
			Opening(0f, 0.06f, 0f, -0.16f);
		}

		public void Mine()
		{
			ShadowWidth = 0.8f;
			Boulder(new Vector2(0f, 0f), new Vector2(0.7f, 1.0f), C(0.66f, 0.52f, 0.38f), 2);
			// Timber-framed entrance going into the dark
			_r.Polygon(new[] { new Vector2(-0.2f, 0), new Vector2(-0.18f, -0.42f), new Vector2(0.18f, -0.42f), new Vector2(0.2f, 0) },
				(u, v) => Mix(C(0.02f, 0.02f, 0.02f), C(0.18f, 0.13f, 0.1f), Sm(-0.42f, 0f, v) * 0.6f + MathF.Abs(u) * 1.5f));
			var wood = C(0.55f, 0.37f, 0.2f);
			_r.Line(new Vector2(-0.2f, 0), new Vector2(-0.18f, -0.45f), _px * 3.5f, wood);
			_r.Line(new Vector2(0.2f, 0), new Vector2(0.18f, -0.45f), _px * 3.5f, wood);
			_r.Line(new Vector2(-0.25f, -0.45f), new Vector2(0.25f, -0.45f), _px * 4f, Mul(wood, 1.1f));
			// Ore cart
			Wall(0.32f, 0.6f, -0.18f, -0.04f, C(0.45f, 0.45f, 0.48f), false);
			_r.Ellipse(new Vector2(0.46f, -0.2f), new Vector2(0.12f, 0.06f), (u, v) => Grain(C(0.4f, 0.32f, 0.3f), u, v, 80, 0.3f));
			foreach (float x in new[] { 0.38f, 0.54f })
				_r.Ellipse(new Vector2(x, -0.03f), new Vector2(0.035f, 0.035f), (_, _) => C(0.15f, 0.12f, 0.1f));
		}

		public void Cave()
		{
			ShadowWidth = 0.85f;
			Boulder(new Vector2(-0.05f, 0f), new Vector2(0.8f, 1.0f), C(0.6f, 0.59f, 0.56f), 4);
			_r.Polygon(new[] { new Vector2(-0.32f, 0), new Vector2(-0.28f, -0.32f), new Vector2(-0.12f, -0.5f), new Vector2(0.1f, -0.52f), new Vector2(0.27f, -0.34f), new Vector2(0.32f, 0) },
				(u, v) => Mix(C(0.02f, 0.02f, 0.03f), C(0.22f, 0.2f, 0.2f), Sm(-0.25f, 0.02f, v) * 0.5f + Sm(0.12f, 0.32f, MathF.Abs(u)) * 0.6f));
		}

		public void Bridge()
		{
			var stone = C(0.72f, 0.7f, 0.66f);
			// Deck (lighter in the middle, like an arch catching light)
			_r.Polygon(new[] { new Vector2(-1.5f, -0.38f), new Vector2(1.5f, -0.38f), new Vector2(1.5f, 0.38f), new Vector2(-1.5f, 0.38f) }, (u, v) =>
			{
				float plank = (u * 9f - MathF.Floor(u * 9f)) < 0.1f ? 0.82f : 1f;
				return Grain(Mul(stone, (0.85f + 0.2f * (1 - MathF.Abs(u) / 1.5f)) * plank), u, v, 30, 0.06f);
			});
			// Parapets
			foreach (float y in new[] { -0.45f, 0.45f })
				_r.Polygon(new[] { new Vector2(-1.58f, y - 0.1f), new Vector2(1.58f, y - 0.1f), new Vector2(1.58f, y + 0.1f), new Vector2(-1.58f, y + 0.1f) },
					(u, v) => Mul(stone, (y < 0 ? 1.05f : 0.8f) * Stones(u, v, 0.18f, 0.1f)));
		}

		public void Ship()
		{
			var wood = C(0.5f, 0.32f, 0.18f);
			var hull = new List<Vector2>();
			for (int i = 0; i <= 12; i++)
			{
				float t = i / 12f;
				hull.Add(new Vector2(-0.68f + 1.36f * t, -0.3f + 0.28f * MathF.Sin(t * Mathf.Pi)));
			}
			hull.Add(new Vector2(0.72f, -0.4f));
			hull.Add(new Vector2(-0.72f, -0.36f));
			_r.Polygon(hull.ToArray(), (u, v) =>
			{
				float plank = ((v * 30f) - MathF.Floor(v * 30f)) < 0.15f ? 0.78f : 1f;
				return Grain(Mul(wood, (1.1f - 0.4f * Sm(-0.4f, 0f, v)) * plank), u, v, 30, 0.08f);
			});
			_r.Line(new Vector2(-0.15f, -0.32f), new Vector2(-0.15f, -1.0f), _px * 2.5f, Mul(wood, 0.8f));
			_r.Line(new Vector2(0.25f, -0.32f), new Vector2(0.25f, -0.85f), _px * 2.5f, Mul(wood, 0.8f));
			var sail = C(0.95f, 0.92f, 0.84f);
			void Sail(float x, float top, float bottom, float w)
			{
				var pts = new[] { new Vector2(x - w, top), new Vector2(x + w, top), new Vector2(x + w * 1.1f, bottom), new Vector2(x - w * 1.1f, bottom) };
				_r.Polygon(pts, (u, v) => Mul(sail, 1.05f - 0.3f * Sm(-w, w * 1.1f, u - x) - 0.08f * MathF.Sin((v - top) / (bottom - top) * Mathf.Pi)));
			}
			Sail(-0.15f, -0.95f, -0.45f, 0.24f);
			Sail(0.25f, -0.8f, -0.45f, 0.18f);
			_r.Polygon(new[] { new Vector2(-0.15f, -1.0f), new Vector2(0.02f, -0.97f), new Vector2(-0.15f, -0.94f) }, C(0.75f, 0.15f, 0.12f));
		}
	}
}
