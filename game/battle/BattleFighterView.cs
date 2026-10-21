using Godot;
using System;
using System.Threading.Tasks;

// One combatant on the battle screen: its sprite (or icon), health and mana bars, status and
// selection rings. Bars show ShownHealth, which the screen updates when a hit lands on screen,
// so the bar doesn't drop before the attack animation reaches the target.
public partial class BattleFighterView : Control
{
	public static readonly Vector2 ViewSize = new Vector2(150, 160);
	// Where the unit's feet are inside the view
	public static readonly Vector2 Feet = new Vector2(75, 152);
	private const float SpriteScale = 1.5f;
	private const float IconSize = 72;
	// Painted battle sprites are scaled to this height
	private const float PaintedHeight = 116;

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
	// Painted sprite: a feet-anchored node animated with tweens (breathing, swings, knockback, falling)
	private Node2D _rig;
	private Tween _motion;
	private float _speed = 1;
	private bool _breathing; // idle breathing keeps its own pace, whatever the battle speed
	private TextureRect _icon;
	private Control _body; // the sprite or the icon; flashes and fades
	private Control _overlay; // bars and text, drawn over the sprite

	public BattleFighterView(Combatant combatant)
	{
		Combatant = combatant;
		ShownHealth = combatant.Health;
	}

	public BattleFighterView() { }

	public Vector2 Chest => Position + Feet + new Vector2(0, _rig != null ? -60 : -45);

	public override void _Ready()
	{
		Size = ViewSize;
		CustomMinimumSize = ViewSize;
		PivotOffset = Feet;
		MouseFilter = MouseFilterEnum.Stop;
		// The body is a plain Control so flashing/fading never tints the bars
		_body = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = ViewSize };
		AddChild(_body);
		if (Combatant.BattleSprite != null)
		{
			var texture = Combatant.BattleSprite;
			float scale = PaintedHeight / texture.GetHeight();
			_rig = new Node2D { Position = Feet };
			_rig.AddChild(new Sprite2D
			{
				Texture = texture,
				Centered = false,
				// The cutout's bottom edge has a small margin under the feet
				Offset = new Vector2(-texture.GetWidth() / 2f, -texture.GetHeight() * 0.976f),
				Scale = new Vector2(scale, scale),
				TextureFilter = TextureFilterEnum.LinearWithMipmaps,
				// Art faces right; enemies stand on the right and face left
				FlipH = !Combatant.IsPlayer,
			});
			_body.AddChild(_rig);
			PlayIdle();
		}
		else if (Combatant.Sprite != null)
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
		_speed = speed;
		if (_sprite != null)
			_sprite.SpeedScale = speed;
		if (!_breathing)
			_motion?.SetSpeedScale(speed);
	}

	public void PlayIdle()
	{
		if (_rig != null && !Dead)
			Breathe();
		if (_sprite != null && _sprite.SpriteFrames.HasAnimation("idle"))
			_sprite.Play("idle");
	}

	// Plays the attack animation once; returns its length in seconds at speed 1 (0 without one)
	public float PlayAttack()
	{
		if (_rig != null)
			return Swing();
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
		// Painted art is lit up past white so the flash reads on dark sprites
		_body.Modulate = _rig != null ? new Color(color.R * 2.2f, color.G * 2.2f, color.B * 2.2f) : color;
		tween.TweenProperty(_body, "modulate", Dead ? DeadModulate : Colors.White, 0.3);
		if (_rig != null && !Dead)
			Recoil();
	}

	// --- Painted sprite motion ------------------------------------------------
	// Everything pivots on the feet. Facing: +1 for player units (face right), -1 for enemies.

	private float Facing => Combatant.IsPlayer ? 1 : -1;

	private Tween NewMotion()
	{
		_motion?.Kill();
		_breathing = false;
		_motion = CreateTween();
		_motion.SetSpeedScale(_speed);
		return _motion;
	}

	private void ResetRig()
	{
		_rig.Position = Feet;
		_rig.Rotation = 0;
		_rig.Scale = Vector2.One;
		_rig.Skew = 0;
	}

	// Slow breathing: the body rises and leans a little, out of step with the others
	private void Breathe()
	{
		ResetRig();
		var tween = NewMotion().SetLoops().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tween.SetSpeedScale(1);
		_breathing = true;
		tween.TweenInterval(GD.Randf() * 0.8);
		tween.TweenProperty(_rig, "scale", new Vector2(1.012f, 1.035f), 1.2);
		tween.Parallel().TweenProperty(_rig, "skew", Mathf.DegToRad(-1.2f) * Facing, 1.2);
		tween.TweenProperty(_rig, "scale", Vector2.One, 1.2);
		tween.Parallel().TweenProperty(_rig, "skew", 0f, 1.2);
	}

	// Wind-up (lean back, crouch), strike (lunge forward, squash), recover. The blow lands at about half.
	private float Swing()
	{
		ResetRig();
		float f = Facing;
		float lean = Combatant.AttackLean ? f : 0;
		var tween = NewMotion();
		tween.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(_rig, "rotation", Mathf.DegToRad(-7) * lean, 0.24);
		tween.Parallel().TweenProperty(_rig, "scale", new Vector2(0.96f, 1.04f), 0.24);
		tween.Parallel().TweenProperty(_rig, "position", Feet + new Vector2(-8 * f, 0), 0.24);
		tween.SetTrans(Tween.TransitionType.Back);
		tween.TweenProperty(_rig, "rotation", Mathf.DegToRad(9) * lean, 0.12);
		tween.Parallel().TweenProperty(_rig, "scale", new Vector2(1.08f, 0.93f), 0.12);
		tween.Parallel().TweenProperty(_rig, "position", Feet + new Vector2(16 * f, 0), 0.12);
		tween.TweenInterval(0.12);
		tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(_rig, "rotation", 0f, 0.26);
		tween.Parallel().TweenProperty(_rig, "scale", Vector2.One, 0.26);
		tween.Parallel().TweenProperty(_rig, "position", Feet, 0.26);
		return 0.74f;
	}

	// Knocked back by a hit, then settles
	private void Recoil()
	{
		ResetRig();
		float f = Facing;
		var tween = NewMotion();
		tween.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(_rig, "position", Feet + new Vector2(-14 * f, 0), 0.08);
		tween.Parallel().TweenProperty(_rig, "rotation", Mathf.DegToRad(-6) * f, 0.08);
		tween.Parallel().TweenProperty(_rig, "scale", new Vector2(1.04f, 0.96f), 0.08);
		tween.SetTrans(Tween.TransitionType.Elastic);
		tween.TweenProperty(_rig, "position", Feet, 0.45);
		tween.Parallel().TweenProperty(_rig, "rotation", 0f, 0.45);
		tween.Parallel().TweenProperty(_rig, "scale", Vector2.One, 0.45);
		tween.TweenCallback(Callable.From(Breathe));
	}

	// Staggers, then falls over backwards
	private void Fall(bool animate)
	{
		float f = Facing;
		if (!animate)
		{
			_motion?.Kill();
			ResetRig();
			_rig.Rotation = Mathf.DegToRad(-80) * f;
			return;
		}
		ResetRig();
		var tween = NewMotion();
		tween.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(_rig, "rotation", Mathf.DegToRad(-10) * f, 0.15);
		tween.Parallel().TweenProperty(_rig, "position", Feet + new Vector2(-10 * f, 0), 0.15);
		tween.SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(_rig, "rotation", Mathf.DegToRad(-80) * f, 0.6);
		tween.Parallel().TweenProperty(_rig, "position", Feet + new Vector2(-24 * f, 0), 0.6);
	}

	private static readonly Color DeadModulate = new Color(0.45f, 0.45f, 0.45f, 0.45f);

	public void SetDead(bool animate = true)
	{
		Dead = true;
		Selected = Targetable = Active = false;
		Status = "";
		MouseFilter = MouseFilterEnum.Ignore;
		if (_rig != null)
			Fall(animate);
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

		// Name and health inside one bar, mana under it: a short header, so rows don't overlap
		const float barWidth = 112;
		float x = (ViewSize.X - barWidth) / 2;
		var healthRect = new Rect2(x, 14, barWidth, 14);
		_overlay.DrawRect(healthRect.Grow(1), HealthBack);
		_overlay.DrawRect(new Rect2(x, 14, barWidth * Mathf.Clamp(ShownHealth / (float)c.MaxHealth, 0, 1), 14), HealthColor);
		var label = $"{c.Name}  {ShownHealth}/{c.MaxHealth}";
		var labelAt = new Vector2(0, 25.5f);
		_overlay.DrawStringOutline(font, labelAt, label, HorizontalAlignment.Center, ViewSize.X, 11, 3, HealthBack);
		_overlay.DrawString(font, labelAt, label, HorizontalAlignment.Center, ViewSize.X, 11, Colors.White);
		if (c.MaxMana > 0)
		{
			_overlay.DrawRect(new Rect2(x, 30, barWidth, 3).Grow(1), HealthBack);
			_overlay.DrawRect(new Rect2(x, 30, barWidth * Mathf.Clamp(c.Mana / (float)c.MaxMana, 0, 1), 3), ManaColor);
		}

		// Effects just under the bars, outlined so they read over the sprite
		string effects = "";
		if (c.StunnedTurns > 0)
			effects += "STUNNED  ";
		if (c.RootedTurns > 0)
			effects += "ROOTED  ";
		if (c.TauntTurns > 0 && c.TauntedBy?.IsAlive == true)
			effects += "TAUNTED  ";
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
