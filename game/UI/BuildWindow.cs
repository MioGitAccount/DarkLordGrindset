using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Centred window showing a castle as a painted scene: the tower in the middle, two faction layers
// on each side, each building drawn on its spot (CastleLayout). A layer is its faction base in
// front with up to 3 slots behind it: built, empty (a plot: click to pick a building for it) or
// locked (rubble: needs a higher base level). Click a building to see its details in the column on
// the right, then Build / Upgrade / Recruit / Demolish. Under the header, a bar for the building
// going up and one for the unit in training, each with Cancel. Built in code, like SkillTreeWindow.
public partial class BuildWindow : PanelContainer
{
	private const float DetailsWidth = 330;

	private static readonly Color BrightGold = new Color(1f, 0.82f, 0.4f);
	private static readonly Color LockedGrey = new Color(0.35f, 0.33f, 0.33f);

	private enum Pick { None, Tower, Base, Slot }

	private Label _title;
	private Label _progressLabel;
	private Control _scene;
	private CastleLayout _layout;
	private Texture2D _backgroundArt;
	// Shown until a building has its own picture
	private Texture2D _placeholderArt, _towerArt, _plotArt, _lockedArt, _scaffoldArt;
	private readonly Dictionary<string, CastleSpot> _spots = new();
	private bool _layoutMode;
	private Button _layoutButton;
	private Label _detailName;
	private Label _detailInfo;
	private Label _detailEffects;
	private Label _detailStatus;
	private HFlowContainer _picker;
	private Button _buildButton;
	private Button _recruitButton;
	private Button _demolishButton;
	// One row each for Castle.Construction and Castle.Training
	private (Control row, Label name, ProgressBar bar, Label time) _buildRow, _trainRow;

	private Castle _castle;
	private Pick _pick;
	private Faction _pickFaction;
	private int _pickSlot;
	// The building chosen in the picker for an empty slot
	private BuildingDefinition _candidate;
	// Demolish needs a second click
	private bool _confirmDemolish;
	// Called on every Refresh to update one spot each
	private readonly List<Action> _spotUpdates = new();
	// The picker's buttons, rebuilt only when another slot is picked (Refresh runs every tick)
	private readonly List<(Button button, BuildingDefinition option)> _pickerButtons = new();
	private string _pickerKey;

	public override void _Ready()
	{
		Visible = false;
		_placeholderArt = LoadArt("res://images/castle/placeholder_building.png");
		_towerArt = LoadArt("res://images/map_objects/dark_tower.png");
		_plotArt = LoadArt("res://images/castle/plot_empty.png");
		_lockedArt = LoadArt("res://images/castle/plot_locked.png");
		_scaffoldArt = LoadArt("res://images/castle/plot_scaffold.png");

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 10);
		AddChild(box);

		var header = new HBoxContainer();
		_title = new Label { ThemeTypeVariation = "HeaderLabel", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		header.AddChild(_title);
		if (OS.IsDebugBuild())
		{
			// Testing aid while gathering is slow; not shown in exported release builds
			var debugResources = new Button { Text = "+200 all (debug)", FocusMode = FocusModeEnum.None };
			debugResources.Pressed += () =>
			{
				foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
					PlayerResources.Add(type, 200);
			};
			header.AddChild(debugResources);
			_layoutButton = new Button { Text = "Edit layout", FocusMode = FocusModeEnum.None, TooltipText = "Drag the buildings and plots, mouse wheel to resize (or F9)" };
			_layoutButton.Pressed += ToggleLayoutMode;
			header.AddChild(_layoutButton);
		}
		var close = new Button { Text = "Close", FocusMode = FocusModeEnum.None };
		close.Pressed += Close;
		header.AddChild(close);
		box.AddChild(header);

		_progressLabel = new Label { ThemeTypeVariation = "MutedLabel" };
		box.AddChild(_progressLabel);
		_buildRow = MakeWorkRow(box, new Color(1f, 0.75f, 0.3f), () => _castle?.CancelConstruction(), "Stop building and get the whole cost back");
		_trainRow = MakeWorkRow(box, new Color(0.55f, 0.8f, 1f), () => _castle?.CancelTraining(), "Stop training and get the whole cost back");
		box.AddChild(new HSeparator());

		var main = new HBoxContainer();
		main.AddThemeConstantOverride("separation", 12);
		box.AddChild(main);
		_scene = new Control { MouseFilter = MouseFilterEnum.Pass, ClipContents = true, SizeFlagsVertical = SizeFlags.ShrinkBegin };
		main.AddChild(_scene);

		// Details column on the right
		var text = new VBoxContainer { CustomMinimumSize = new Vector2(DetailsWidth, 0) };
		text.AddThemeConstantOverride("separation", 8);
		main.AddChild(text);
		// Wrapping labels need a width, or they wrap after every word and the window grows very tall
		_detailName = new Label { ThemeTypeVariation = "HeaderLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(DetailsWidth, 0) };
		_detailInfo = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(DetailsWidth, 0) };
		_detailEffects = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(DetailsWidth, 0) };
		_detailEffects.AddThemeColorOverride("font_color", BrightGold);
		_detailStatus = new Label { ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(DetailsWidth, 0) };
		_picker = new HFlowContainer { CustomMinimumSize = new Vector2(DetailsWidth, 0) };
		_picker.AddThemeConstantOverride("h_separation", 6);
		_picker.AddThemeConstantOverride("v_separation", 6);
		text.AddChild(_detailName);
		text.AddChild(_picker);
		text.AddChild(_detailInfo);
		text.AddChild(_detailEffects);
		text.AddChild(_detailStatus);
		var buttons = new HFlowContainer();
		buttons.AddThemeConstantOverride("h_separation", 8);
		buttons.AddThemeConstantOverride("v_separation", 8);
		text.AddChild(buttons);
		_buildButton = new Button { Text = "Build", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(150, 36) };
		_buildButton.Pressed += BuildPressed;
		buttons.AddChild(_buildButton);
		_recruitButton = new Button { Text = "Recruit", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(150, 36) };
		_recruitButton.Pressed += RecruitPressed;
		buttons.AddChild(_recruitButton);
		_demolishButton = new Button { Text = "Demolish", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(150, 36) };
		_demolishButton.Pressed += DemolishPressed;
		buttons.AddChild(_demolishButton);
	}

	private static (Control, Label, ProgressBar, Label) MakeWorkRow(Container parent, Color color, Action cancel, string cancelTip)
	{
		var row = new HBoxContainer { Visible = false };
		row.AddThemeConstantOverride("separation", 10);
		var name = new Label { CustomMinimumSize = new Vector2(320, 0) };
		name.AddThemeColorOverride("font_color", color);
		var bar = new ProgressBar
		{
			ShowPercentage = false,
			MaxValue = 1,
			Step = 0,
			CustomMinimumSize = new Vector2(0, 12),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		var back = new StyleBoxFlat { BgColor = new Color(0.12f, 0.1f, 0.1f), BorderColor = color.Darkened(0.5f) };
		back.SetBorderWidthAll(1);
		back.SetCornerRadiusAll(3);
		var fill = new StyleBoxFlat { BgColor = color.Darkened(0.15f) };
		fill.SetCornerRadiusAll(3);
		bar.AddThemeStyleboxOverride("background", back);
		bar.AddThemeStyleboxOverride("fill", fill);
		var time = new Label { CustomMinimumSize = new Vector2(90, 0), HorizontalAlignment = HorizontalAlignment.Right };
		var cancelButton = new Button { Text = "Cancel", TooltipText = cancelTip, FocusMode = FocusModeEnum.None };
		cancelButton.Pressed += cancel;
		row.AddChild(name);
		row.AddChild(bar);
		row.AddChild(time);
		row.AddChild(cancelButton);
		parent.AddChild(row);
		return (row, name, bar, time);
	}

	private static void ShowWork((Control row, Label name, ProgressBar bar, Label time) ui, CastleWork work, string verb)
	{
		ui.row.Visible = work != null;
		if (work == null)
			return;
		ui.name.Text = $"{verb} {work.Name}";
		ui.bar.Value = work.Progress;
		ui.time.Text = $"{work.TimeLeft} left";
	}

	public override void _ExitTree()
	{
		Unsubscribe();
	}

	public void Open(Castle castle)
	{
		if (castle == null)
		{
			Close();
			return;
		}
		if (castle != _castle)
		{
			Unsubscribe();
			_pick = Pick.None;
			_candidate = null;
			_castle = castle;
			_castle.Changed += Refresh;
			_castle.Progressed += Refresh;
			PlayerResources.Changed += OnResourcesChanged;
			PlayerAgents.Changed += Refresh;
		}
		_title.Text = $"{_castle.Tree.Name} - buildings";
		BuildScene();
		Refresh();
		Visible = true;
		ResetSize();
		SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.KeepSize);
	}

	public void Close()
	{
		Visible = false;
		Unsubscribe();
		_castle = null;
		_pick = Pick.None;
		_candidate = null;
	}

	private void Unsubscribe()
	{
		if (_castle != null)
		{
			_castle.Changed -= Refresh;
			_castle.Progressed -= Refresh;
		}
		PlayerResources.Changed -= OnResourcesChanged;
		PlayerAgents.Changed -= Refresh;
	}

	// What the player can afford decides what the details show
	private void OnResourcesChanged(ResourceType type, float amount) => Refresh();

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Visible && @event.IsActionPressed("ui_cancel"))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
		else if (Visible && OS.IsDebugBuild() && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F9 })
		{
			ToggleLayoutMode();
			GetViewport().SetInputAsHandled();
		}
	}

	// --- Scene -----------------------------------------------------------------------------------

	private static Texture2D LoadArt(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

	// As large as fits next to the details column, keeping the background's shape
	private Vector2 SceneSize()
	{
		var view = GetViewportRect().Size;
		float aspect = _backgroundArt == null ? 16f / 9f : (float)_backgroundArt.GetWidth() / _backgroundArt.GetHeight();
		float width = Mathf.Min(view.X - DetailsWidth - 70, (view.Y - 250) * aspect);
		width = Mathf.Max(width, 480);
		return new Vector2(width, width / aspect);
	}

	private void BuildScene()
	{
		UIText.ClearChildren(_scene);
		_spots.Clear();
		_spotUpdates.Clear();
		_pickerKey = null;
		_layout = CastleLayout.Load();
		_backgroundArt = LoadArt(_layout.Background);
		var size = SceneSize();
		_scene.CustomMinimumSize = size;
		_scene.Size = size;
		_scene.AddChild(new TextureRect
		{
			Texture = _backgroundArt,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			Size = size,
			MouseFilter = MouseFilterEnum.Ignore,
		});

		AddSpot(CastleLayout.TowerKey, SelectTower, UpdateTowerSpot);
		foreach (var faction in Factions.All)
		{
			var layer = faction;
			AddSpot(CastleLayout.BaseKey(faction), () => SelectBase(layer), spot => UpdateBaseSpot(spot, layer));
			for (int slot = 0; slot < Castle.MaxSlots; slot++)
			{
				int index = slot;
				AddSpot(CastleLayout.SlotKey(faction, slot), () => SelectSlot(layer, index), spot => UpdateSlotSpot(spot, layer, index));
			}
			var label = new Label
			{
				Text = Factions.Name(faction),
				ThemeTypeVariation = "HeaderLabel",
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = MouseFilterEnum.Ignore,
				Size = new Vector2(200, 30),
				Position = new Vector2(_layout.Get(CastleLayout.BaseKey(faction)).X * size.X - 100, _layout.LabelY * size.Y),
			};
			label.AddThemeColorOverride("font_outline_color", Colors.Black);
			label.AddThemeConstantOverride("outline_size", 6);
			_scene.AddChild(label);
		}
		// Back spots first, so the ones in front draw (and take clicks) over them
		foreach (var spot in _spots.Values.OrderBy(s => _layout.Get(s.Key).Y))
			_scene.MoveChild(spot, -1);
		foreach (var label in _scene.GetChildren().OfType<Label>().ToList())
			_scene.MoveChild(label, -1);
	}

	private void AddSpot(string key, Action pressed, Action<CastleSpot> update)
	{
		var spot = new CastleSpot { Key = key };
		spot.Clicked += pressed;
		spot.Dragged += feet => MoveSpot(spot, feet);
		spot.Scaled += factor => ScaleSpot(spot, factor);
		_scene.AddChild(spot);
		_spots[key] = spot;
		_spotUpdates.Add(() => update(spot));
	}

	// Puts the spot's picture on its place from the layout
	private void Place(CastleSpot spot, Texture2D picture)
	{
		var at = _layout.Get(spot.Key);
		var size = _scene.Size;
		spot.Place(picture, new Vector2(at.X * size.X, at.Y * size.Y), at.Width * size.X);
	}

	private void ShowSpot(CastleSpot spot, Texture2D picture, string caption, string detail, bool always, bool selected, bool dim = false)
	{
		spot.Caption = caption;
		spot.Detail = detail;
		spot.AlwaysCaption = always;
		spot.Selected = selected;
		spot.Dim = dim;
		spot.Editing = _layoutMode;
		Place(spot, picture);
	}

	// e.g. "●●○"
	private static string Pips(int level) =>
		new string('●', level) + new string('○', Math.Max(0, BuildingDefinition.MaxLevel - level));

	private void UpdateTowerSpot(CastleSpot spot)
	{
		var tower = _castle.Towers.LastOrDefault(t => _castle.IsBuilt(t));
		var next = _castle.NextTower;
		var status = next == null ? BuildingStatus.MaxLevel : _castle.TowerStatus(next);
		string detail = status switch
		{
			BuildingStatus.UnderConstruction => $"Building {next.Name}: {_castle.Construction.TimeLeft}",
			BuildingStatus.Available => $"{next.Name} ready to build",
			_ => "",
		};
		ShowSpot(spot, tower?.Icon ?? _towerArt, $"{tower?.Name ?? "No tower"}  {Pips(_castle.TowerLevel)}", detail, true, _pick == Pick.Tower);
	}

	private void UpdateBaseSpot(CastleSpot spot, Faction faction)
	{
		var building = _castle.Base(faction);
		int level = _castle.BaseLevel(faction);
		var status = _castle.BaseStatus(faction);
		bool selected = _pick == Pick.Base && _pickFaction == faction;
		string name = building?.Name ?? $"{Factions.Name(faction)} base";
		if (level == 0)
		{
			bool building_ = status == BuildingStatus.UnderConstruction;
			ShowSpot(spot, building_ ? _scaffoldArt : _plotArt, name,
				building_ ? $"Building: {_castle.Construction.TimeLeft}" : "Not built",
				true, selected);
			return;
		}
		string detail = status switch
		{
			BuildingStatus.UnderConstruction => $"Level {level + 1}: {_castle.Construction.TimeLeft}",
			BuildingStatus.Available => $"Level {level + 1} ready to build",
			_ => "",
		};
		ShowSpot(spot, building?.IconAt(level) ?? _placeholderArt, $"{name}  {Pips(level)}", detail, true, selected);
	}

	private void UpdateSlotSpot(CastleSpot spot, Faction faction, int slot)
	{
		bool selected = _pick == Pick.Slot && _pickFaction == faction && _pickSlot == slot;
		var building = _castle.SlotAt(faction, slot);
		if (building != null)
			ShowSpot(spot, building.Icon ?? _placeholderArt, building.Name, ShortEffect(building), false, selected);
		else if (_castle.IsSlotUnderConstruction(faction, slot))
			ShowSpot(spot, _scaffoldArt, _castle.Construction.Building.Name, _castle.Construction.TimeLeft, true, selected);
		else if (slot < _castle.SlotCount(faction))
			ShowSpot(spot, _plotArt, "Empty slot", "Click to build", false, selected);
		else
			ShowSpot(spot, _lockedArt, "Locked", $"Needs base level {slot + 1}", false, selected, dim: true);
	}

	// One short line, e.g. "Recruit Imp", "Imp +2 lv", "Quests"
	private static string ShortEffect(BuildingDefinition building)
	{
		var parts = new List<string>();
		if (building.RecruitsUnit != null)
			parts.Add($"Recruit {building.RecruitsUnit.Name}");
		if (building.BonusRecruitLevel != 0)
			parts.Add($"{building.BonusLevelFor?.Name ?? "All"} +{building.BonusRecruitLevel} lv");
		if (building.GoldPerTick != 0)
			parts.Add($"+{building.GoldPerTick:0.##} gold");
		if (building.HealInterval > 0)
			parts.Add("Heals");
		if (building.QuestPool.Count > 0)
			parts.Add("Quests");
		if (building.AgentCap != 0)
			parts.Add($"+{building.AgentCap} agents");
		return string.Join(", ", parts);
	}

	// --- Layout mode (debug builds): the Edit layout button or F9; drag spots, wheel to resize,
	// the button (or F9) again to save --------------------------------------------------------------

	private void ToggleLayoutMode()
	{
		_layoutMode = !_layoutMode;
		if (!_layoutMode)
			_layout.Save();
		if (_layoutButton != null)
			_layoutButton.Text = _layoutMode ? "Save layout" : "Edit layout";
		Refresh();
	}

	private void MoveSpot(CastleSpot spot, Vector2 feet)
	{
		var at = _layout.Get(spot.Key);
		at.X = Mathf.Clamp(feet.X / _scene.Size.X, 0, 1);
		at.Y = Mathf.Clamp(feet.Y / _scene.Size.Y, 0, 1);
		_layout.Spots[spot.Key] = at;
		Place(spot, spot.Picture);
	}

	private void ScaleSpot(CastleSpot spot, float factor)
	{
		var at = _layout.Get(spot.Key);
		at.Width = Mathf.Clamp(at.Width * factor, 0.02f, 0.6f);
		_layout.Spots[spot.Key] = at;
		Place(spot, spot.Picture);
	}

	private enum CardLook { Built, Available, NoResources, Locked }

	private static CardLook LookOf(BuildingStatus status) => status switch
	{
		BuildingStatus.Built => CardLook.Built,
		BuildingStatus.Available => CardLook.Available,
		BuildingStatus.NoResources => CardLook.NoResources,
		_ => CardLook.Locked,
	};

	// --- Selection and details -------------------------------------------------------------------

	private void SelectTower() => Select(Pick.Tower, Faction.None, 0);
	private void SelectBase(Faction faction) => Select(Pick.Base, faction, 0);
	private void SelectSlot(Faction faction, int slot) => Select(Pick.Slot, faction, slot);

	private void Select(Pick pick, Faction faction, int slot)
	{
		bool same = _pick == pick && _pickFaction == faction && _pickSlot == slot;
		_pick = pick;
		_pickFaction = faction;
		_pickSlot = slot;
		if (!same)
		{
			_candidate = null;
			_confirmDemolish = false;
		}
		Refresh();
	}

	private void Refresh()
	{
		if (_castle == null)
			return;
		int slots = Factions.All.Sum(_castle.SlotCount);
		int used = Factions.All.Sum(f => Enumerable.Range(0, Castle.MaxSlots).Count(i => _castle.SlotAt(f, i) != null));
		ShowWork(_buildRow, _castle.Construction, "Building");
		ShowWork(_trainRow, _castle.Training, "Training");
		_progressLabel.Text = _layoutMode ? "Layout mode: drag the buildings and plots, mouse wheel to resize, then Save layout."
			: $"Tower level {_castle.TowerLevel}   Vision {_castle.VisionRadius:0}   Slots used {used}/{slots}   Gold {_castle.GoldPerTick:0.##}/tick";
		foreach (var update in _spotUpdates)
			update();
		ShowDetails();
	}

	private void ShowDetails()
	{
		_picker.Visible = false;
		_buildButton.Visible = false;
		_recruitButton.Visible = false;
		_demolishButton.Visible = false;
		_detailInfo.Text = "";
		_detailEffects.Text = "";
		_detailStatus.Text = "";
		switch (_pick)
		{
			case Pick.Tower:
				ShowTower();
				break;
			case Pick.Base:
				ShowBase(_pickFaction);
				break;
			case Pick.Slot:
				ShowSlot(_pickFaction, _pickSlot);
				break;
			default:
				_detailName.Text = "Pick a building";
				_detailInfo.Text = "Click the tower, a faction base or a slot. Raise a base to open more slots in its layer; "
					+ "each base level needs the tower at that level.";
				break;
		}
	}

	private void ShowBuildingText(BuildingDefinition building, string costText)
	{
		_detailInfo.Text = (costText != null ? costText + "\n" : "") + building.Description;
		_detailEffects.Text = string.Join("\n", building.EffectLines());
	}

	private void ShowTower()
	{
		var next = _castle.NextTower;
		if (next == null)
			return;
		var status = _castle.TowerStatus(next);
		bool maxed = status == BuildingStatus.MaxLevel || status == BuildingStatus.Built;
		_detailName.Text = maxed ? next.Name : $"Tower {_castle.TowerLevel} → {next.Name}";
		ShowBuildingText(next, maxed ? null : $"Costs {ResourceCost.Text(next.Cost)}  ·  takes {BuildTime(next, next.TowerLevel)}");
		_detailStatus.Text = maxed ? "The tower is at its highest level." : StatusText(status, next);
		_buildButton.Visible = !maxed && status != BuildingStatus.UnderConstruction;
		_buildButton.Text = "Upgrade";
		_buildButton.Disabled = status != BuildingStatus.Available;
	}

	private void ShowBase(Faction faction)
	{
		var building = _castle.Base(faction);
		if (building == null)
		{
			_detailName.Text = $"{Factions.Name(faction)}: no base";
			return;
		}
		int level = _castle.BaseLevel(faction);
		var status = _castle.BaseStatus(faction);
		_detailName.Text = $"{building.Name} ({Factions.Name(faction)})  ·  level {level}/{BuildingDefinition.MaxLevel}";
		ShowBuildingText(building, status == BuildingStatus.MaxLevel ? null
			: $"{(level == 0 ? "Build" : $"Level {level + 1}")} costs {ResourceCost.Text(_castle.NextBaseCost(faction))}  ·  takes {BuildTime(building, level + 1)}");
		if (building.UnitCapFor != null)
			_detailEffects.Text += $"\n{building.UnitCapFor.Name} cap now {_castle.UnitCap(building.UnitCapFor)} (from all buildings)";
		_detailStatus.Text = status switch
		{
			BuildingStatus.MaxLevel => "Highest level: 3 slots open.",
			BuildingStatus.Available => level == 0 ? "Ready to build." : $"Ready to raise to level {level + 1}: opens slot {level + 1}.",
			BuildingStatus.NoResources => "Not enough resources.",
			BuildingStatus.NeedsTower => $"Needs Tower level {level + 1}.",
			_ => StatusText(status, building, faction),
		};
		_buildButton.Visible = status != BuildingStatus.MaxLevel && status != BuildingStatus.UnderConstruction;
		_buildButton.Text = level == 0 ? "Build" : "Upgrade";
		_buildButton.Disabled = status != BuildingStatus.Available;
		ShowRecruit(building, level > 0);
	}

	private void ShowRecruit(BuildingDefinition building, bool built)
	{
		if (building.RecruitsUnit == null)
			return;
		_recruitButton.Visible = true;
		var type = building.RecruitsUnit;
		int level = _castle.RecruitLevel(type);
		_recruitButton.Text = $"Recruit (lv {level})";
		_recruitButton.Disabled = !_castle.CanRecruit(building);
		var training = _castle.Training;
		string blocked = training != null ? $"Already training {Article(training.Name)} ({training.TimeLeft} left); one unit at a time"
			: PlayerAgents.RecruitBlockedReason(type);
		_recruitButton.TooltipText = !built ? "Build it first"
			: blocked ?? $"Recruit a level {level} {type.Name} for {ResourceCost.Text(building.RecruitCost)}, ready in {CastleWork.Clock(building.RecruitSeconds)}"
				+ $" ({type.Name} {PlayerAgents.CountOf(type)}/{PlayerAgents.CapFor(type)}, agents {PlayerAgents.Count}/{PlayerAgents.Cap})";
		if (built && blocked != null)
			_detailStatus.Text += $"  {blocked}.";
	}

	private void ShowSlot(Faction faction, int slot)
	{
		var building = _castle.SlotAt(faction, slot);
		if (building != null)
		{
			_detailName.Text = $"{building.Name}  ·  {Factions.Name(faction)} slot {slot + 1}";
			ShowBuildingText(building, null);
			_demolishButton.Visible = true;
			_demolishButton.Text = _confirmDemolish ? "Click to confirm" : "Demolish";
			_demolishButton.TooltipText = $"Tear it down and get back {ResourceCost.Text(RefundOf(building))}";
			_detailStatus.Text = $"Demolishing pays back {Castle.DemolishRefund:P0} of its cost ({ResourceCost.Text(RefundOf(building))}).";
			ShowRecruit(building, true);
			return;
		}
		if (_castle.IsSlotUnderConstruction(faction, slot))
		{
			var going = _castle.Construction.Building;
			_detailName.Text = $"{going.Name}  ·  {Factions.Name(faction)} slot {slot + 1}";
			ShowBuildingText(going, null);
			_detailStatus.Text = StatusText(BuildingStatus.UnderConstruction, going, faction);
			return;
		}
		if (slot >= _castle.SlotCount(faction))
		{
			_detailName.Text = $"{Factions.Name(faction)} slot {slot + 1}: locked";
			_detailInfo.Text = $"Raise the {_castle.Base(faction)?.Name ?? "base"} to level {slot + 1} to open this slot.";
			return;
		}

		_detailName.Text = $"{Factions.Name(faction)} slot {slot + 1}: pick a building";
		_picker.Visible = true;
		string key = $"{faction}/{slot}";
		if (key != _pickerKey)
		{
			_pickerKey = key;
			UIText.ClearChildren(_picker);
			_pickerButtons.Clear();
			foreach (var option in _castle.SlotOptions(faction))
			{
				var button = new Button
				{
					Text = option.Faction == Faction.None ? $"{option.Name} (neutral)" : option.Name,
					FocusMode = FocusModeEnum.None,
					CustomMinimumSize = new Vector2(0, 30),
				};
				var picked = option;
				button.Pressed += () =>
				{
					_candidate = picked;
					Refresh();
				};
				_picker.AddChild(button);
				_pickerButtons.Add((button, option));
			}
		}
		foreach (var (button, option) in _pickerButtons)
		{
			var status = _castle.SlotStatus(option, faction);
			button.TooltipText = status == BuildingStatus.Built ? "Already built in another slot" : option.Description;
			StyleOption(button, status, option == _candidate);
		}
		if (_candidate == null)
		{
			_detailInfo.Text = "Faction buildings fit only their own layer; neutral ones fit any layer. Each building can stand only once.";
			return;
		}
		var candidateStatus = _castle.SlotStatus(_candidate, faction);
		ShowBuildingText(_candidate, $"Costs {ResourceCost.Text(_candidate.Cost)}  ·  takes {BuildTime(_candidate, _candidate.Level)}");
		_detailStatus.Text = StatusText(candidateStatus, _candidate, faction);
		_buildButton.Visible = true;
		_buildButton.Text = $"Build {_candidate.Name}";
		_buildButton.Disabled = candidateStatus != BuildingStatus.Available;
	}

	private void StyleOption(Button button, BuildingStatus status, bool selected)
	{
		var look = LookOf(status);
		Color bg = look switch
		{
			CardLook.Built => new Color(0.2f, 0.12f, 0.1f),
			CardLook.Available => new Color(0.2f, 0.16f, 0.12f),
			CardLook.NoResources => new Color(0.14f, 0.12f, 0.11f),
			_ => new Color(0.09f, 0.085f, 0.09f),
		};
		var style = new StyleBoxFlat { BgColor = bg, BorderColor = selected ? Colors.White : look == CardLook.Available ? BrightGold : LockedGrey };
		style.SetBorderWidthAll(selected ? 2 : 1);
		style.SetCornerRadiusAll(4);
		style.SetContentMarginAll(6);
		var hover = (StyleBoxFlat)style.Duplicate();
		hover.BgColor = bg.Lightened(0.08f);
		button.AddThemeStyleboxOverride("normal", style);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("pressed", hover);
		button.AddThemeColorOverride("font_color", look == CardLook.Locked ? new Color(0.55f, 0.52f, 0.5f) : new Color(1f, 0.92f, 0.75f));
	}

	private string StatusText(BuildingStatus status, BuildingDefinition building, Faction faction = Faction.None) => status switch
	{
		BuildingStatus.Built => _castle.SlotOf(building) is { } at
			? $"Already built in the {Factions.Name(at.faction)} layer (slot {at.slot + 1})."
			: "Built.",
		BuildingStatus.Available => "Ready to build.",
		BuildingStatus.NoResources => "Not enough resources.",
		BuildingStatus.NeedsTower => $"Needs Tower level {building.RequiredTowerLevel}.",
		BuildingStatus.NeedsBase => $"Needs the {_castle.Base(faction)?.Name ?? "base"} at level {building.MinBaseLevel}.",
		BuildingStatus.WrongLayer => $"Only fits the {Factions.Name(building.Faction)} layer.",
		BuildingStatus.UnderConstruction => $"Under construction: {_castle.Construction.TimeLeft} left.",
		BuildingStatus.Busy => $"{_castle.Construction.Name} is being built ({_castle.Construction.TimeLeft} left); one building at a time.",
		_ => "Build the required buildings first.",
	};

	private static string BuildTime(BuildingDefinition building, int level) => CastleWork.Clock(Castle.BuildSeconds(building, level));

	// "an Imp", "a Witch"
	private static string Article(string name) => ("AEIOU".Contains(char.ToUpper(name[0])) ? "an " : "a ") + name;

	private static IEnumerable<ResourceCost> RefundOf(BuildingDefinition building) =>
		building.Cost.Where(c => c != null).Select(c => new ResourceCost { Type = c.Type, Amount = Mathf.Floor(c.Amount * Castle.DemolishRefund) });

	// --- Actions ---------------------------------------------------------------------------------

	// Each raises Castle.Changed or PlayerResources.Changed, which refreshes the window
	private void BuildPressed()
	{
		if (_castle == null)
			return;
		switch (_pick)
		{
			case Pick.Tower:
				_castle.TryBuildTower(_castle.NextTower);
				break;
			case Pick.Base:
				_castle.TryUpgradeBase(_pickFaction);
				break;
			case Pick.Slot:
				if (_castle.TryBuildInSlot(_candidate, _pickFaction, _pickSlot))
					_candidate = null;
				break;
		}
	}

	private void RecruitPressed()
	{
		if (_castle == null)
			return;
		var building = _pick == Pick.Base ? _castle.Base(_pickFaction) : _pick == Pick.Slot ? _castle.SlotAt(_pickFaction, _pickSlot) : null;
		if (building != null)
			_castle.TryRecruit(building);
	}

	private void DemolishPressed()
	{
		if (_castle == null || _pick != Pick.Slot)
			return;
		if (!_confirmDemolish)
		{
			_confirmDemolish = true;
			Refresh();
			return;
		}
		_confirmDemolish = false;
		_castle.TryDemolish(_pickFaction, _pickSlot);
	}
}
