using Godot;
using System;
using System.Linq;

// Right-side panel describing the selected field: biome, resources, neutral and player units.
// Refreshes itself while units enter or leave the shown field and when its fog changes.
// In grey fog it shows the hostiles as last seen and hides live resource amounts.
// Every section keeps a fixed height, so the panel never changes size between fields.
public partial class FieldInfoPanel : PanelContainer
{
	public event Action<unit> UnitClicked;
	// The "Enter castle" button of a castle field
	public event Action<Castle> CastleRequested;
	// A quest button of the field was clicked
	public event Action<Quest> QuestRequested;

	// Set by UIManager; the panel lists the shown field's quests
	public QuestManager Quests
	{
		get => _quests;
		set
		{
			if (_quests != null)
				_quests.QuestAdded -= OnQuestsChanged;
			_quests = value;
			if (_quests != null)
				_quests.QuestAdded += OnQuestsChanged;
		}
	}
	private QuestManager _quests;

	[Export] public PackedScene IconSlotScene;
	[Export] public PackedScene ResourceCounterScene;

	[Export] private Label TitleLabel;
	[Export] private TextureRect FieldImage;
	[Export] private Container ResourcesRow;
	[Export] private Control NeutralSection;
	[Export] private Container NeutralUnitsRow;
	[Export] private Label PlayerHeader;
	[Export] private Container PlayerUnitsRow;

	private const int UnitSlotSize = 34;

	private field _field;
	private Label _neutralHeader;
	private Label _perksLabel;
	private Button _castleButton;
	private HBoxContainer _questsRow;
	// The quests the row's buttons show, so the timers update without rebuilding the buttons
	private readonly System.Collections.Generic.List<(Quest, Button)> _questButtons = new();

	public override void _Ready()
	{
		_neutralHeader = NeutralSection.GetNode<Label>("NeutralHeader");
		// Created in code so saving UI.tscn in the editor can't drop them; one fixed-height row
		// right under the resources: perks on the left, the castle button (castle fields only) on the right
		var perksRow = new HBoxContainer { Name = "PerksRow", CustomMinimumSize = new Vector2(0, 30) };
		perksRow.AddThemeConstantOverride("separation", 6);
		_perksLabel = new Label
		{
			Name = "PerksLabel",
			ThemeTypeVariation = "MutedLabel",
			ClipText = true, // one line; the tooltip has the full list
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Pass, // so the tooltip shows
		};
		perksRow.AddChild(_perksLabel);
		// Shown on fields with the Castle perk; opens the build window
		_castleButton = new Button { Name = "CastleButton", Text = "Enter castle", FocusMode = FocusModeEnum.None, SizeFlagsVertical = SizeFlags.ShrinkCenter };
		_castleButton.Pressed += () => CastleRequested?.Invoke(_field?.Castle);
		perksRow.AddChild(_castleButton);
		ResourcesRow.GetParent().AddChild(perksRow);
		ResourcesRow.GetParent().MoveChild(perksRow, ResourcesRow.GetIndex() + 1);
		// Quests of the field, one fixed-height row under the perks; each button opens its quest
		_questsRow = new HBoxContainer { Name = "QuestsRow", CustomMinimumSize = new Vector2(0, 30), ClipContents = true };
		_questsRow.AddThemeConstantOverride("separation", 6);
		ResourcesRow.GetParent().AddChild(_questsRow);
		ResourcesRow.GetParent().MoveChild(_questsRow, perksRow.GetIndex() + 1);
		ShowField(null);
	}

	public override void _ExitTree()
	{
		if (_field != null)
			_field.ContentsChanged -= Refresh;
		Quests = null;
	}

	private void OnQuestsChanged(Quest quest)
	{
		if (_field != null && quest.Field == _field)
			RefreshQuests();
	}

	public override void _Process(double delta)
	{
		// Timed perks (e.g. "Raided") count down; refresh their seconds left a few times a second
		_perksRefreshTimer -= delta;
		if (_field == null || _perksRefreshTimer > 0)
			return;
		_perksRefreshTimer = 0.5;
		RefreshPerks();
		RefreshQuestTimers();
	}
	private double _perksRefreshTimer;

	public void ShowField(field selectedField)
	{
		if (_field != null)
			_field.ContentsChanged -= Refresh;

		_field = selectedField;
		Visible = _field != null;
		if (_field == null)
			return;

		_field.ContentsChanged += Refresh;
		FieldImage.Texture = _field.type?.FieldImage;
		Refresh();
	}

	private bool InSight => _field.Fog == FogState.Visible;

	private void Refresh()
	{
		TitleLabel.Text = (_field.type?.Name ?? _field.Name) + (_field.IsPlayerOwned ? " (yours)" : InSight ? "" : " (last seen)");
		_castleButton.Visible = _field.Castle != null;
		if (_field.Castle != null)
		{
			_castleButton.Text = "Enter castle";
			_castleButton.TooltipText = $"Tower {_field.Castle.TowerLevel}";
		}
		RefreshResources();
		RefreshPerks();
		RefreshQuests();
		RefreshNeutralUnits();
		RefreshPlayerUnits();
	}

	private void RefreshResources()
	{
		UIText.ClearChildren(ResourcesRow);
		var resources = _field.resourcesHolder?.Resources;
		if (!InSight || resources == null || resources.Count == 0)
		{
			ResourcesRow.AddChild(new Label { Text = InSight ? "No resources" : "Resources unknown", ThemeTypeVariation = "MutedLabel" });
			return;
		}

		foreach (var resource in resources)
		{
			var counter = ResourceCounterScene.Instantiate<ResourceCounter>();
			counter.Bind(resource);
			ResourcesRow.AddChild(counter);
		}
	}

	// Perks are part of the land, so they stay known in fog
	private void RefreshPerks()
	{
		var perks = new System.Collections.Generic.List<FieldPerk>(_field.Perks);
		if (perks.Count == 0)
		{
			_perksLabel.Text = "No perks";
			_perksLabel.TooltipText = "";
			return;
		}
		var names = new System.Collections.Generic.List<string>();
		var details = new System.Collections.Generic.List<string>();
		foreach (var perk in perks)
		{
			int ticksLeft = _field.GetTimedPerkTicksLeft(perk);
			names.Add(ticksLeft > 0 ? $"{perk.Name} ({ticksLeft}s)" : perk.Name);
			details.Add($"{perk.Name}: {perk.Description}\n  {perk.EffectText()}");
		}
		_perksLabel.Text = "Perks: " + string.Join(", ", names);
		_perksLabel.TooltipText = string.Join("\n", details);
	}

	private void RefreshQuests()
	{
		UIText.ClearChildren(_questsRow);
		_questButtons.Clear();
		var quests = _quests?.QuestsOn(_field).ToList() ?? new System.Collections.Generic.List<Quest>();
		if (quests.Count == 0)
		{
			_questsRow.AddChild(new Label { Text = "No quests", ThemeTypeVariation = "MutedLabel", SizeFlagsVertical = SizeFlags.ShrinkCenter });
			return;
		}
		foreach (var quest in quests)
		{
			var button = new Button { FocusMode = FocusModeEnum.None, SizeFlagsVertical = SizeFlags.ShrinkCenter, TooltipText = "Open the quest" };
			var shown = quest;
			button.Pressed += () => QuestRequested?.Invoke(shown);
			_questsRow.AddChild(button);
			_questButtons.Add((quest, button));
		}
		RefreshQuestTimers();
	}

	private void RefreshQuestTimers()
	{
		foreach (var (quest, button) in _questButtons)
		{
			string time = quest.HasDeadline ? $" {quest.TimeLeftText}" : "";
			string mark = quest.State == QuestState.WaitingForBattle ? "Fight! " : quest.IsStarted ? "... " : "! ";
			button.Text = $"{mark}{quest.Title} (Lv {quest.Definition.Level}){time}";
			button.Modulate = quest.IsExpiringSoon ? new Color(1f, 0.6f, 0.55f) : Colors.White;
		}
	}

	private void RefreshNeutralUnits()
	{
		UIText.ClearChildren(NeutralUnitsRow);
		var neutrals = InSight ? _field.presentNeutralUnits : _field.LastSeenNeutrals;
		bool any = neutrals != null && neutrals.Count > 0;
		_neutralHeader.Text = (InSight ? "Hostiles" : "Hostiles (last seen)") + (any ? $": {neutrals.Count}" : ": none");
		if (!any)
			return;

		foreach (var neutral in neutrals)
		{
			var slot = IconSlotScene.Instantiate<IconSlot>();
			slot.CustomMinimumSize = new Vector2(UnitSlotSize, UnitSlotSize);
			string name = neutral.Template?.Name ?? "Unknown";
			slot.Setup(neutral.GetIcon(), () => UIText.UnitTooltip(name, neutral.GetEffectiveStats(), neutral.GetAttributes()) + "\n" + UIText.Threat(neutral.Level));
			NeutralUnitsRow.AddChild(slot);
		}
	}

	private void RefreshPlayerUnits()
	{
		UIText.ClearChildren(PlayerUnitsRow);
		var units = _field.presentPlayerUnits;
		bool hasUnits = units != null && units.Count > 0;
		PlayerHeader.Text = hasUnits ? $"Your units: {units.Count}" : "Your units: none";
		if (!hasUnits)
			return;

		foreach (var playerUnit in units)
		{
			var slot = IconSlotScene.Instantiate<IconSlot>();
			slot.CustomMinimumSize = new Vector2(UnitSlotSize, UnitSlotSize);
			string name = playerUnit.Template?.Name ?? playerUnit.Name;
			slot.Setup(playerUnit.GetIcon(), () => UIText.UnitTooltip(name, playerUnit.battleStats, playerUnit.Attributes) + "\nClick to select");
			slot.Pressed += () => UnitClicked?.Invoke(playerUnit);
			PlayerUnitsRow.AddChild(slot);
		}
	}
}
