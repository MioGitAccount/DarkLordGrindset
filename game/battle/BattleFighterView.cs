using Godot;
using System;
using System.Threading.Tasks;

// One combatant on the battle screen: its sprite (or icon), health and mana bars, status and
// selection rings. Bars show ShownHealth, which the screen updates when a hit lands on screen,
// so the bar doesn't drop before the attack animation reaches the target.
public partial class BattleFighterView : Control
{
	public static readonly Vector2 ViewSize = new Vector2(130, 152);
	// Where the unit's feet are inside the view
	public static readonly Vector2 Feet = new Vector2(65, 136);
	private const float SpriteScale = 1.5f;
	private const float IconSize = 72;

	private static readonly Color HealthColor = new Color(0.78f, 0.2f, 0.18f);
	private static readonly Color HealthBack = new Color(0.12f, 0.08f, 0.08f, 0.9f);
	private static readonly Color ManaColor = new Color(0.3f, 0.5f, 0.95f);
	private static readonly Color SelectedColor = new Color(1f, 0.82f, 0.35f);
	private static readonly Color TargetableColor = new Color(1f, 0.35f, 0.3f);
	private static readonly Color ActiveColor = new Color(1f, 1f, 1f);
	private static readonly Color PlayerColor = new Color(0.45f, 0.7f, 1f);
	private static readonly Color EnemyColor = new Color(1f, 0.45f, 0.4f);

	public event Action<BattleFighterView, MouseButton> Clicked;

	public Combatant Combatant { get; private set; }
	public int ShownHealth;
	public bool Selected;
	public bool Targetable;
	public bool Active;
	public bool Dead { get; private set; }
	// Extra line under the unit, e.g. "+2 ATK" or the planned order
	public string Status = "";

	private AnimatedSprite2D _sprite;
	private TextureRect _icon;
	private Control _body; // the sprite or the icon; flashes and fades
	private Control _overlay; // bars and text, drawn over the sprite

	public BattleFighterView(Combatant combatant)
	{
		Combatant = combatant;
		ShownHealth = combatant.Health;
	}

	public BattleFighterView() { }

	public Vector2 Chest => Position + Feet + new Vector2(0, -45);

	public override void _Ready()
	{
		Size = ViewSize;
		CustomMinimumSize = ViewSize;
		PivotOffset = Feet;
		MouseFilter = MouseFilterEnum.Stop;
		// The body is a plain Control so flashing/fading never tints the bars
		_body = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = ViewSize };
		AddChild(_body);
		if (Combatant.Sprite != null)
		{
			_sprite = new AnimatedSprite2D
			{
				SpriteFrames = Combatant.Sprite,
				Scale = new Vector2(SpriteScale, SpriteScale),
				Position = Feet + new Vector2(0, -32 * SpriteScale + 4),
				TextureFilter = TextureFilterEnum.Nearest,
				// Sprites face right; enemies stand on the right and face left
				FlipH = !Combatant.IsPlayer,
			};
			_body.AddChild(_sprite);
			PlayIdle();
		}
		else
		{
			_icon = new TextureRect
			{
				Texture = Combatant.Icon,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				Size = new Vector2(IconSize, IconSize),
				Position = Feet - new Vector2(IconSize / 2, IconSize),
				MouseFilter = MouseFilterEnum.Ignore,
			};
			_body.AddChild(_icon);
		}
		_overlay = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = ViewSize };
		_overlay.Draw += DrawOverlay;
		AddChild(_overlay);
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true } mouse
				&& (mouse.ButtonIndex == MouseButton.Left || mouse.ButtonIndex == MouseButton.Right))
		{
			Clicked?.Invoke(this, mouse.ButtonIndex);
			AcceptEvent();
		}
	}

	public void SetAnimationSpeed(float speed)
	{
		if (_sprite != null)
			_sprite.SpeedScale = speed;
	}

	public void PlayIdle()
	{
		if (_sprite != null && _sprite.SpriteFrames.HasAnimation("idle"))
			_sprite.Play("idle");
	}

	// Plays the attack animation once; returns its length in seconds at speed 1 (0 without one)
	public float PlayAttack()
	{
		if (_sprite == null || !_sprite.SpriteFrames.HasAnimation("attack"))
			return 0;
		_sprite.Play("attack");
		var frames = _sprite.SpriteFrames;
		double fps = frames.GetAnimationSpeed("attack");
		return fps > 0 ? (float)(frames.GetFrameCount("attack") / fps) : 0;
	}

	public void Flash(Color color)
	{
		var tween = CreateTween();
		_body.Modulate = color;
		tween.TweenProperty(_body, "modulate", Dead ? DeadModulate : Colors.White, 0.3);
	}

	private static readonly Color DeadModulate = new Color(0.45f, 0.45f, 0.45f, 0.45f);

	public void SetDead(bool animate = true)
	{
		Dead = true;
		Selected = Targetable = Active = false;
		Status = "";
		MouseFilter = MouseFilterEnum.Ignore;
		if (animate)
		{
			var tween = CreateTween().SetParallel();
			tween.TweenProperty(_body, "modulate", DeadModulate, 0.5);
			tween.TweenProperty(_body, "position:y", 10f, 0.5);
		}
		else
		{
			_body.Modulate = DeadModulate;
		}
		_sprite?.Stop();
		QueueRedraw();
		_overlay.QueueRedraw();
	}

	public void Refresh()
	{
		TooltipText = Dead ? "" : Tooltip();
		QueueRedraw();
		_overlay.QueueRedraw();
	}

	private string Tooltip()
	{
		var c = Combatant;
		string text = $"{c.Name}\nHP {c.Health}/{c.MaxHealth}   ATK {c.Attack}   DEF {c.Defence}";
		if (c.MaxMana > 0)
			text += $"   MP {c.Mana}/{c.MaxMana}";
		if (c.Ranged)
			text += "   (ranged)";
		foreach (var passive in c.Passives)
			text += $"\n{passive.Name}: {passive.Description}";
		foreach (var ability in c.Abilities)
			text += $"\n{ability.Name}: {ability.Description}";
		return text;
	}

	public override void _Draw()
	{
		var font = ThemeDB.FallbackFont;
		var c = Combatant;

		// Shadow and rings at the feet
		DrawSetTransform(Feet, 0, new Vector2(1, 0.32f));
		DrawCircle(Vector2.Zero, 34, new Color(0, 0, 0, 0.35f));
		if (!Dead)
		{
			DrawArc(Vector2.Zero, 38, 0, Mathf.Tau, 48, (c.IsPlayer ? PlayerColor : EnemyColor) with { A = 0.55f }, 2f, true);
			if (Active)
				DrawArc(Vector2.Zero, 44, 0, Mathf.Tau, 48, ActiveColor, 3f, true);
			if (Targetable)
				DrawArc(Vector2.Zero, 50, 0, Mathf.Tau, 48, TargetableColor, 3f, true);
			if (Selected)
				DrawArc(Vector2.Zero, 50, 0, Mathf.Tau, 48, SelectedColor, 4f, true);
		}
		DrawSetTransform(Vector2.Zero, 0, Vector2.One);
	}

	private void DrawOverlay()
	{
		if (Dead)
			return;
		var font = ThemeDB.FallbackFont;
		var c = Combatant;

		// Name, health and mana above the head
		const float barWidth = 96;
		float x = (ViewSize.X - barWidth) / 2;
		_overlay.DrawString(font, new Vector2(0, 12), c.Name, HorizontalAlignment.Center, ViewSize.X, 13, Colors.White);
		var healthRect = new Rect2(x, 17, barWidth, 7);
		_overlay.DrawRect(healthRect.Grow(1), HealthBack);
		_overlay.DrawRect(new Rect2(x, 17, barWidth * Mathf.Clamp(ShownHealth / (float)c.MaxHealth, 0, 1), 7), HealthColor);
		_overlay.DrawString(font, new Vector2(0, 41), $"{ShownHealth}/{c.MaxHealth}", HorizontalAlignment.Center, ViewSize.X, 11, new Color(1, 1, 1, 0.85f));
		if (c.MaxMana > 0)
		{
			_overlay.DrawRect(new Rect2(x, 26, barWidth, 3).Grow(1), HealthBack);
			_overlay.DrawRect(new Rect2(x, 26, barWidth * Mathf.Clamp(c.Mana / (float)c.MaxMana, 0, 1), 3), ManaColor);
		}

		// Effects just under the bars, outlined so they read over the sprite
		string effects = "";
		if (c.StunnedTurns > 0)
			effects += "STUNNED  ";
		if (c.BuffAttack > 0)
			effects += $"+{c.BuffAttack} ATK  ";
		if (Status != "")
			effects += Status;
		if (effects != "")
		{
			var at = new Vector2(-20, 55);
			_overlay.DrawStringOutline(font, at, effects.Trim(), HorizontalAlignment.Center, ViewSize.X + 40, 12, 4, Colors.Black);
			_overlay.DrawString(font, at, effects.Trim(), HorizontalAlignment.Center, ViewSize.X + 40, 12, SelectedColor);
		}
	}

	// Moves the view to `target` (its top-left), for melee lunges
	public async Task MoveTo(Vector2 target, double seconds, float speed)
	{
		var tween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
		tween.SetSpeedScale(speed);
		tween.TweenProperty(this, "position", target, seconds);
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	public async Task Pulse(float speed)
	{
		var tween = CreateTween();
		tween.SetSpeedScale(speed);
		tween.TweenProperty(this, "scale", new Vector2(1.15f, 1.15f), 0.15);
		tween.TweenProperty(this, "scale", Vector2.One, 0.2);
		await ToSignal(tween, Tween.SignalName.Finished);
	}
}
