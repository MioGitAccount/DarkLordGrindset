using Godot;
using System;

public partial class FogOfWar : Node2D
{
	[Export] public int MapWidth = 3072;
	[Export] public int MapHeight = 3072;

	private Image _fogImage;
	private ImageTexture _fogTexture;
	private Sprite2D _fogSprite;

	public override void _EnterTree()
	{
		InitializeFog();
	}

	private void InitializeFog()
	{
		// Create black fog image
		_fogImage = Image.Create(MapWidth, MapHeight, false, Image.Format.Rgba8);
		_fogImage.Fill(Colors.Black);

		_fogTexture = ImageTexture.CreateFromImage(_fogImage);

		_fogSprite = new Sprite2D();
		_fogSprite.Texture = _fogTexture;

		// Important: must match your Map sprite setup
		_fogSprite.Centered = true;
		_fogSprite.Position = Vector2.Zero;

		AddChild(_fogSprite);
	}

	public void ResetFog()
	{
		if (_fogImage == null)
			return;

		_fogImage.Fill(Colors.Black);
		_fogTexture.Update(_fogImage);
	}

	public void RevealArea(Vector2 worldPosition, float radius)
	{
		if (_fogImage == null)
			return;

		// Convert world → map local coordinates
		Vector2 localPos = ToLocal(worldPosition);

		// Convert center-based coords → texture (top-left origin)
		Vector2 texturePos = localPos + (Vector2)_fogImage.GetSize() / 2f;

		int r = (int)radius;

		for (int x = -r; x <= r; x++)
		{
			for (int y = -r; y <= r; y++)
			{
				if (x * x + y * y <= r * r)
				{
					int px = (int)texturePos.X + x;
					int py = (int)texturePos.Y + y;

					if (px >= 0 && py >= 0 && px < MapWidth && py < MapHeight)
					{
						_fogImage.SetPixel(px, py, new Color(0, 0, 0, 0));
					}
				}
			}
		}

		_fogTexture.Update(_fogImage);
	}

public async void RevealAnimated(Vector2 worldPosition, float finalRadius, float duration = 1f)
{
	float elapsed = 0f;

	while (elapsed < duration)
	{
		float t = elapsed / duration;

		// Optional: ease-out for nicer feel
		float eased = Mathf.SmoothStep(0f, 1f, t);

		float currentRadius = Mathf.Lerp(0f, finalRadius, eased);

		RevealArea(worldPosition, currentRadius);

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		elapsed += (float)GetProcessDeltaTime();
	}

	// Ensure full radius at end
	RevealArea(worldPosition, finalRadius);
}
}
