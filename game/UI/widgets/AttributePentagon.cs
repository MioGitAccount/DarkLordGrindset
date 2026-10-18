using Godot;

// Radar chart of a unit's SpecialAttributes. Corners go clockwise from the top:
// Deadliness, Strength, Durability, Cleverness, Quickness.
// The outer pentagon is always drawn at the maximum value (10 everywhere), so the unit's
// own pentagon inside it shows at a glance how close each attribute is to the cap.
public partial class AttributePentagon : Control
{
	[Export] public Color MaxFillColor = new Color(1f, 1f, 1f, 0.05f);
	[Export] public Color MaxOutlineColor = new Color(0.85f, 0.65f, 0.3f, 0.9f);
	[Export] public Color GridColor = new Color(1f, 1f, 1f, 0.12f);
	[Export] public Color FillColor = new Color(0.85f, 0.25f, 0.2f, 0.5f);
	[Export] public Color OutlineColor = new Color(1f, 0.45f, 0.3f);
	[Export] public Color LabelColor = new Color(0.9f, 0.85f, 0.75f);
	[Export] public int FontSize = 11;
	// Full attribute names ("Deadliness 3") instead of short ones ("DEA 3"); for big charts
	[Export] public bool FullNames;

	private SpecialAttributes _attributes;

	public SpecialAttributes Attributes
	{
		get => _attributes;
		set
		{
			_attributes = value;
			TooltipText = BuildTooltip();
			QueueRedraw();
		}
	}

	public override void _Ready()
	{
		if (CustomMinimumSize == Vector2.Zero)
			CustomMinimumSize = new Vector2(150, 130);
		Resized += QueueRedraw;
	}

	private static Vector2 Corner(Vector2 center, float radius, int index)
	{
		float angle = -Mathf.Pi / 2 + index * Mathf.Tau / SpecialAttributes.Count;
		return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
	}

	private static Vector2[] Pentagon(Vector2 center, float radius, bool closed)
	{
		int count = SpecialAttributes.Count;
		var points = new Vector2[closed ? count + 1 : count];
		for (int i = 0; i < points.Length; i++)
			points[i] = Corner(center, radius, i % count);
		return points;
	}

	private string LabelText(int index)
	{
		string name = FullNames ? SpecialAttributes.Names[index] : SpecialAttributes.ShortNames[index];
		return _attributes != null ? $"{name} {_attributes.Get(index)}" : name;
	}

	public override void _Draw()
	{
		int count = SpecialAttributes.Count;
		var font = GetThemeDefaultFont();

		// Leave room around the pentagon for the corner labels
		float labelWidth = 0;
		if (font != null)
			for (int i = 0; i < count; i++)
				labelWidth = Mathf.Max(labelWidth, font.GetStringSize(LabelText(i), HorizontalAlignment.Left, -1, FontSize).X);
		float labelGap = FontSize * 0.8f;
		var center = Size / 2 + new Vector2(0, FontSize * 0.4f);
		float radius = Mathf.Max(4f, Mathf.Min(Size.X / 2 - labelWidth * 0.6f - labelGap, Size.Y / 2 - FontSize * 1.6f - labelGap));
		float line = Mathf.Max(1f, radius / 60f);

		// Maximum pentagon (all attributes at 10): faint fill and a solid outline
		DrawColoredPolygon(Pentagon(center, radius, false), MaxFillColor);

		// Inner guide rings every 2 points, plus spokes
		for (int ring = 2; ring < SpecialAttributes.MaxValue; ring += 2)
			DrawPolyline(Pentagon(center, radius * ring / SpecialAttributes.MaxValue, true), GridColor, line, true);
		for (int i = 0; i < count; i++)
			DrawLine(center, Corner(center, radius, i), GridColor, line, true);
		DrawPolyline(Pentagon(center, radius, true), MaxOutlineColor, line * 2f, true);

		// The unit's own attributes
		if (_attributes != null)
		{
			var shape = new Vector2[count + 1];
			for (int i = 0; i < count; i++)
			{
				// Keep a tiny radius for zero values so the polygon never degenerates
				float value = Mathf.Max(_attributes.Get(i), 0.05f);
				shape[i] = Corner(center, radius * value / SpecialAttributes.MaxValue, i);
			}
			shape[count] = shape[0];
			DrawColoredPolygon(shape[..count], FillColor);
			DrawPolyline(shape, OutlineColor, line * 2f, true);
			for (int i = 0; i < count; i++)
				DrawCircle(shape[i], line * 2.5f, OutlineColor);
		}

		if (font == null)
			return;
		for (int i = 0; i < count; i++)
		{
			string text = LabelText(i);
			var textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1, FontSize);
			var corner = Corner(center, radius + labelGap, i);
			var direction = (corner - center).Normalized();
			// Push the label outward from its corner so it never overlaps the outline
			var topLeft = corner - textSize / 2 + new Vector2(direction.X * textSize.X / 2, direction.Y * textSize.Y / 2);
			DrawString(font, topLeft + new Vector2(0, font.GetAscent(FontSize)), text, HorizontalAlignment.Left, -1, FontSize, LabelColor);
		}
	}

	private string BuildTooltip()
	{
		if (_attributes == null)
			return "";
		var lines = new string[SpecialAttributes.Count];
		for (int i = 0; i < lines.Length; i++)
			lines[i] = $"{SpecialAttributes.Names[i]}: {_attributes.Get(i)}/{SpecialAttributes.MaxValue}  ({UIText.AttributeBonus(_attributes, i)})";
		return string.Join("\n", lines);
	}
}
