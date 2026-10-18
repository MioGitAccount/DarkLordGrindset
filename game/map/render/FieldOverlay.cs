using Godot;
using System.Collections.Generic;

// Draws layer 3 (fields and connections) straight from map data, for the editor and map previews.
// The game itself uses real field nodes instead.
public partial class FieldOverlay : Node2D
{
	public const float FieldRadius = 16f;

	public MapData Data;
	public FieldData Selected;
	public FieldData Hovered;
	public bool ShowIds = true;
	// Connection being dragged in the editor
	public FieldData LinkFrom;
	public Vector2 LinkTo;

	private static readonly Color LineColor = new(0.95f, 0.92f, 0.85f, 0.85f);
	private static readonly Color LineShadow = new(0f, 0f, 0f, 0.5f);

	public override void _Draw()
	{
		if (Data == null)
			return;
		var byId = new Dictionary<string, FieldData>();
		foreach (var f in Data.Fields)
			if (f.Id != null)
				byId[f.Id] = f;
		foreach (var c in Data.Connections)
		{
			if (!byId.TryGetValue(c[0], out var a) || !byId.TryGetValue(c[1], out var b))
				continue;
			DrawDashedLine(MapData.V(a.Pos) + Vector2.One, MapData.V(b.Pos) + Vector2.One, LineShadow, 3f, 8f);
			DrawDashedLine(MapData.V(a.Pos), MapData.V(b.Pos), LineColor, 2f, 8f);
		}
		if (LinkFrom != null)
			DrawLine(MapData.V(LinkFrom.Pos), LinkTo, new Color(1f, 0.82f, 0.35f), 2.5f, true);

		var font = ThemeDB.FallbackFont;
		foreach (var f in Data.Fields)
		{
			var pos = MapData.V(f.Pos);
			var marker = FieldMarkers.Get(FieldTypeManager.GetFieldType(f.Type ?? ""));
			float r = FieldRadius;
			if (marker != null)
				DrawTextureRect(marker, new Rect2(pos - Vector2.One * r, Vector2.One * r * 2), false);
			else
				DrawCircle(pos, r, new Color(0.8f, 0.2f, 0.8f));
			if (f == Selected)
				DrawArc(pos, r + 3, 0, Mathf.Tau, 40, new Color(1f, 0.82f, 0.35f), 3f, true);
			else if (f == Hovered)
				DrawArc(pos, r + 3, 0, Mathf.Tau, 40, new Color(1f, 1f, 1f, 0.7f), 2f, true);
			if (f.Neutrals != null && f.Neutrals.Count > 0)
			{
				// Red badge with the number of neutral units
				int count = 0;
				foreach (var n in f.Neutrals)
					count += n.Count;
				var badge = pos + new Vector2(r * 0.75f, -r * 0.75f);
				DrawCircle(badge, 7, new Color(0.75f, 0.12f, 0.1f));
				DrawString(font, badge + new Vector2(-4, 4), count.ToString(), HorizontalAlignment.Center, 8, 10, Colors.White);
			}
			if (ShowIds)
				DrawString(font, pos + new Vector2(-40, r + 13), f.Id, HorizontalAlignment.Center, 80, 11, new Color(1, 1, 1, 0.9f));
		}
	}

	public FieldData FieldAt(Vector2 point)
	{
		if (Data == null)
			return null;
		for (int i = Data.Fields.Count - 1; i >= 0; i--)
			if (MapData.V(Data.Fields[i].Pos).DistanceTo(point) <= FieldRadius + 2)
				return Data.Fields[i];
		return null;
	}
}
