using Godot;

// Code-drawn placeholder icon for an item: a frame in the tier's colour and a simple shape for its
// kind, tinted by its material. Painted icons can replace this later.
// With ShowLock it darkens and shows the seconds left while the item is locked on an agent.
public partial class ItemIcon : Control
{
	private ItemDefinition _definition;
	public ItemDefinition Definition
	{
		get => _definition;
		set { _definition = value; QueueRedraw(); }
	}
	// The item instance, for the lock countdown (optional)
	public Item Item;
	public bool ShowLock;
	// Text drawn faintly in an empty slot ("Hand", "Jewelry", ...)
	public string EmptyText = "";

	private static readonly Color Background = new(0.1f, 0.09f, 0.1f);
	private static readonly Color Outline = new(0.06f, 0.05f, 0.06f);
	private int _shownLockSeconds = -1;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Process(double delta)
	{
		if (!ShowLock || Item == null)
			return;
		int seconds = (int)System.Math.Ceiling(Item.LockSecondsLeft);
		if (seconds != _shownLockSeconds)
		{
			_shownLockSeconds = seconds;
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		var size = Size;
		float s = Mathf.Min(size.X, size.Y);
		var frame = new StyleBoxFlat
		{
			BgColor = Background,
			BorderColor = _definition != null ? ItemDefinition.TierColor(_definition.Tier) : new Color(0.3f, 0.27f, 0.25f),
		};
		frame.SetBorderWidthAll(2);
		frame.SetCornerRadiusAll(4);
		DrawStyleBox(frame, new Rect2(Vector2.Zero, size));

		var font = GetThemeDefaultFont();
		if (_definition == null)
		{
			if (!string.IsNullOrEmpty(EmptyText))
				DrawString(font, new Vector2(0, size.Y / 2 + 4), EmptyText, HorizontalAlignment.Center, size.X, 10, new Color(1, 1, 1, 0.35f));
			return;
		}

		// Shapes are drawn in a 0..1 box with a margin
		float margin = s * 0.14f;
		var origin = (size - new Vector2(s, s)) / 2 + new Vector2(margin, margin);
		DrawShape(_definition.Kind, ItemDefinition.MaterialColor(_definition.Material), origin, s - 2 * margin);

		// Tier pips in the bottom-left corner
		for (int i = 0; i < _definition.Tier; i++)
			DrawCircle(new Vector2(6 + i * 6, size.Y - 6), 2f, ItemDefinition.TierColor(_definition.Tier));

		if (ShowLock && Item != null && Item.IsLocked)
		{
			// Dimmed, with the seconds left along the bottom
			DrawRect(new Rect2(Vector2.Zero, size), new Color(0, 0, 0, 0.35f));
			int fontSize = Mathf.Clamp((int)(s * 0.26f), 8, 13);
			DrawRect(new Rect2(2, size.Y - fontSize - 4, size.X - 4, fontSize + 2), new Color(0, 0, 0, 0.6f));
			DrawString(font, new Vector2(0, size.Y - 4), $"{_shownLockSeconds}s", HorizontalAlignment.Center, size.X, fontSize, new Color(1f, 0.8f, 0.45f));
		}
	}

	private void DrawShape(ItemKind kind, Color c, Vector2 o, float w)
	{
		Vector2 P(float x, float y) => o + new Vector2(x * w, y * w);
		Vector2[] Poly(params float[] xy)
		{
			var points = new Vector2[xy.Length / 2];
			for (int i = 0; i < points.Length; i++)
				points[i] = P(xy[2 * i], xy[2 * i + 1]);
			return points;
		}
		void Fill(Vector2[] points, Color color)
		{
			DrawColoredPolygon(points, color);
			var closed = new Vector2[points.Length + 1];
			points.CopyTo(closed, 0);
			closed[^1] = points[0];
			DrawPolyline(closed, Outline, 1.5f, true);
		}
		void Line(float x1, float y1, float x2, float y2, float width, Color color) => DrawLine(P(x1, y1), P(x2, y2), color, width * w, true);
		void Circle(float x, float y, float r, Color color) { DrawCircle(P(x, y), r * w, color); DrawArc(P(x, y), r * w, 0, Mathf.Tau, 24, Outline, 1.5f, true); }
		void Ring(float x, float y, float r, float width, Color color) => DrawArc(P(x, y), r * w, 0, Mathf.Tau, 32, color, width * w, true);

		Color dark = c.Darkened(0.35f);
		Color light = c.Lightened(0.3f);
		Color gem = new(0.8f, 0.15f, 0.2f);
		Color wood = ItemDefinition.MaterialColor(ItemMaterial.Wood);

		switch (kind)
		{
			case ItemKind.Ring:
				Ring(0.5f, 0.58f, 0.27f, 0.11f, c);
				Fill(Poly(0.5f, 0.12f, 0.62f, 0.26f, 0.5f, 0.38f, 0.38f, 0.26f), gem);
				break;
			case ItemKind.Bracelet:
				DrawSetTransform(P(0.5f, 0.5f), 0, new Vector2(1f, 0.6f));
				DrawArc(Vector2.Zero, 0.38f * w, 0, Mathf.Tau, 32, c, 0.12f * w, true);
				DrawSetTransform(Vector2.Zero, 0, Vector2.One);
				Circle(0.5f, 0.27f, 0.07f, light);
				break;
			case ItemKind.Necklace:
			case ItemKind.Amulet:
				DrawArc(P(0.5f, 0.2f), 0.36f * w, 0.15f, Mathf.Pi - 0.15f, 20, c, 0.06f * w, true);
				if (kind == ItemKind.Necklace)
					Circle(0.5f, 0.68f, 0.12f, gem);
				else
					Fill(Poly(0.5f, 0.5f, 0.68f, 0.72f, 0.5f, 0.98f, 0.32f, 0.72f), c);
				break;
			case ItemKind.Coin:
				Circle(0.5f, 0.5f, 0.4f, c);
				Ring(0.5f, 0.5f, 0.28f, 0.04f, dark);
				Line(0.5f, 0.33f, 0.5f, 0.67f, 0.06f, dark);
				break;
			case ItemKind.Cloak:
				Fill(Poly(0.38f, 0.05f, 0.62f, 0.05f, 0.95f, 0.95f, 0.05f, 0.95f), c);
				Line(0.5f, 0.15f, 0.5f, 0.9f, 0.03f, dark);
				Circle(0.5f, 0.1f, 0.07f, ItemDefinition.MaterialColor(ItemMaterial.Gold));
				break;
			case ItemKind.Robe:
				Fill(Poly(0.35f, 0.05f, 0.65f, 0.05f, 0.95f, 0.4f, 0.8f, 0.45f, 0.8f, 0.95f, 0.2f, 0.95f, 0.2f, 0.45f, 0.05f, 0.4f), c);
				Line(0.2f, 0.6f, 0.8f, 0.6f, 0.05f, dark);
				break;
			case ItemKind.Jerkin:
			case ItemKind.Breastplate:
			case ItemKind.Chainmail:
				Fill(Poly(0.2f, 0.08f, 0.38f, 0.08f, 0.5f, 0.2f, 0.62f, 0.08f, 0.8f, 0.08f, 0.92f, 0.35f, 0.8f, 0.4f, 0.78f, 0.95f, 0.22f, 0.95f, 0.2f, 0.4f, 0.08f, 0.35f), c);
				if (kind == ItemKind.Breastplate)
				{
					Line(0.5f, 0.25f, 0.5f, 0.9f, 0.04f, light);
					Line(0.25f, 0.55f, 0.75f, 0.55f, 0.03f, dark);
				}
				else if (kind == ItemKind.Chainmail)
				{
					for (float y = 0.32f; y < 0.92f; y += 0.12f)
						for (float x = 0.3f; x < 0.75f; x += 0.12f)
							DrawArc(P(x, y), 0.035f * w, 0, Mathf.Tau, 8, dark, 1f, true);
				}
				else
					Line(0.5f, 0.2f, 0.5f, 0.95f, 0.03f, dark);
				break;
			case ItemKind.Helmet:
				Fill(Poly(0.15f, 0.7f, 0.15f, 0.45f, 0.25f, 0.22f, 0.5f, 0.1f, 0.75f, 0.22f, 0.85f, 0.45f, 0.85f, 0.7f), c);
				Fill(Poly(0.08f, 0.68f, 0.92f, 0.68f, 0.92f, 0.8f, 0.08f, 0.8f), dark);
				Line(0.5f, 0.1f, 0.5f, 0.68f, 0.05f, light);
				break;
			case ItemKind.Crown:
				Fill(Poly(0.1f, 0.8f, 0.1f, 0.3f, 0.3f, 0.55f, 0.5f, 0.2f, 0.7f, 0.55f, 0.9f, 0.3f, 0.9f, 0.8f), c);
				Circle(0.5f, 0.66f, 0.07f, gem);
				break;
			case ItemKind.WizardCap:
				Fill(Poly(0.5f, 0.02f, 0.75f, 0.75f, 0.25f, 0.75f), c);
				Fill(Poly(0.05f, 0.75f, 0.95f, 0.75f, 0.85f, 0.9f, 0.15f, 0.9f), dark);
				Circle(0.55f, 0.45f, 0.05f, ItemDefinition.MaterialColor(ItemMaterial.Gold));
				break;
			case ItemKind.Hood:
				Fill(Poly(0.5f, 0.05f, 0.85f, 0.45f, 0.9f, 0.95f, 0.1f, 0.95f, 0.15f, 0.45f), c);
				Fill(Poly(0.5f, 0.3f, 0.68f, 0.55f, 0.62f, 0.85f, 0.38f, 0.85f, 0.32f, 0.55f), new Color(0.05f, 0.04f, 0.05f));
				break;
			case ItemKind.Sword:
				Fill(Poly(0.85f, 0.08f, 0.92f, 0.15f, 0.38f, 0.69f, 0.31f, 0.62f), light);
				Line(0.22f, 0.53f, 0.47f, 0.78f, 0.07f, c);
				Line(0.33f, 0.67f, 0.12f, 0.88f, 0.07f, wood);
				break;
			case ItemKind.Knife:
				Fill(Poly(0.78f, 0.15f, 0.8f, 0.25f, 0.45f, 0.6f, 0.4f, 0.55f), light);
				Line(0.32f, 0.5f, 0.5f, 0.68f, 0.06f, c);
				Line(0.42f, 0.58f, 0.2f, 0.8f, 0.08f, wood);
				break;
			case ItemKind.Axe:
				Line(0.25f, 0.92f, 0.65f, 0.12f, 0.07f, wood);
				Fill(Poly(0.55f, 0.15f, 0.88f, 0.12f, 0.92f, 0.5f, 0.66f, 0.38f), c);
				break;
			case ItemKind.Mace:
				Line(0.25f, 0.9f, 0.6f, 0.4f, 0.07f, wood);
				Circle(0.66f, 0.32f, 0.18f, c);
				Circle(0.66f, 0.32f, 0.06f, dark);
				break;
			case ItemKind.Shield:
				Fill(Poly(0.12f, 0.1f, 0.88f, 0.1f, 0.85f, 0.5f, 0.5f, 0.95f, 0.15f, 0.5f), c);
				Fill(Poly(0.22f, 0.2f, 0.78f, 0.2f, 0.75f, 0.5f, 0.5f, 0.82f, 0.25f, 0.5f), dark);
				Circle(0.5f, 0.45f, 0.08f, light);
				break;
			case ItemKind.Book:
				Fill(Poly(0.18f, 0.1f, 0.85f, 0.1f, 0.85f, 0.9f, 0.18f, 0.9f), c);
				Fill(Poly(0.18f, 0.1f, 0.3f, 0.1f, 0.3f, 0.9f, 0.18f, 0.9f), dark);
				Fill(Poly(0.85f, 0.15f, 0.9f, 0.15f, 0.9f, 0.85f, 0.85f, 0.85f), new Color(0.9f, 0.86f, 0.75f));
				Circle(0.57f, 0.45f, 0.1f, ItemDefinition.MaterialColor(ItemMaterial.Gold));
				break;
			case ItemKind.Staff:
				Line(0.22f, 0.95f, 0.68f, 0.25f, 0.07f, c == wood ? wood : wood.Lerp(c, 0.6f));
				Circle(0.72f, 0.2f, 0.13f, new Color(0.45f, 0.85f, 0.95f));
				break;
		}
	}
}
