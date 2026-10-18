using Godot;
using System;
using System.Collections.Generic;

// Turns terrain shapes into triangles. "Rough" shapes get rounded corners and wobbly
// edges so a polygon with a handful of points still looks like a hand-drawn coastline.
public static class TerrainGeometry
{
	public class Mesh
	{
		public Vector2[] Points = Array.Empty<Vector2>();
		public int[] Indices = Array.Empty<int>();
		// Soft band around land shapes: fades from opaque (Alpha 1) to clear (Alpha 0) outwards.
		// Drawn only in color, never in the coast/blight masks.
		public int[] FeatherIndices = Array.Empty<int>();
		public float[] Alpha = Array.Empty<float>();
		public Rect2 Bounds;
		public float Feather; // feather width this mesh was built with
		// Closed outlines inside Points (start, count) that the feather band grows from
		public readonly List<(int start, int count)> Outlines = new();
	}

	private const float EdgeWobble = 7f;     // world px the edge moves in or out
	private const float WobbleStep = 8f;     // world px between edge points after resampling
	private const float StrokeWidthWobble = 0.25f;

	private static FastNoiseLite _noise;
	private static int _noiseSeed = int.MinValue;

	private static FastNoiseLite Noise(int seed)
	{
		if (_noise == null || _noiseSeed != seed)
		{
			_noise = new FastNoiseLite { Seed = seed, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.015f, FractalOctaves = 3 };
			_noiseSeed = seed;
		}
		return _noise;
	}

	// feather: width in world px of the soft band around the shape (0 = hard edge)
	public static Mesh Build(TerrainShape shape, int seed, float feather = 0f)
	{
		var mesh = new Mesh();
		switch (shape.Shape)
		{
			case "polygon":
				if (shape.Points != null && shape.Points.Count >= 3)
					FillPolygon(mesh, PolygonOutline(ToVectors(shape.Points), shape.IsRough, seed));
				break;
			case "circle":
				if (shape.Center != null && shape.Radius > 0)
					FillPolygon(mesh, PolygonOutline(Circle(MapData.V(shape.Center), shape.Radius), shape.IsRough, seed));
				break;
			case "stroke":
				if (shape.Points != null && shape.Points.Count >= 1)
					BuildStroke(mesh, ToVectors(shape.Points), Math.Max(1f, shape.Width), shape.IsRough, seed, feather);
				break;
		}
		if (feather > 0f && shape.Shape != "stroke")
			AddPolygonFeather(mesh, feather);
		if (mesh.Alpha.Length != mesh.Points.Length)
		{
			mesh.Alpha = new float[mesh.Points.Length];
			Array.Fill(mesh.Alpha, 1f);
		}
		if (mesh.Points.Length > 0)
		{
			var bounds = new Rect2(mesh.Points[0], Vector2.Zero);
			foreach (var p in mesh.Points)
				bounds = bounds.Expand(p);
			mesh.Bounds = bounds;
		}
		return mesh;
	}

	// Rough check whether a point is on a shape (used to pick shapes in the editor)
	public static bool Contains(TerrainShape shape, Vector2 point)
	{
		switch (shape.Shape)
		{
			case "polygon":
				return shape.Points != null && shape.Points.Count >= 3 && Geometry2D.IsPointInPolygon(point, ToVectors(shape.Points));
			case "circle":
				return shape.Center != null && MapData.V(shape.Center).DistanceTo(point) <= shape.Radius;
			case "stroke":
				if (shape.Points == null)
					return false;
				var pts = ToVectors(shape.Points);
				float r = Math.Max(4f, shape.Width / 2f);
				if (pts.Length == 1)
					return pts[0].DistanceTo(point) <= r;
				for (int i = 0; i + 1 < pts.Length; i++)
					if (Geometry2D.GetClosestPointToSegment(point, pts[i], pts[i + 1]).DistanceTo(point) <= r)
						return true;
				return false;
		}
		return false;
	}

	public static Vector2[] ToVectors(List<float[]> points)
	{
		var result = new Vector2[points.Count];
		for (int i = 0; i < points.Count; i++)
			result[i] = MapData.V(points[i]);
		return result;
	}

	public static Vector2[] Circle(Vector2 center, float radius, int segments = 0)
	{
		if (segments <= 0)
			segments = Math.Clamp((int)(radius / 3f), 10, 64);
		var pts = new Vector2[segments];
		for (int i = 0; i < segments; i++)
			pts[i] = center + Vector2.FromAngle(Mathf.Tau * i / segments) * radius;
		return pts;
	}

	// Ramer-Douglas-Peucker: drops points that lie within `epsilon` of the simplified line
	public static List<Vector2> Simplify(List<Vector2> points, float epsilon)
	{
		if (points.Count < 3)
			return new List<Vector2>(points);
		var keep = new bool[points.Count];
		keep[0] = keep[^1] = true;
		var stack = new Stack<(int, int)>();
		stack.Push((0, points.Count - 1));
		while (stack.Count > 0)
		{
			var (a, b) = stack.Pop();
			float maxDist = 0;
			int index = -1;
			for (int i = a + 1; i < b; i++)
			{
				float d = Geometry2D.GetClosestPointToSegment(points[i], points[a], points[b]).DistanceTo(points[i]);
				if (d > maxDist)
				{
					maxDist = d;
					index = i;
				}
			}
			if (index >= 0 && maxDist > epsilon)
			{
				keep[index] = true;
				stack.Push((a, index));
				stack.Push((index, b));
			}
		}
		var result = new List<Vector2>();
		for (int i = 0; i < points.Count; i++)
			if (keep[i])
				result.Add(points[i]);
		return result;
	}

	private static Vector2[] PolygonOutline(Vector2[] pts, bool rough, int seed)
	{
		if (!rough)
			return pts;
		// Few, far apart points (typed by hand or AI): round the corners first
		if (AverageEdge(pts, true) > 30f)
			pts = Chaikin(pts, true, 3);
		pts = Resample(pts, true, WobbleStep);
		var noise = Noise(seed);
		var result = new Vector2[pts.Length];
		for (int i = 0; i < pts.Length; i++)
		{
			Vector2 prev = pts[(i - 1 + pts.Length) % pts.Length];
			Vector2 next = pts[(i + 1) % pts.Length];
			Vector2 normal = (next - prev).Orthogonal().Normalized();
			result[i] = pts[i] + normal * noise.GetNoise2D(pts[i].X, pts[i].Y) * EdgeWobble;
		}
		return result;
	}

	private static void FillPolygon(Mesh mesh, Vector2[] outline)
	{
		var indices = Geometry2D.TriangulatePolygon(outline);
		if (indices.Length > 0)
		{
			mesh.Points = outline;
			mesh.Indices = indices;
			mesh.Outlines.Add((0, outline.Length));
			return;
		}
		// Self-intersecting outline (e.g. a lasso that crossed itself): let Clipper split it into simple pieces
		var points = new List<Vector2>();
		var allIndices = new List<int>();
		var pieces = Geometry2D.OffsetPolygon(outline, 0.01f);
		// Holes wind the other way than outer pieces; the biggest piece is always an outer one
		bool outerClockwise = false;
		float biggest = -1;
		foreach (var piece in pieces)
		{
			float area = Math.Abs(SignedArea(piece));
			if (area > biggest)
			{
				biggest = area;
				outerClockwise = Geometry2D.IsPolygonClockwise(piece);
			}
		}
		foreach (var piece in pieces)
		{
			if (Geometry2D.IsPolygonClockwise(piece) != outerClockwise)
				continue; // the fill ignores holes
			var pieceIndices = Geometry2D.TriangulatePolygon(piece);
			foreach (int i in pieceIndices)
				allIndices.Add(points.Count + i);
			mesh.Outlines.Add((points.Count, piece.Length));
			points.AddRange(piece);
		}
		mesh.Points = points.ToArray();
		mesh.Indices = allIndices.ToArray();
	}

	// Outer band around every outline: inner edge on the outline (opaque), outer edge `feather` further out (clear)
	private static void AddPolygonFeather(Mesh mesh, float feather)
	{
		var points = new List<Vector2>(mesh.Points);
		var alpha = new List<float>();
		for (int i = 0; i < points.Count; i++)
			alpha.Add(1f);
		var indices = new List<int>();
		foreach (var (start, count) in mesh.Outlines)
		{
			var outline = new Vector2[count];
			Array.Copy(mesh.Points, start, outline, 0, count);
			// Outward is to the right of the edge direction when the area is positive
			float sign = SignedArea(outline) > 0 ? 1f : -1f;
			int outer = points.Count;
			for (int i = 0; i < count; i++)
			{
				Vector2 dir = outline[(i + 1) % count] - outline[(i - 1 + count) % count];
				var normal = new Vector2(dir.Y, -dir.X).Normalized() * sign;
				points.Add(outline[i] + normal * feather);
				alpha.Add(0f);
			}
			for (int i = 0; i < count; i++)
			{
				int j = (i + 1) % count;
				indices.AddRange(new[] { start + i, start + j, outer + j, start + i, outer + j, outer + i });
			}
		}
		mesh.Points = points.ToArray();
		mesh.Alpha = alpha.ToArray();
		mesh.FeatherIndices = indices.ToArray();
	}

	// A stroke is a quad per segment plus a round cap at every point, all in one mesh
	private static void BuildStroke(Mesh mesh, Vector2[] pts, float width, bool rough, int seed, float feather)
	{
		if (rough && pts.Length >= 3 && AverageEdge(pts, false) > 30f)
			pts = Chaikin(pts, false, 3);
		pts = Resample(pts, false, Math.Max(3f, Math.Min(width * 0.5f, 16f)));
		var noise = Noise(seed + 7);
		var radii = new float[pts.Length];
		for (int i = 0; i < pts.Length; i++)
		{
			float wobble = rough ? 1f + noise.GetNoise2D(pts[i].X * 2f, pts[i].Y * 2f) * StrokeWidthWobble : 1f;
			radii[i] = width / 2f * wobble;
		}

		var points = new List<Vector2>();
		var indices = new List<int>();
		for (int i = 0; i < pts.Length; i++)
		{
			// Round joint
			int segments = Math.Clamp((int)(radii[i] / 1.5f), 8, 24);
			int center = points.Count;
			points.Add(pts[i]);
			for (int s = 0; s < segments; s++)
				points.Add(pts[i] + Vector2.FromAngle(Mathf.Tau * s / segments) * radii[i]);
			for (int s = 0; s < segments; s++)
			{
				indices.Add(center);
				indices.Add(center + 1 + s);
				indices.Add(center + 1 + (s + 1) % segments);
			}
			if (i + 1 >= pts.Length)
				break;
			// Segment to the next point
			Vector2 dir = pts[i + 1] - pts[i];
			if (dir.LengthSquared() < 0.0001f)
				continue;
			Vector2 n = dir.Normalized().Orthogonal();
			int q = points.Count;
			points.Add(pts[i] + n * radii[i]);
			points.Add(pts[i] - n * radii[i]);
			points.Add(pts[i + 1] - n * radii[i + 1]);
			points.Add(pts[i + 1] + n * radii[i + 1]);
			indices.AddRange(new[] { q, q + 1, q + 2, q, q + 2, q + 3 });
		}
		var alpha = new List<float>();
		for (int i = 0; i < points.Count; i++)
			alpha.Add(1f);
		var featherIndices = new List<int>();
		if (feather > 0f)
		{
			for (int i = 0; i < pts.Length; i++)
			{
				// Ring around each round joint
				int segments = Math.Clamp((int)((radii[i] + feather) / 1.5f), 10, 32);
				int inner = points.Count;
				for (int s = 0; s < segments; s++)
				{
					var dir = Vector2.FromAngle(Mathf.Tau * s / segments);
					points.Add(pts[i] + dir * radii[i]);
					alpha.Add(1f);
					points.Add(pts[i] + dir * (radii[i] + feather));
					alpha.Add(0f);
				}
				for (int s = 0; s < segments; s++)
				{
					int a0 = inner + s * 2, a1 = inner + ((s + 1) % segments) * 2;
					featherIndices.AddRange(new[] { a0, a1, a1 + 1, a0, a1 + 1, a0 + 1 });
				}
				if (i + 1 >= pts.Length)
					break;
				// Strips along both sides of the segment
				Vector2 d = pts[i + 1] - pts[i];
				if (d.LengthSquared() < 0.0001f)
					continue;
				Vector2 n = d.Normalized().Orthogonal();
				foreach (float side in new[] { 1f, -1f })
				{
					int q = points.Count;
					points.Add(pts[i] + n * side * radii[i]);
					points.Add(pts[i + 1] + n * side * radii[i + 1]);
					points.Add(pts[i + 1] + n * side * (radii[i + 1] + feather));
					points.Add(pts[i] + n * side * (radii[i] + feather));
					alpha.AddRange(new[] { 1f, 1f, 0f, 0f });
					featherIndices.AddRange(new[] { q, q + 1, q + 2, q, q + 2, q + 3 });
				}
			}
		}
		mesh.Points = points.ToArray();
		mesh.Indices = indices.ToArray();
		mesh.Alpha = alpha.ToArray();
		mesh.FeatherIndices = featherIndices.ToArray();
	}

	private static float SignedArea(Vector2[] pts)
	{
		float area = 0;
		for (int i = 0; i < pts.Length; i++)
			area += pts[i].Cross(pts[(i + 1) % pts.Length]);
		return area / 2f;
	}

	private static float AverageEdge(Vector2[] pts, bool closed)
	{
		if (pts.Length < 2)
			return 0;
		float total = 0;
		int count = closed ? pts.Length : pts.Length - 1;
		for (int i = 0; i < count; i++)
			total += pts[i].DistanceTo(pts[(i + 1) % pts.Length]);
		return total / count;
	}

	private static Vector2[] Chaikin(Vector2[] pts, bool closed, int iterations)
	{
		for (int it = 0; it < iterations; it++)
		{
			var result = new List<Vector2>();
			int count = closed ? pts.Length : pts.Length - 1;
			if (!closed)
				result.Add(pts[0]);
			for (int i = 0; i < count; i++)
			{
				Vector2 a = pts[i];
				Vector2 b = pts[(i + 1) % pts.Length];
				result.Add(a.Lerp(b, 0.25f));
				result.Add(a.Lerp(b, 0.75f));
			}
			if (!closed)
				result.Add(pts[^1]);
			pts = result.ToArray();
		}
		return pts;
	}

	// Points spaced about `step` apart along the line
	private static Vector2[] Resample(Vector2[] pts, bool closed, float step)
	{
		if (pts.Length < 2)
			return pts;
		var result = new List<Vector2> { pts[0] };
		int count = closed ? pts.Length : pts.Length - 1;
		for (int i = 0; i < count; i++)
		{
			Vector2 a = pts[i];
			Vector2 b = pts[(i + 1) % pts.Length];
			float length = a.DistanceTo(b);
			int pieces = Math.Max(1, (int)MathF.Ceiling(length / step));
			for (int s = 1; s <= pieces; s++)
				result.Add(a.Lerp(b, s / (float)pieces));
		}
		if (closed)
			result.RemoveAt(result.Count - 1); // last point is the first again
		return result.ToArray();
	}
}
