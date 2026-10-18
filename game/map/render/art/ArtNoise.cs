using System;

// Fast seeded gradient noise for painting sprites pixel by pixel (no engine calls per pixel)
public class ArtNoise
{
	private readonly int[] _perm = new int[512];

	public ArtNoise(int seed)
	{
		var p = new int[256];
		for (int i = 0; i < 256; i++)
			p[i] = i;
		var rng = new Random(seed);
		for (int i = 255; i > 0; i--)
		{
			int j = rng.Next(i + 1);
			(p[i], p[j]) = (p[j], p[i]);
		}
		for (int i = 0; i < 512; i++)
			_perm[i] = p[i & 255];
	}

	private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);

	private float Grad(int hash, float x, float y)
	{
		switch (hash & 7)
		{
			case 0: return x + y;
			case 1: return -x + y;
			case 2: return x - y;
			case 3: return -x - y;
			case 4: return x;
			case 5: return -x;
			case 6: return y;
			default: return -y;
		}
	}

	// About -1..1
	public float Get(float x, float y)
	{
		int xi = (int)MathF.Floor(x);
		int yi = (int)MathF.Floor(y);
		float xf = x - xi;
		float yf = y - yi;
		xi &= 255;
		yi &= 255;
		float u = Fade(xf);
		float v = Fade(yf);
		int aa = _perm[_perm[xi] + yi];
		int ab = _perm[_perm[xi] + yi + 1];
		int ba = _perm[_perm[xi + 1] + yi];
		int bb = _perm[_perm[xi + 1] + yi + 1];
		float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
		float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
		return Lerp(x1, x2, v);
	}

	// Tiles every `period` lattice cells in x and y (x, y in cells): for seamless textures
	public float GetTiled(float x, float y, int periodX, int periodY)
	{
		int xi = (int)MathF.Floor(x);
		int yi = (int)MathF.Floor(y);
		float xf = x - xi;
		float yf = y - yi;
		int x0 = Mod(xi, periodX) & 255, x1 = Mod(xi + 1, periodX) & 255;
		int y0 = Mod(yi, periodY) & 255, y1 = Mod(yi + 1, periodY) & 255;
		float u = Fade(xf);
		float v = Fade(yf);
		float a = Lerp(Grad(_perm[_perm[x0] + y0], xf, yf), Grad(_perm[_perm[x1] + y0], xf - 1, yf), u);
		float b = Lerp(Grad(_perm[_perm[x0] + y1], xf, yf - 1), Grad(_perm[_perm[x1] + y1], xf - 1, yf - 1), u);
		return Lerp(a, b, v);
	}

	// Tiled fBm over a texture: (tx, ty) in 0..1 across the tile, `cells` lattice cells across at the first octave
	public float FbmTiled(float tx, float ty, int cellsX, int cellsY, int octaves = 4)
	{
		float sum = 0, amp = 0.5f, norm = 0;
		for (int i = 0; i < octaves; i++)
		{
			sum += GetTiled(tx * cellsX, ty * cellsY, cellsX, cellsY) * amp;
			norm += amp;
			cellsX *= 2;
			cellsY *= 2;
			amp *= 0.5f;
		}
		return sum / norm;
	}

	private static int Mod(int a, int m) => ((a % m) + m) % m;

	// Several octaves, about -1..1
	public float Fbm(float x, float y, int octaves = 4)
	{
		float sum = 0, amp = 0.5f, norm = 0;
		for (int i = 0; i < octaves; i++)
		{
			sum += Get(x, y) * amp;
			norm += amp;
			x = x * 2.03f + 17.1f;
			y = y * 2.03f + 9.7f;
			amp *= 0.5f;
		}
		return sum / norm;
	}

	// Sharp creases (0..1, 1 on the crease): gullies, cracks
	public float Ridged(float x, float y, int octaves = 3)
	{
		float sum = 0, amp = 0.5f, norm = 0;
		for (int i = 0; i < octaves; i++)
		{
			float n = 1f - MathF.Abs(Get(x, y));
			sum += n * n * amp;
			norm += amp;
			x = x * 2.1f + 5.3f;
			y = y * 2.1f + 1.9f;
			amp *= 0.5f;
		}
		return sum / norm;
	}

	private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
