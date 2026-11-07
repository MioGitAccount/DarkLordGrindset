using Godot;
using System;
using System.Collections.Generic;

// One building spot on the castle screen: draws its picture with its feet on Anchor (bottom centre)
// and Width wide, and counts a click only on the picture's own pixels (not its see-through parts).
// Glows gold when selected and pale on hover; Caption is a small plate drawn at its feet.
// Placed by BuildWindow from CastleLayout.
public partial class CastleSpot : Control
{
	private static readonly Dictionary<Texture2D, Image> HitImages = new();

	private static readonly Color SelectedGlow = new Color(1f, 0.78f, 0.3f, 0.85f);
	private static readonly Color HoverGlow = new Color(1f, 1f, 1f, 0.45f);

	public string Key;
	public event Action Clicked;
	// Editing the layout: dragged to a new place (in scene pixels) or scaled by the mouse wheel
	public event Action<Vector2> Dragged;
	public event Action<float> Scaled;

	public Texture2D Picture { get; private set; }
	public string Caption = "";
	public string Detail = "";
	public Color CaptionColor = new Color(1f, 0.92f, 0.75f);
	public Color DetailColor = new Color(0.8f, 0.75f, 0.68f);
	// Show the caption plate even without hover or selection
	public bool AlwaysCaption;
	public bool Selected;
	public bool Dim;
	public bool Editing;

	private bool _hover;
	private bool _dragging;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		MouseEntered += () => { _hover = true; QueueRedraw(); };
		MouseExited += () => { _hover = false; QueueRedraw(); };
	}

	// Feet at `anchor`, `width` pixels wide, height from the picture
	public void Place(Texture2D picture, Vector2 anchor, float width)
	{
		Picture = picture;
		float height = picture == null ? width * 0.6f : width * picture.GetHeight() / picture.GetWidth();
		Size = new Vector2(width, height);
		Position = anchor - new Vector2(width / 2, height);
		QueueRedraw();
	}

	public override bool _HasPoint(Vector2 point)
	{
		if (!new Rect2(Vector2.Zero, Size).HasPoint(point))
			return false;
		if (Picture == null || Editing)
			return true;
		if (!HitImages.TryGetValue(Picture, out var image))
			HitImages[Picture] = image = Picture.GetImage();
		if (image == null)
			return true;
		int x = Math.Clamp((int)(point.X / Size.X * image.GetWidth()), 0, image.GetWidth() - 1);
		int y = Math.Clamp((int)(point.Y / Size.Y * image.GetHeight()), 0, image.GetHeight() - 1);
		return image.GetPixel(x, y).A > 0.3f;
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton button)
		{
			if (Editing && button.Pressed && (button.ButtonIndex == MouseButton.WheelUp || button.ButtonIndex == MouseButton.WheelDown))
			{
				Scaled?.Invoke(button.ButtonIndex == MouseButton.WheelUp ? 1.04f : 1 / 1.04f);
				AcceptEvent();
				return;
			}
			if (button.ButtonIndex != MouseButton.Left)
				return;
			if (Editing)
				_dragging = button.Pressed;
			else if (button.Pressed)
				Clicked?.Invoke();
			AcceptEvent();
		}
		else if (@event is InputEventMouseMotion motion && _dragging)
		{
			Dragged?.Invoke(Position + new Vector2(Size.X / 2, Size.Y) + motion.Relative);
			AcceptEvent();
		}
	}

	public override void _Draw()
	{
		var rect = new Rect2(Vector2.Zero, Size);
		if (Picture != null)
		{
			if (Selected || _hover)
			{
				// A glow: the picture tinted and nudged out in 8 directions, under the real one
				var glow = Selected ? SelectedGlow : HoverGlow;
				float r = Selected ? 3 : 2;
				for (int i = 0; i < 8; i++)
				{
					var offset = Vector2.Right.Rotated(i * Mathf.Pi / 4) * r;
					DrawTextureRect(Picture, new Rect2(offset, Size), false, glow with { A = glow.A * 0.5f });
				}
			}
			var tint = Dim ? new Color(0.7f, 0.7f, 0.72f) : _hover || Selected ? new Color(1.12f, 1.1f, 1.05f) : Colors.White;
			DrawTextureRect(Picture, rect, false, tint);
		}
		else
			DrawRect(rect, new Color(0.2f, 0.15f, 0.12f, 0.7f));
		if (Editing)
		{
			DrawRect(rect, new Color(0.3f, 1f, 0.5f), false, 1);
			DrawCaption(Key, "", Colors.White);
			return;
		}
		if (Caption != "" && (AlwaysCaption || Selected || _hover))
			DrawCaption(Caption, Detail, CaptionColor);
	}

	// A dark plate at the feet with one or two centred lines
	private void DrawCaption(string text, string detail, Color color)
	{
		var font = GetThemeDefaultFont();
		const int size = 13;
		var lines = detail == "" ? new[] { text } : new[] { text, detail };
		float width = 0;
		foreach (var line in lines)
			width = Math.Max(width, font.GetStringSize(line, HorizontalAlignment.Left, -1, size).X);
		float lineHeight = font.GetHeight(size);
		var plate = new Rect2(Size.X / 2 - width / 2 - 6, Size.Y - lineHeight * lines.Length - 6, width + 12, lineHeight * lines.Length + 4);
		DrawStyleBox(CaptionBox, plate);
		for (int i = 0; i < lines.Length; i++)
		{
			float lineWidth = font.GetStringSize(lines[i], HorizontalAlignment.Left, -1, size).X;
			var at = new Vector2(Size.X / 2 - lineWidth / 2, plate.Position.Y + 2 + font.GetAscent(size) + i * lineHeight);
			DrawString(font, at, lines[i], HorizontalAlignment.Left, -1, size, i == 0 ? color : DetailColor);
		}
	}

	private static readonly StyleBoxFlat CaptionBox = MakeCaptionBox();

	private static StyleBoxFlat MakeCaptionBox()
	{
		var box = new StyleBoxFlat { BgColor = new Color(0.06f, 0.05f, 0.05f, 0.78f), BorderColor = new Color(0.55f, 0.42f, 0.22f, 0.9f) };
		box.SetBorderWidthAll(1);
		box.SetCornerRadiusAll(4);
		return box;
	}
}
