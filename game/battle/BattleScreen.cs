using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Full-screen battle on one field: the player's units there against its neutral units.
// 1. Positioning: put up to 5 of your units into the 3 front and 2 back positions.
// 2. Planning: 3 seconds to change targets before anyone moves.
// 3. Fighting: units act one at a time by initiative. Click one of your units, then an enemy to
//    change its target, or an ability button to have it use that ability on its next turn.
// 4. Result: the outcome is written back to the units and the field.
// The world is paused while the screen is open. Built in code, like BuildWindow.
public partial class BattleScreen : Control
{
	private enum Phase { Positioning, Planning, Fighting, Finished }

	private const double PlanningSeconds = 3;
	private const double TurnPause = 0.35;

	private static readonly Color SkyTop = new Color(0.07f, 0.06f, 0.09f);
	private static readonly Color SkyBottom = new Color(0.17f, 0.13f, 0.18f);
	private static readonly Color GroundTop = new Color(0.2f, 0.19f, 0.14f);
	private static readonly Color GroundBottom = new Color(0.1f, 0.1f, 0.08f);
	// Laid over a painted battleground so the fighters stand out from it
	private static readonly Color BackgroundDim = new Color(0, 0, 0, 0.25f);
	private static readonly Color Gold = new Color(1f, 0.82f, 0.4f);
	private static readonly Color Muted = new Color(1f, 1f, 1f, 0.35f);
	private static readonly Color SpellColor = new Color(0.45f, 1f, 0.4f);
	private static readonly Color FireColor = new Color(1f, 0.45f, 0.15f);
	private static readonly Color ManaColor = new Color(0.4f, 0.65f, 1f);
	private static readonly Color HealColor = new Color(0.55f, 1f, 0.55f);
	private static readonly Color CurseColor = new Color(0.75f, 0.45f, 1f);
	private static readonly Color RootColor = new Color(0.6f, 0.8f, 0.3f);
	private static readonly Color TauntColor = new Color(1f, 0.5f, 0.25f);

	private field _field;
	// Painted backdrop of the field (see Battlegrounds); null draws the plain sky and ground
	private Texture2D _background;
	private BattleSimulation _sim;
	private Phase _phase;
	// Every player unit on the field; those not in a slot sit out
	private readonly List<Combatant> _playerUnits = new();
	private readonly List<Combatant> _allEnemies = new();
	private readonly Dictionary<Combatant, BattleFighterView> _views = new();
	private Combatant _selected;
	private CombatAbility _armedAbility;
	private Combatant _activeActor;
	private float _speed = 1f; // the speed button: 1x, 2x, 3x
	// Everything in battle runs at this fraction of real time at 1x
	private const float BasePace = 0.5f;
	private float Pace => _speed * BasePace;
	// Experience each survivor gets, worked out when the result is shown
	private int _experience;
	private bool _paused;
	// Bumped on every open/close, so a battle loop from an earlier battle stops
	private int _battleId;

	private Control _fighters;
	private Control _topBar;
	private Control _bottomBar;
	private Label _title;
	private Label _subtitle;
	private HBoxContainer _turnStrip;
	private Button _startButton;
	private Button _cancelButton;
	private Button _pauseButton;
	private Button _speedButton;
	private Label _bigLabel;
	private Label _hint;
	private HBoxContainer _bench;
	private Control _benchBox;
	private Control _orderBox;
	private Label _orderLabel;
	private HBoxContainer _abilityRow;
	private Label _log;
	private readonly List<string> _logLines = new();
	private PanelContainer _resultPanel;
	private Label _resultTitle;
	private Label _resultText;

	public override void _Ready()
	{
		Visible = false;
		ProcessMode = ProcessModeEnum.Always; // keeps running while the world is paused
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Resized += Layout;

		_fighters = new Control { MouseFilter = MouseFilterEnum.Ignore };
		_fighters.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_fighters);

		BuildTopBar();
		BuildBottomBar();

		_bigLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
		_bigLabel.LabelSettings = new LabelSettings { FontSize = 34, FontColor = Gold, OutlineSize = 6, OutlineColor = Colors.Black };
		_bigLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
		_bigLabel.Position = new Vector2(0, 70);
		AddChild(_bigLabel);

		BuildResultPanel();
	}

	private void BuildTopBar()
	{
		var top = new PanelContainer();
		_topBar = top;
		top.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
		AddChild(top);
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		top.AddChild(row);

		var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1 };
		_title = new Label { ThemeTypeVariation = "HeaderLabel" };
		_subtitle = new Label { ThemeTypeVariation = "MutedLabel" };
		titles.AddChild(_title);
		titles.AddChild(_subtitle);
		row.AddChild(titles);

		_turnStrip = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.4f, Alignment = BoxContainer.AlignmentMode.Center };
		_turnStrip.AddThemeConstantOverride("separation", 4);
		row.AddChild(_turnStrip);

		var buttons = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1, Alignment = BoxContainer.AlignmentMode.End };
		row.AddChild(buttons);
		_cancelButton = MakeButton("Cancel", Close);
		_cancelButton.TooltipText = "Leave without fighting";
		_startButton = MakeButton("Start battle", StartBattle);
		_pauseButton = MakeButton("Pause", () => SetPaused(!_paused));
		_pauseButton.TooltipText = "Pause after the current action (Space)";
		_speedButton = MakeButton("Speed 1x", ToggleSpeed);
		foreach (var button in new[] { _speedButton, _pauseButton, _cancelButton, _startButton })
			buttons.AddChild(button);
	}

	private void BuildBottomBar()
	{
		var bottom = new PanelContainer();
		_bottomBar = bottom;
		bottom.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
		bottom.GrowVertical = GrowDirection.Begin;
		AddChild(bottom);
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		bottom.AddChild(row);

		var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		row.AddChild(left);
		_hint = new Label { ThemeTypeVariation = "MutedLabel" };
		left.AddChild(_hint);

		// Positioning: units waiting for a slot
		var benchBox = new VBoxContainer();
		_benchBox = benchBox;
		_bench = new HBoxContainer();
		_bench.AddThemeConstantOverride("separation", 6);
		benchBox.AddChild(_bench);
		left.AddChild(benchBox);

		// Fighting: orders for the selected unit
		var orderBox = new VBoxContainer();
		_orderBox = orderBox;
		_orderLabel = new Label();
		_abilityRow = new HBoxContainer();
		_abilityRow.AddThemeConstantOverride("separation", 6);
		orderBox.AddChild(_orderLabel);
		orderBox.AddChild(_abilityRow);
		left.AddChild(orderBox);

		_log = new Label
		{
			CustomMinimumSize = new Vector2(430, 108), // 6 lines, so the bar keeps its height
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			VerticalAlignment = VerticalAlignment.Bottom,
		};
		_log.AddThemeFontSizeOverride("font_size", 12);
		row.AddChild(_log);
	}

	private void BuildResultPanel()
	{
		_resultPanel = new PanelContainer { Visible = false };
		AddChild(_resultPanel);
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 10);
		_resultPanel.AddChild(box);
		_resultTitle = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_resultTitle.LabelSettings = new LabelSettings { FontSize = 30, FontColor = Gold, OutlineSize = 4, OutlineColor = Colors.Black };
		box.AddChild(_resultTitle);
		_resultText = new Label { CustomMinimumSize = new Vector2(340, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
		box.AddChild(_resultText);
		box.AddChild(MakeButton("Return to map", FinishAndClose));
	}

	private static Button MakeButton(string text, Action pressed)
	{
		var button = new Button { Text = text, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 34) };
		button.Pressed += pressed;
		return button;
	}

	// --- Opening and closing --------------------------------------------------

	public static bool CanFightOn(field battleField, out string reason)
	{
		reason = "";
		if (battleField == null)
			reason = "Select a field first";
		else if (battleField.presentPlayerUnits.Count == 0)
			reason = "None of your units stand on this field";
		else if (!battleField.presentNeutralUnits.Any(n => (n.GetEffectiveStats()?.health ?? 0) > 0))
			reason = "No enemies on this field";
		return reason == "";
	}

	public void Open(field battleField)
	{
		if (!CanFightOn(battleField, out _))
			return;
		_battleId++;
		_field = battleField;
		_background = Battlegrounds.For(battleField);
		ClearBattle();

		foreach (var playerUnit in battleField.presentPlayerUnits)
			if (IsInstanceValid(playerUnit) && playerUnit.battleStats != null && playerUnit.battleStats.health > 0)
				_playerUnits.Add(Combatant.FromUnit(playerUnit));
		foreach (var neutral in battleField.presentNeutralUnits)
			if ((neutral.GetEffectiveStats()?.health ?? 0) > 0)
				_allEnemies.Add(Combatant.FromNeutral(neutral));

		_sim = new BattleSimulation(_allEnemies);
		BattleFormation.AutoPlace(_sim.PlayerSlots, _playerUnits.Take(BattleFormation.SlotCount));
		foreach (var combatant in _sim.All)
			AddView(combatant);

		_phase = Phase.Positioning;
		_title.Text = $"Battle - {battleField.type?.Name ?? battleField.Name}";
		Log(_allEnemies.Count > BattleFormation.SlotCount
			? $"{_allEnemies.Count} enemies: they will come in waves of {BattleFormation.SlotCount}"
			: _allEnemies.Count == 1 ? "1 enemy stands against you" : $"{_allEnemies.Count} enemies stand against you");

		GetTree().Paused = true;
		Visible = true;
		Layout();
		RefreshAll();
	}

	public void Close()
	{
		_battleId++;
		ClearBattle();
		Visible = false;
		_resultPanel.Visible = false;
		_paused = false;
		GetTree().Paused = false;
	}

	private void ClearBattle()
	{
		foreach (var view in _views.Values)
			view.QueueFree();
		_views.Clear();
		_playerUnits.Clear();
		_allEnemies.Clear();
		_logLines.Clear();
		_log.Text = "";
		_bigLabel.Text = "";
		_selected = null;
		_armedAbility = null;
		_activeActor = null;
		_paused = false;
		_pauseButton.Text = "Pause";
		_resultPanel.Visible = false;
	}

	private void AddView(Combatant combatant)
	{
		var view = new BattleFighterView(combatant);
		view.Clicked += OnFighterClicked;
		_fighters.AddChild(view);
		view.SetAnimationSpeed(Pace);
		_views[combatant] = view;
	}

	// --- Layout --------------------------------------------------------------

	// Where a slot's unit stands (its feet)
	private Vector2 SlotPosition(BattleSide side, int slot)
	{
		float centre = Size.X / 2;
		float front = Mathf.Min(Size.X * 0.12f, 170);
		float back = Mathf.Min(Size.X * 0.27f, 370);
		float offset = BattleFormation.IsBack(slot) ? back : front;
		float x = side == BattleSide.Player ? centre - offset : centre + offset;
		// The three front rows share the space between the bars
		float top = _topBar.Size.Y + BattleFighterView.Feet.Y + 6;
		float bottom = Size.Y - _bottomBar.Size.Y - 20;
		float spacing = Mathf.Max(60, (bottom - top) / 2);
		return new Vector2(x, top + BattleFormation.Lane(slot) * spacing);
	}

	private Vector2 HomePosition(Combatant combatant) => SlotPosition(combatant.Side, combatant.Slot) - BattleFighterView.Feet;

	private void Layout()
	{
		if (!Visible || _sim == null)
			return;
		foreach (var (combatant, view) in _views)
			if (combatant.Slot >= 0)
				view.Position = HomePosition(combatant);
		SortFighters();
		_resultPanel.ResetSize();
		_resultPanel.Position = (Size - _resultPanel.Size) / 2;
		_bigLabel.Size = new Vector2(Size.X, 50);
		_bigLabel.Position = new Vector2(0, Size.Y * 0.14f);
		QueueRedraw();
	}

	// Lower units are drawn in front of higher ones
	private void SortFighters()
	{
		var ordered = _views.Values.OrderBy(view => view.Position.Y).ToList();
		for (int i = 0; i < ordered.Count; i++)
			_fighters.MoveChild(ordered[i], i);
	}

	// --- Drawing -------------------------------------------------------------

	public override void _Process(double delta)
	{
		if (Visible && (_phase == Phase.Planning || _phase == Phase.Fighting))
			QueueRedraw(); // target lines follow the moving units
	}

	public override void _Draw()
	{
		if (_sim == null)
			return;
		if (_background != null)
		{
			// Cover the screen, keeping the picture's shape (cropped at the sides or top and bottom)
			Vector2 picture = _background.GetSize();
			float scale = Mathf.Max(Size.X / picture.X, Size.Y / picture.Y);
			Vector2 drawn = picture * scale;
			DrawTextureRect(_background, new Rect2((Size - drawn) / 2, drawn), false);
			DrawRect(new Rect2(Vector2.Zero, Size), BackgroundDim);
		}
		else
		{
			float horizon = Size.Y * 0.3f;
			DrawPolygon(new[] { Vector2.Zero, new Vector2(Size.X, 0), new Vector2(Size.X, horizon), new Vector2(0, horizon) },
				new[] { SkyTop, SkyTop, SkyBottom, SkyBottom });
			DrawPolygon(new[] { new Vector2(0, horizon), new Vector2(Size.X, horizon), Size, new Vector2(0, Size.Y) },
				new[] { GroundTop, GroundTop, GroundBottom, GroundBottom });
		}

		foreach (BattleSide side in new[] { BattleSide.Player, BattleSide.Enemy })
		{
			var slots = _sim.SlotsOf(side);
			if (_phase == Phase.Positioning && side == BattleSide.Player)
			{
				// Guard links: each back slot is covered by its two closest front slots
				foreach (int back in BattleFormation.BackSlots)
					foreach (int guard in BattleFormation.GuardsOf(back))
						DrawDashedLine(SlotPosition(side, back), SlotPosition(side, guard), Muted with { A = 0.18f }, 2f, 8f);
			}
			for (int slot = 0; slot < BattleFormation.SlotCount; slot++)
			{
				if (slots[slot] != null)
					continue;
				var feet = SlotPosition(side, slot);
				bool open = _phase == Phase.Positioning && side == BattleSide.Player;
				DrawSetTransform(feet, 0, new Vector2(1, 0.32f));
				DrawArc(Vector2.Zero, 44, 0, Mathf.Tau, 40, open ? (_selected != null ? Gold : Muted) : Muted with { A = 0.12f }, open ? 2.5f : 1.5f, true);
				DrawSetTransform(Vector2.Zero, 0, Vector2.One);
				if (open)
					DrawString(ThemeDB.FallbackFont, feet + new Vector2(-50, 5), BattleFormation.IsBack(slot) ? "Back" : "Front",
						HorizontalAlignment.Center, 100, 13, Muted);
			}
		}

		if (_phase == Phase.Planning || _phase == Phase.Fighting)
			DrawTargetLines();
	}

	private void DrawTargetLines()
	{
		foreach (var combatant in _sim.Players)
		{
			if (!combatant.IsAlive || !_views.TryGetValue(combatant, out var from))
				continue;
			bool selected = combatant == _selected;
			Combatant target;
			Color color;
			if (combatant.QueuedAbility != null && combatant.QueuedAbility.NeedsTarget)
			{
				target = combatant.QueuedAbilityTarget;
				color = combatant.QueuedAbility.TargetsAlly ? HealColor : SpellColor;
			}
			else
			{
				target = _sim.PlannedTarget(combatant);
				color = combatant.OrderedTarget != null && combatant.OrderedTarget == target ? Gold : new Color(1, 1, 1);
			}
			if (target == null || !target.IsAlive || !_views.TryGetValue(target, out var to))
				continue;
			color.A = selected ? 0.9f : 0.28f;
			Vector2 a = from.Chest, b = to.Chest;
			DrawDashedLine(a, b, color, selected ? 3f : 2f, 10f);
			Vector2 dir = (b - a).Normalized();
			Vector2 side = new Vector2(-dir.Y, dir.X);
			DrawColoredPolygon(new[] { b - dir * 6, b - dir * 20 + side * 8, b - dir * 20 - side * 8 }, color);
		}
	}

	// --- Positioning ---------------------------------------------------------

	private void RefreshBench()
	{
		UIText.ClearChildren(_bench);
		var waiting = _playerUnits.Where(c => c.Slot < 0).ToList();
		foreach (var combatant in waiting)
		{
			var button = new Button
			{
				Text = combatant.Name,
				Icon = combatant.Icon,
				ExpandIcon = true,
				CustomMinimumSize = new Vector2(120, 44),
				ToggleMode = true,
				ButtonPressed = combatant == _selected,
				FocusMode = FocusModeEnum.None,
				TooltipText = $"HP {combatant.Health}/{combatant.MaxHealth}   ATK {combatant.Attack}   DEF {combatant.Defence}\nClick, then click a position",
			};
			button.AddThemeConstantOverride("icon_max_width", 36);
			// Deferred: the refresh rebuilds this row, button included
			button.Pressed += () => Callable.From(() => Select(_selected == combatant ? null : combatant)).CallDeferred();
			_bench.AddChild(button);
		}
		if (waiting.Count == 0)
			_bench.AddChild(new Label { Text = "All your units here are placed", ThemeTypeVariation = "MutedLabel" });
	}

	private void Select(Combatant combatant)
	{
		_selected = combatant;
		_armedAbility = null;
		RefreshAll();
	}

	// Puts the selected unit into `slot`; a unit already there swaps with it
	private void PlaceSelected(int slot)
	{
		var moving = _selected;
		var slots = _sim.PlayerSlots;
		var occupant = slots[slot];
		int from = moving.Slot;
		if (from >= 0)
			slots[from] = null;
		if (occupant != null)
		{
			occupant.Slot = from;
			if (from >= 0)
				slots[from] = occupant;
			else
				RemoveView(occupant);
		}
		moving.Slot = slot;
		slots[slot] = moving;
		if (!_views.ContainsKey(moving))
			AddView(moving);
		_selected = null;
		Layout();
		RefreshAll();
	}

	private void Bench(Combatant combatant)
	{
		if (combatant.Slot < 0)
			return;
		_sim.PlayerSlots[combatant.Slot] = null;
		combatant.Slot = -1;
		RemoveView(combatant);
		_selected = null;
		RefreshAll();
	}

	private void RemoveView(Combatant combatant)
	{
		if (_views.Remove(combatant, out var view))
			view.QueueFree();
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton { Pressed: true } mouse)
			return;
		if (mouse.ButtonIndex == MouseButton.Right)
		{
			Select(null);
			return;
		}
		if (mouse.ButtonIndex != MouseButton.Left)
			return;
		if (_phase == Phase.Positioning && _selected != null)
		{
			// Click on an empty slot
			for (int slot = 0; slot < BattleFormation.SlotCount; slot++)
			{
				if (mouse.Position.DistanceTo(SlotPosition(BattleSide.Player, slot) + new Vector2(0, -35)) < 55)
				{
					PlaceSelected(slot);
					return;
				}
			}
		}
		Select(null);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Visible)
			return;
		if (@event.IsActionPressed("ui_cancel"))
		{
			Select(null);
			GetViewport().SetInputAsHandled();
		}
		else if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space } && _phase == Phase.Fighting)
		{
			SetPaused(!_paused);
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnFighterClicked(BattleFighterView view, MouseButton button)
	{
		var clicked = view.Combatant;
		if (_phase == Phase.Positioning)
		{
			if (!clicked.IsPlayer)
				return;
			if (button == MouseButton.Right)
				Bench(clicked);
			else if (_selected == null || _selected == clicked)
				Select(_selected == clicked ? null : clicked);
			else
				PlaceSelected(clicked.Slot);
			return;
		}
		if (_phase != Phase.Planning && _phase != Phase.Fighting)
			return;
		if (button == MouseButton.Right)
		{
			Select(null);
			return;
		}
		// Clicking your own unit selects it, unless a heal is waiting for its target
		bool pickingAlly = _armedAbility != null && _armedAbility.TargetsAlly && _selected != null && _selected.IsAlive;
		if (clicked.IsPlayer && !pickingAlly)
		{
			Select(_selected == clicked ? null : clicked);
			return;
		}
		if (_selected == null || !_selected.IsAlive)
		{
			_hint.Text = "Click one of your units first, then the enemy it should attack";
			return;
		}
		OrderAgainst(clicked);
	}

	// --- Orders --------------------------------------------------------------

	private void OrderAgainst(Combatant enemy)
	{
		var unit = _selected;
		if (_armedAbility != null)
		{
			if (!_sim.ValidTargets(unit, _armedAbility).Contains(enemy))
				return;
			unit.QueuedAbility = _armedAbility;
			unit.QueuedAbilityTarget = enemy;
			Log($"{unit.Name} will cast {_armedAbility.Name} {(_armedAbility.TargetsAlly ? "on" : "at")} {(enemy == unit ? "itself" : enemy.Name)} on its turn");
			_armedAbility = null;
		}
		else if (_sim.ReachableTargets(unit).Contains(enemy))
		{
			unit.OrderedTarget = enemy;
			Log($"{unit.Name} now targets {enemy.Name}");
		}
		else
		{
			_hint.Text = $"{enemy.Name} is guarded: kill the front units next to it first (ranged units and spells can reach it)";
			return;
		}
		RefreshAll();
	}

	private void OnAbilityPressed(CombatAbility ability)
	{
		var unit = _selected;
		if (unit == null)
			return;
		if (unit.QueuedAbility == ability)
		{
			unit.QueuedAbility = null; // cancel
			unit.QueuedAbilityTarget = null;
			_armedAbility = null;
		}
		else if (ability.NeedsTarget)
		{
			_armedAbility = _armedAbility == ability ? null : ability;
		}
		else
		{
			unit.QueuedAbility = ability;
			_armedAbility = null;
			Log($"{unit.Name} will use {ability.Name} on its turn");
		}
		RefreshAll();
	}

	private void RefreshOrders()
	{
		UIText.ClearChildren(_abilityRow);
		var unit = _selected;
		if (unit == null || !unit.IsAlive || !unit.IsPlayer)
		{
			_orderLabel.Text = "";
			return;
		}
		var target = _sim.PlannedTarget(unit);
		string targetText = target == null ? "nobody in reach"
			: target.Name + (unit.OrderedTarget == target ? " (your order)" : " (closest)");
		_orderLabel.Text = $"{unit.Name}   HP {unit.Health}/{unit.MaxHealth}   ATK {_sim.EffectiveAttack(unit)}   DEF {unit.Defence}"
			+ (unit.MaxMana > 0 ? $"   MP {unit.Mana}/{unit.MaxMana}" : "") + $"   -   attacks {targetText}";

		foreach (var ability in unit.Abilities)
		{
			string why = _sim.WhyCantUse(unit, ability);
			bool queued = unit.QueuedAbility == ability;
			var button = new Button
			{
				Text = ability.Name + (ability.ManaCost > 0 ? $" ({ability.ManaCost} MP)" : "") + (queued ? " - queued" : ""),
				Icon = ability.Icon,
				ToggleMode = true,
				ButtonPressed = queued || _armedAbility == ability,
				Disabled = why != "" && !queued,
				FocusMode = FocusModeEnum.None,
				CustomMinimumSize = new Vector2(0, 34),
				TooltipText = ability.Description + (why != "" ? $"\n{why}" : queued ? "\nClick to cancel" : ""),
			};
			button.Pressed += () => Callable.From(() => OnAbilityPressed(ability)).CallDeferred();
			_abilityRow.AddChild(button);
		}
		foreach (var passive in unit.Passives)
			_abilityRow.AddChild(new Label { Text = passive.Name, ThemeTypeVariation = "MutedLabel", TooltipText = passive.Description, MouseFilter = MouseFilterEnum.Pass });
	}

	// --- Refresh -------------------------------------------------------------

	private void RefreshAll()
	{
		if (_sim == null)
			return;
		bool positioning = _phase == Phase.Positioning;
		bool fighting = _phase == Phase.Planning || _phase == Phase.Fighting;
		_startButton.Visible = _cancelButton.Visible = positioning;
		_startButton.Disabled = !_sim.Players.Any();
		_pauseButton.Visible = _speedButton.Visible = fighting;
		_benchBox.Visible = positioning;
		_orderBox.Visible = fighting;

		_subtitle.Text = positioning ? "Place your units: 3 front positions guard the 2 back positions"
			: $"Round {Math.Max(1, _sim.Round)}" + (_allEnemies.Count > BattleFormation.SlotCount ? $"   -   wave {_sim.Wave}" : "");
		_hint.Text = HintText();

		var reachable = new HashSet<Combatant>();
		if (fighting && _selected != null && _selected.IsAlive)
			reachable.UnionWith(_armedAbility != null ? _sim.ValidTargets(_selected, _armedAbility) : _sim.ReachableTargets(_selected));
		foreach (var (combatant, view) in _views)
		{
			view.Selected = combatant == _selected;
			view.Targetable = reachable.Contains(combatant);
			view.Active = combatant == _activeActor;
			view.Status = combatant.IsPlayer && combatant.QueuedAbility != null ? $"{combatant.QueuedAbility.Name} queued" : "";
			view.Refresh();
		}
		if (positioning)
			RefreshBench();
		else
			RefreshOrders();
		RefreshTurnStrip();
		QueueRedraw();
	}

	private string HintText()
	{
		switch (_phase)
		{
			case Phase.Positioning:
				int sittingOut = _playerUnits.Count(c => c.Slot < 0);
				string bench = sittingOut > 0 ? $"   {sittingOut} unit(s) will sit out." : "";
				return (_selected == null ? "Click a unit, then a position to move it there (right-click a unit to take it out)."
					: $"Click a position for {_selected.Name}.") + bench;
			case Phase.Planning:
			case Phase.Fighting:
				if (_armedAbility != null)
					return $"Pick a target for {_armedAbility.Name}";
				return _selected == null ? "Click one of your units to give it orders" : $"Click an enemy for {_selected.Name} to attack, or pick an ability";
			default:
				return "";
		}
	}

	private void RefreshTurnStrip()
	{
		UIText.ClearChildren(_turnStrip);
		if (_phase != Phase.Fighting && _phase != Phase.Planning)
			return;
		var order = new List<Combatant>();
		if (_activeActor != null && _activeActor.IsAlive)
			order.Add(_activeActor);
		order.AddRange(_sim.UpcomingTurns);
		foreach (var combatant in order.Take(10))
		{
			bool current = combatant == _activeActor;
			var frame = new PanelContainer { TooltipText = combatant.Name, MouseFilter = MouseFilterEnum.Pass };
			var style = new StyleBoxFlat
			{
				BgColor = new Color(0, 0, 0, 0.4f),
				BorderColor = current ? Gold : combatant.IsPlayer ? new Color(0.45f, 0.7f, 1f) : new Color(1f, 0.45f, 0.4f),
			};
			style.SetBorderWidthAll(current ? 3 : 2);
			style.SetCornerRadiusAll(4);
			frame.AddThemeStyleboxOverride("panel", style);
			float size = current ? 40 : 30;
			frame.AddChild(new TextureRect
			{
				Texture = combatant.Icon,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				CustomMinimumSize = new Vector2(size, size),
				MouseFilter = MouseFilterEnum.Ignore,
			});
			_turnStrip.AddChild(frame);
		}
	}

	private void Log(string line)
	{
		_logLines.Add(line);
		while (_logLines.Count > 6)
			_logLines.RemoveAt(0);
		_log.Text = string.Join("\n", _logLines);
	}

	// --- Battle loop ---------------------------------------------------------

	private void StartBattle()
	{
		if (!_sim.Players.Any())
			return;
		// Units that sit out keep their place on the field and take no part
		_selected = null;
		RunBattle(_battleId);
	}

	private async void RunBattle(int battleId)
	{
		await PlanningCountdown(battleId);
		while (battleId == _battleId)
		{
			var actor = _sim.NextActor();
			if (actor == null)
			{
				if (_sim.SideDefeated(BattleSide.Enemy) && _sim.HasMoreWaves)
				{
					await NextWave(battleId);
					continue;
				}
				break;
			}
			_activeActor = actor;
			// A new round can change health (Lord)
			foreach (var (combatant, view) in _views)
				view.ShownHealth = combatant.Health;
			RefreshAll();
			await Wait(0.25, battleId);
			if (battleId != _battleId)
				return;

			var events = _sim.TakeTurn(actor);
			foreach (var battleEvent in events)
			{
				Log(battleEvent.Message);
				await Animate(battleEvent, battleId);
				if (battleId != _battleId)
					return;
			}
			_activeActor = null;
			foreach (var (combatant, view) in _views)
				view.ShownHealth = combatant.Health;
			if (_selected != null && !_selected.IsAlive)
				_selected = null;
			RefreshAll();
			await Wait(TurnPause, battleId);
		}
		if (battleId == _battleId)
			ShowResult();
	}

	private async Task PlanningCountdown(int battleId)
	{
		_phase = Phase.Planning;
		RefreshAll();
		for (int second = (int)PlanningSeconds; second > 0 && battleId == _battleId; second--)
		{
			_bigLabel.Text = $"Give your orders: {second}";
			await Wait(1, battleId, ignoreSpeed: true);
		}
		_bigLabel.Text = "";
		_phase = Phase.Fighting;
		RefreshAll();
	}

	private async Task NextWave(int battleId)
	{
		foreach (var enemy in _sim.Enemies.ToList())
			RemoveView(enemy);
		_sim.StartNextWave();
		foreach (var enemy in _sim.Enemies)
			AddView(enemy);
		// Fallen player units are left where they fell; orders against the old wave are void
		foreach (var player in _sim.Players)
		{
			player.OrderedTarget = null;
			player.QueuedAbilityTarget = null;
		}
		Log($"Wave {_sim.Wave} arrives!");
		Layout();
		await PlanningCountdown(battleId);
	}

	private async Task Wait(double seconds, int battleId, bool ignoreSpeed = false)
	{
		double left = seconds;
		while (left > 0 || _paused)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (battleId != _battleId)
				return;
			if (!_paused)
				left -= GetProcessDeltaTime() * (ignoreSpeed ? 1 : Pace);
		}
	}

	private void SetPaused(bool paused)
	{
		_paused = paused;
		_pauseButton.Text = paused ? "Resume" : "Pause";
		_bigLabel.Text = paused ? "Paused" : "";
	}

	private void ToggleSpeed()
	{
		_speed = _speed >= 3 ? 1 : _speed + 1;
		_speedButton.Text = $"Speed {_speed}x";
		foreach (var view in _views.Values)
			view.SetAnimationSpeed(Pace);
	}

	// --- Animations ----------------------------------------------------------

	private async Task Animate(BattleEvent battleEvent, int battleId)
	{
		if (!_views.TryGetValue(battleEvent.Actor, out var actorView))
			return;
		switch (battleEvent)
		{
			case HitEvent hit:
				await AnimateHit(hit, actorView, battleId);
				break;
			case AreaEvent area:
				await AnimateArea(area, actorView, battleId);
				break;
			case BuffEvent buff:
				bool curse = buff.Ability.IsCurse;
				bool regrowth = buff.Ability.Kind == CombatAbilityKind.Rejuvenation;
				bool taunt = buff.Ability.Kind == CombatAbilityKind.Taunt;
				bool fortify = buff.Ability.Kind == CombatAbilityKind.Fortify;
				Color buffColor = curse ? CurseColor : regrowth ? HealColor : taunt ? TauntColor : Gold;
				string effect = curse ? $"-{buff.Ability.Amount} ATK" : regrowth ? $"+{buff.Ability.Amount} HP/turn" : taunt ? "Taunted!" : fortify ? $"+{buff.Ability.Amount} DEF" : $"+{buff.Ability.Amount} ATK";
				Popup(actorView, buff.Ability.Name.ToUpper() + "!", buffColor, 22);
				actorView.PlayAttack();
				await actorView.Pulse(Pace);
				foreach (var affected in buff.Affected)
					if (_views.TryGetValue(affected, out var affectedView))
					{
						if (curse || regrowth || taunt)
							affectedView.Flash(buffColor);
						Popup(affectedView, effect, buffColor, 16, 0.25f);
						affectedView.Refresh();
					}
				await Wait(0.6, battleId);
				actorView.PlayIdle();
				break;
			case HealEvent { Ability: null } tick:
				// Rejuvenation at the end of the healed unit's turn
				actorView.ShownHealth = tick.Actor.Health;
				Popup(actorView, $"+{tick.Healed[0].Amount} HP", HealColor, 18);
				actorView.Refresh();
				await Wait(0.4, battleId);
				break;
			case RootEvent root:
				Popup(actorView, root.Ability.Name.ToUpper() + "!", HealColor, 22);
				actorView.PlayAttack();
				if (_views.TryGetValue(root.Target, out var rootedView))
				{
					await Projectile(actorView.Chest, rootedView.Chest, RootColor);
					rootedView.Flash(RootColor);
					Popup(rootedView, "Rooted!", RootColor, 22);
					rootedView.Refresh();
				}
				await Wait(0.6, battleId);
				actorView.PlayIdle();
				break;
			case RootedTurnEvent rooted:
				actorView.ShownHealth = Math.Max(0, actorView.ShownHealth - rooted.Damage);
				actorView.Flash(RootColor);
				Popup(actorView, rooted.Damage > 0 ? $"Rooted  -{rooted.Damage}" : "Rooted", RootColor, 20);
				if (rooted.Killed)
					actorView.SetDead();
				actorView.Refresh();
				await Wait(0.7, battleId);
				break;
			case HealEvent heal:
				Popup(actorView, heal.Ability.Name.ToUpper() + "!", HealColor, 22);
				actorView.PlayAttack();
				await actorView.Pulse(Pace);
				foreach (var (patient, amount) in heal.Healed)
					if (_views.TryGetValue(patient, out var patientView))
					{
						patientView.Flash(HealColor);
						patientView.ShownHealth = patient.Health;
						Popup(patientView, $"+{amount} HP", HealColor, 20, 0.15f);
						patientView.Refresh();
					}
				await Wait(0.6, battleId);
				actorView.PlayIdle();
				break;
			case SkipEvent:
				Popup(actorView, "Stunned", new Color(0.75f, 0.55f, 1f), 18);
				await Wait(0.7, battleId);
				break;
		}
	}

	private async Task AnimateHit(HitEvent hit, BattleFighterView attacker, int battleId)
	{
		if (!_views.TryGetValue(hit.Target, out var target))
			return;
		_fighters.MoveChild(attacker, -1); // draw the attacker on top while it acts
		bool melee = hit.Ability == null && !hit.Actor.Ranged;
		Vector2 home = attacker.Position;
		if (hit.Ability?.Kind == CombatAbilityKind.AimedShot)
			Popup(attacker, hit.Ability.Name.ToUpper() + "!", Gold, 22);
		if (melee)
		{
			Vector2 inFront = target.Position + new Vector2(hit.Actor.IsPlayer ? -70 : 70, 6);
			await attacker.MoveTo(inFront, 0.3, Pace);
		}

		float length = attacker.PlayAttack();
		if (melee)
		{
			// The blow lands about halfway through the swing
			await Wait(length > 0 ? length * 0.5f : 0.12, battleId);
			if (length <= 0)
				await attacker.MoveTo(attacker.Position + new Vector2(hit.Actor.IsPlayer ? 18 : -18, 0), 0.08, Pace);
		}
		else
		{
			await Wait(length * 0.5f, battleId);
			await Projectile(attacker.Chest, target.Chest, ProjectileColor(hit));
		}
		if (battleId != _battleId)
			return;

		ShowImpact(hit, target);
		await Wait(Math.Max(0.35, length * 0.5f), battleId);
		attacker.PlayIdle();
		if (melee)
			await attacker.MoveTo(home, 0.3, Pace);
		SortFighters();
	}

	private static Color ProjectileColor(HitEvent hit) => hit.Ability?.Kind switch
	{
		null or CombatAbilityKind.AimedShot => hit.Actor.ProjectileColor,
		CombatAbilityKind.Fireball => FireColor,
		CombatAbilityKind.ManaSteal => ManaColor,
		_ => SpellColor,
	};

	// Fireball, Volley: one cast, a projectile to every target at the same time.
	// Whirling Blades: one spin in place, every reachable target is hit at once
	private async Task AnimateArea(AreaEvent area, BattleFighterView attacker, int battleId)
	{
		_fighters.MoveChild(attacker, -1);
		bool spin = area.Ability.Kind == CombatAbilityKind.WhirlingBlades;
		Color color = area.Ability.Kind == CombatAbilityKind.Fireball ? FireColor : Gold;
		Popup(attacker, area.Ability.Name.ToUpper() + "!", color, 22);
		float length = attacker.PlayAttack();
		await Wait(length * 0.5f, battleId);
		if (spin)
		{
			await attacker.Pulse(Pace);
		}
		else
		{
			var flights = new List<Task>();
			foreach (var hit in area.Hits)
				if (_views.TryGetValue(hit.Target, out var target))
					flights.Add(Projectile(attacker.Chest, target.Chest, area.Ability.Kind == CombatAbilityKind.Volley ? area.Actor.ProjectileColor : FireColor));
			await Task.WhenAll(flights);
		}
		if (battleId != _battleId)
			return;
		foreach (var hit in area.Hits)
			if (_views.TryGetValue(hit.Target, out var target))
				ShowImpact(hit, target);
		await Wait(Math.Max(0.35, length * 0.5f), battleId);
		attacker.PlayIdle();
		SortFighters();
	}

	private void ShowImpact(HitEvent hit, BattleFighterView target)
	{
		target.ShownHealth = hit.Target.Health;
		if (hit.ManaStolen > 0)
		{
			Popup(target, $"-{hit.ManaStolen} MP", ManaColor, 22);
			if (_views.TryGetValue(hit.Actor, out var casterView))
			{
				Popup(casterView, $"+{hit.ManaStolen} MP", ManaColor, 18, 0.2f);
				casterView.Refresh();
			}
		}
		else if (hit.Damage == 0)
		{
			Popup(target, hit.ShieldBlocked ? "Shield block!" : hit.Dodged ? "Dodged!" : "Blocked", new Color(0.8f, 0.8f, 0.8f), hit.ShieldBlocked || hit.Dodged ? 22 : 18);
		}
		else
		{
			if (!hit.Killed)
				target.Flash(new Color(1f, 0.35f, 0.35f));
			if (hit.Critical)
				Popup(target, $"{hit.Damage}!  CRITICAL", new Color(1f, 0.85f, 0.2f), 26);
			else
				Popup(target, hit.Damage.ToString(), new Color(1f, 0.3f, 0.25f), 24);
		}
		if (hit.Stunned)
			Popup(target, "Stunned!", new Color(0.75f, 0.55f, 1f), 18, 0.3f);
		if (hit.Killed)
			target.SetDead();
		target.Refresh();
	}

	private void Popup(BattleFighterView over, string text, Color color, int size, float delay = 0)
	{
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
		label.LabelSettings = new LabelSettings { FontSize = size, FontColor = color, OutlineSize = 5, OutlineColor = Colors.Black };
		label.Size = new Vector2(220, 40);
		label.Position = over.Position + new Vector2(BattleFighterView.ViewSize.X / 2 - 110, 30);
		label.Modulate = delay > 0 ? Colors.Transparent : Colors.White;
		AddChild(label);
		var tween = label.CreateTween();
		tween.SetSpeedScale(Pace);
		if (delay > 0)
			tween.TweenInterval(delay);
		tween.TweenProperty(label, "modulate", Colors.White, 0.01);
		tween.TweenProperty(label, "position:y", label.Position.Y - 46, 0.9).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		tween.Parallel().TweenProperty(label, "modulate:a", 0f, 0.9).SetDelay(0.4);
		tween.TweenCallback(Callable.From(label.QueueFree));
	}

	private async Task Projectile(Vector2 from, Vector2 to, Color color)
	{
		var bolt = new ColorRect { Color = color, Size = new Vector2(16, 16), PivotOffset = new Vector2(8, 8), MouseFilter = MouseFilterEnum.Ignore };
		bolt.Position = from - bolt.PivotOffset;
		bolt.Rotation = Mathf.Pi / 4;
		AddChild(bolt);
		var tween = bolt.CreateTween();
		tween.SetSpeedScale(Pace);
		tween.TweenProperty(bolt, "position", to - bolt.PivotOffset, 0.35).SetTrans(Tween.TransitionType.Sine);
		tween.Parallel().TweenProperty(bolt, "rotation", bolt.Rotation + Mathf.Tau, 0.35);
		await ToSignal(tween, Tween.SignalName.Finished);
		bolt.QueueFree();
	}

	// --- Result --------------------------------------------------------------

	private bool Won => _sim.SideDefeated(BattleSide.Enemy) && !_sim.HasMoreWaves;

	private List<Combatant> Fought => _sim.Players.ToList();

	private void ShowResult()
	{
		_phase = Phase.Finished;
		_selected = null;
		_activeActor = null;
		RefreshAll();
		var fought = Fought;
		var killed = _allEnemies.Where(c => !c.IsAlive).ToList();
		int experience = _experience = Won ? BattleSimulation.ExperienceFor(killed, fought.Count(c => c.IsAlive)) : 0;

		_resultTitle.Text = Won ? "Victory!" : "Defeat";
		var lines = new List<string> { $"Enemies slain: {killed.Count} of {_allEnemies.Count}" };
		foreach (var combatant in fought)
			lines.Add(combatant.IsAlive
				? $"{combatant.Name}: {combatant.Health}/{combatant.MaxHealth} HP" + (experience > 0 ? $", +{experience} XP" : "")
				: $"{combatant.Name} has fallen");
		_resultText.Text = string.Join("\n", lines);
		_resultPanel.Visible = true;
		Layout();
		CallDeferred(MethodName.Layout); // the panel's size is known after the label updates
	}

	// Writes the outcome back to the units and the field, then closes
	private void FinishAndClose()
	{
		var killed = _allEnemies.Where(c => !c.IsAlive).ToList();
		foreach (var combatant in Fought)
		{
			var playerUnit = combatant.PlayerUnit;
			if (!IsInstanceValid(playerUnit))
				continue;
			if (!combatant.IsAlive)
			{
				playerUnit.Die();
				continue;
			}
			playerUnit.ApplyBattleOutcome(combatant.Health, combatant.Mana);
			if (_experience > 0)
				playerUnit.AddExperience(_experience);
		}
		foreach (var enemy in _allEnemies)
		{
			var stats = enemy.Neutral?.GetEffectiveStats();
			if (stats != null)
				stats.health = enemy.Health;
		}
		_field.RemoveNeutralUnits(killed.Select(c => c.Neutral));
		_field.NotifyContentsChanged();
		Close();
	}
}
