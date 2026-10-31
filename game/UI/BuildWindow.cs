using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Centred window showing a castle: the tower in the middle, two faction layers on each side.
// A layer is its faction base at the bottom (level pips) with up to 3 slots above it: built,
// empty (click to pick a building for it) or locked (needs a higher base level). Click a card to
// see its details below, then Build / Upgrade / Recruit / Demolish. Built in code, like SkillTreeWindow.
public partial class BuildWindow : PanelContainer
{
	private static readonly Vector2 CardSize = new Vector2(190, 60);
	private const float ColumnGap = 18;
	private const float RowGap = 10;
	private const float HeaderHeight = 26;

	private static readonly Color Gold = new Color(0.85f, 0.65f, 0.3f);
	private static readonly Color BrightGold = new Color(1f, 0.82f, 0.4f);
	private static readonly Color LockedGrey = new Color(0.35f, 0.33f, 0.33f);

	private enum CardLook { Built, Available, NoResources, Locked, Empty }

	private enum Pick { None, Tower, Base, Slot }

	private Label _title;
	private Label _progressLabel;
	private Control _canvas;
	private Label _detailName;
	private Label _detailInfo;
	private Label _detailEffects;
	private Label _detailStatus;
	private HFlowContainer _picker;
	private Button _buildButton;
	private Button _recruitButton;
	private Button _demolishButton;

	private Castle _castle;
	private Pick _pick;
	private Faction _pickFaction;
	private int _pickSlot;
	// The building chosen in the picker for an empty slot
	private BuildingDefinition _candidate;
	// Demolish needs a second click
	private bool _confirmDemolish;
	// Called on every Refresh to update one card each
	private readonly List<Action> _cardUpdates = new();
	// The picker's buttons, rebuilt only when another slot is picked (Refresh runs every tick)
	private readonly List<(Button button, BuildingDefinition option)> _pickerButtons = new();
	private string _pickerKey;

	public override void _Ready()
	{
		Visible = false;

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
		}
		var close = new Button { Text = "Close", FocusMode = FocusModeEnum.None };
		close.Pressed += Close;
		header.AddChild(close);
		box.AddChild(header);

		_progressLabel = new Label { ThemeTypeVariation = "MutedLabel" };
		box.AddChild(_progressLabel);
		box.AddChild(new HSeparator());

		_canvas = new Control { MouseFilter = MouseFilterEnum.Pass };
		_canvas.Draw += DrawLayers;
		box.AddChild(_canvas);

		box.AddChild(new HSeparator());
		var details = new HBoxContainer();
		details.AddThemeConstantOverride("separation", 12);
		box.AddChild(details);
		var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		details.AddChild(text);
		_detailName = new Label { ThemeTypeVariation = "HeaderLabel" };
		// Wrapping labels need a width, or they wrap after every word and the window grows very tall
		_detailInfo = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(440, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_detailEffects = new Label { CustomMinimumSize = new Vector2(320, 0) };
		_detailEffects.AddThemeColorOverride("font_color", BrightGold);
		_detailStatus = new Label { ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0) };
		_picker = new HFlowContainer { CustomMinimumSize = new Vector2(560, 0) };
		_picker.AddThemeConstantOverride("h_separation", 6);
		_picker.AddThemeConstantOverride("v_separation", 6);
		text.AddChild(_detailName);
		text.AddChild(_picker);
		var infoRow = new HBoxContainer();
		infoRow.AddThemeConstantOverride("separation", 16);
		infoRow.AddChild(_detailInfo);
		infoRow.AddChild(_detailEffects);
		text.AddChild(infoRow);
		text.AddChild(_detailStatus);
		var buttons = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
		details.AddChild(buttons);
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
			PlayerResources.Changed += OnResourcesChanged;
			PlayerAgents.Changed += Refresh;
		}
		_title.Text = $"{_castle.Tree.Name} - buildings";
		BuildCards();
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
			_castle.Changed -= Refresh;
		PlayerResources.Changed -= OnResourcesChanged;
		PlayerAgents.Changed -= Refresh;
	}

	// What the player can afford decides which cards light up
	private void OnResourcesChanged(ResourceType type, float amount) => Refresh();

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Visible && @event.IsActionPressed("ui_cancel"))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	// --- Layout ----------------------------------------------------------------------------------

	// Columns: Chaos, Stronghold, Tower, Dark Humans, Undead
	private static float ColumnX(int column) => column * (CardSize.X + ColumnGap);

	private static int ColumnOf(Faction faction)
	{
		int index = Array.IndexOf(Factions.All, faction);
		return index < 2 ? index : index + 1;
	}

	// Row 0 = top slot (slot 3) ... row 3 = the base
	private static float RowY(int row) => HeaderHeight + row * (CardSize.Y + RowGap);

	private static Vector2 SlotPosition(Faction faction, int slot) => new Vector2(ColumnX(ColumnOf(faction)), RowY(Castle.MaxSlots - 1 - slot));

	private static Vector2 BasePosition(Faction faction) => new Vector2(ColumnX(ColumnOf(faction)), RowY(Castle.MaxSlots));

	private void BuildCards()
	{
		UIText.ClearChildren(_canvas);
		_cardUpdates.Clear();
		_pickerKey = null;
		_canvas.CustomMinimumSize = new Vector2(ColumnX(5) - ColumnGap, RowY(Castle.MaxSlots + 1) - RowGap);

		foreach (var faction in Factions.All)
		{
			var header = new Label
			{
				Text = Factions.Name(faction),
				ThemeTypeVariation = "MutedLabel",
				HorizontalAlignment = HorizontalAlignment.Center,
				Position = new Vector2(ColumnX(ColumnOf(faction)), 0),
				Size = new Vector2(CardSize.X, HeaderHeight),
				MouseFilter = MouseFilterEnum.Ignore,
			};
			_canvas.AddChild(header);

			var layer = faction;
			var baseCard = MakeCard(BasePosition(faction), CardSize, () => SelectBase(layer));
			_cardUpdates.Add(() => UpdateBaseCard(baseCard, layer));
			for (int slot = 0; slot < Castle.MaxSlots; slot++)
			{
				int index = slot;
				var slotCard = MakeCard(SlotPosition(faction, slot), CardSize, () => SelectSlot(layer, index));
				_cardUpdates.Add(() => UpdateSlotCard(slotCard, layer, index));
			}
		}

		var towerHeader = new Label
		{
			Text = "Tower",
			ThemeTypeVariation = "MutedLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			Position = new Vector2(ColumnX(2), 0),
			Size = new Vector2(CardSize.X, HeaderHeight),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_canvas.AddChild(towerHeader);
		var towerCard = MakeCard(new Vector2(ColumnX(2), RowY(0)), new Vector2(CardSize.X, RowY(Castle.MaxSlots + 1) - RowGap - RowY(0)), SelectTower);
		_cardUpdates.Add(() => UpdateTowerCard(towerCard));
	}

	// A card button with three centred lines: Name, Sub and Cost
	private Button MakeCard(Vector2 position, Vector2 size, Action pressed)
	{
		var button = new Button
		{
			Position = position,
			Size = size,
			CustomMinimumSize = size,
			FocusMode = FocusModeEnum.None,
			ClipText = true,
		};
		var labels = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
		labels.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		labels.AddThemeConstantOverride("separation", 0);
		foreach (var name in new[] { "Name", "Sub", "Cost" })
			labels.AddChild(new Label
			{
				Name = name,
				HorizontalAlignment = HorizontalAlignment.Center,
				ThemeTypeVariation = name == "Name" ? "" : "MutedLabel",
				ClipText = true,
				MouseFilter = MouseFilterEnum.Ignore,
			});
		button.AddChild(labels);
		button.Pressed += pressed;
		_canvas.AddChild(button);
		return button;
	}

	private static void SetCardText(Button card, string name, string sub, string cost)
	{
		var labels = card.GetChild(0);
		labels.GetNode<Label>("Name").Text = name;
		labels.GetNode<Label>("Sub").Text = sub;
		labels.GetNode<Label>("Cost").Text = cost;
	}

	// e.g. "●●○"
	private static string Pips(int level) =>
		new string('●', level) + new string('○', Math.Max(0, BuildingDefinition.MaxLevel - level));

	private void UpdateTowerCard(Button card)
	{
		var tower = _castle.Towers.LastOrDefault(t => _castle.IsBuilt(t));
		var next = _castle.NextTower;
		var status = next == null ? BuildingStatus.MaxLevel : _castle.TowerStatus(next);
		string nextText = status switch
		{
			BuildingStatus.MaxLevel or BuildingStatus.Built => "Max level",
			_ => $"Next: {next.Name}\n{ResourceCost.Text(next.Cost).Replace(", ", "\n")}",
		};
		SetCardText(card, tower?.Name ?? "No tower", $"Level {Pips(_castle.TowerLevel)}", nextText);
		StyleCard(card, status == BuildingStatus.Available ? CardLook.Available : CardLook.Built, _pick == Pick.Tower);
	}

	private void UpdateBaseCard(Button card, Faction faction)
	{
		var building = _castle.Base(faction);
		int level = _castle.BaseLevel(faction);
		var status = _castle.BaseStatus(faction);
		string cost = status switch
		{
			BuildingStatus.MaxLevel => "Max level",
			BuildingStatus.NeedsTower => $"Needs Tower {level + 1}",
			_ => level == 0 ? "Click to build" : $"Upgrade to level {level + 1}",
		};
		SetCardText(card, building?.Name ?? $"{Factions.Name(faction)} base", $"Level {Pips(level)}", cost);
		var look = level == 0
			? (status == BuildingStatus.Available ? CardLook.Available : CardLook.Locked)
			: status == BuildingStatus.Available ? CardLook.Available : CardLook.Built;
		StyleCard(card, look, _pick == Pick.Base && _pickFaction == faction);
	}

	private void UpdateSlotCard(Button card, Faction faction, int slot)
	{
		bool selected = _pick == Pick.Slot && _pickFaction == faction && _pickSlot == slot;
		var building = _castle.SlotAt(faction, slot);
		if (building != null)
		{
			SetCardText(card, building.Name, ShortEffect(building), "Built");
			StyleCard(card, CardLook.Built, selected);
		}
		else if (slot < _castle.SlotCount(faction))
		{
			SetCardText(card, "Empty slot", "Click to build", "");
			StyleCard(card, CardLook.Empty, selected);
		}
		else
		{
			SetCardText(card, "Locked", $"Base level {slot + 1}", "");
			StyleCard(card, CardLook.Locked, selected);
		}
	}

	// One line that fits on a card, e.g. "Recruit Imp", "Imp +2 lv", "Quests"
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

	private void DrawLayers()
	{
		if (_castle == null)
			return;
		// Faint band behind each faction layer, and behind the slots it has opened
		foreach (var faction in Factions.All)
		{
			float x = ColumnX(ColumnOf(faction)) - 6;
			var column = new Rect2(x, HeaderHeight - 4, CardSize.X + 12, RowY(Castle.MaxSlots + 1) - RowGap - HeaderHeight + 8);
			_canvas.DrawRect(column, new Color(1, 1, 1, 0.025f));
			int open = _castle.SlotCount(faction);
			if (open > 0)
			{
				float top = SlotPosition(faction, open - 1).Y - 4;
				_canvas.DrawRect(new Rect2(x, top, CardSize.X + 12, RowY(Castle.MaxSlots) - top - 4), new Color(1, 1, 1, 0.03f));
			}
		}
	}

	private void StyleCard(Button button, CardLook look, bool selected)
	{
		(Color bg, Color border, Color text) = look switch
		{
			CardLook.Built => (new Color(0.38f, 0.16f, 0.1f), BrightGold, new Color(1f, 0.92f, 0.75f)),
			CardLook.Available => (new Color(0.16f, 0.13f, 0.12f), BrightGold, new Color(1f, 0.92f, 0.75f)),
			CardLook.NoResources => (new Color(0.13f, 0.11f, 0.11f), Gold.Darkened(0.4f), new Color(0.8f, 0.75f, 0.68f)),
			CardLook.Empty => (new Color(0.12f, 0.1f, 0.1f), Gold.Darkened(0.3f), new Color(0.85f, 0.78f, 0.68f)),
			_ => (new Color(0.09f, 0.085f, 0.09f), LockedGrey, new Color(0.5f, 0.48f, 0.46f)),
		};
		var style = new StyleBoxFlat { BgColor = bg, BorderColor = selected ? Colors.White : border };
		style.SetBorderWidthAll(selected ? 3 : look == CardLook.Available ? 2 : 1);
		style.SetCornerRadiusAll(5);
		var hover = (StyleBoxFlat)style.Duplicate();
		hover.BgColor = bg.Lightened(0.08f);
		button.AddThemeStyleboxOverride("normal", style);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("pressed", hover);
		button.GetChild(0).GetNode<Label>("Name").AddThemeColorOverride("font_color", text);
	}

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
		_progressLabel.Text = $"Tower level {_castle.TowerLevel}   Vision {_castle.VisionRadius:0}   Slots used {used}/{slots}   Gold {_castle.GoldPerTick:0.##}/tick";
		foreach (var update in _cardUpdates)
			update();
		_canvas.QueueRedraw();
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
		ShowBuildingText(next, maxed ? null : $"Costs {ResourceCost.Text(next.Cost)}");
		_detailStatus.Text = maxed ? "The tower is at its highest level." : StatusText(status, next);
		_buildButton.Visible = !maxed;
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
			: $"{(level == 0 ? "Build" : $"Level {level + 1}")} costs {ResourceCost.Text(_castle.NextBaseCost(faction))}");
		if (building.UnitCapFor != null)
			_detailEffects.Text += $"\n{building.UnitCapFor.Name} cap now {_castle.UnitCap(building.UnitCapFor)} (from all buildings)";
		_detailStatus.Text = status switch
		{
			BuildingStatus.MaxLevel => "Highest level: 3 slots open.",
			BuildingStatus.Available => level == 0 ? "Ready to build." : $"Ready to raise to level {level + 1}: opens slot {level + 1}.",
			BuildingStatus.NoResources => "Not enough resources.",
			BuildingStatus.NeedsTower => $"Needs Tower level {level + 1}.",
			_ => "",
		};
		_buildButton.Visible = status != BuildingStatus.MaxLevel;
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
		string blocked = PlayerAgents.RecruitBlockedReason(type);
		_recruitButton.TooltipText = !built ? "Build it first"
			: blocked ?? $"Recruit a level {level} {type.Name} for {ResourceCost.Text(building.RecruitCost)}"
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
		ShowBuildingText(_candidate, $"Costs {ResourceCost.Text(_candidate.Cost)}");
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
		_ => "Build the required buildings first.",
	};

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
