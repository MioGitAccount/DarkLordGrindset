using Godot;
using System;

// Root of the in-game HUD. The rest of the game talks to the UI only through this class.
public partial class UIManager : CanvasLayer
{
	// Raised when the player picks a unit from the UI (e.g. from the field panel)
	public event Action<unit> UnitSelected;

	[Export] private FieldInfoPanel FieldInfo;
	[Export] private SelectedUnitPanel SelectedUnit;
	[Export] private ResourceCounter PlayerGold;
	[Export] private ResourceCounter PlayerWood;
	[Export] private Control BattleOverlay;
	[Export] private BaseButton BattleButton;
	[Export] private BaseButton CloseBattleButton;
	// Created in code (it builds its own contents), so it doesn't depend on UI.tscn
	private FieldHoverCard FieldHoverCard;
	// Top bar counter for every ResourceType; the player always sees all of them
	private readonly System.Collections.Generic.Dictionary<ResourceType, ResourceCounter> _playerCounters = new();
	private UnitInspectWindow UnitInspect;
	private SkillTreeWindow SkillTree;
	private BuildWindow Build;

	public override void _Ready()
	{
		CreatePlayerResourceCounters();
		FieldHoverCard = new FieldHoverCard { Name = "FieldHoverCard" };
		FieldInfo.GetParent().AddChild(FieldHoverCard);
		FieldInfo.UnitClicked += selected => UnitSelected?.Invoke(selected);
		UnitInspect = new UnitInspectWindow { Name = "UnitInspect" };
		FieldInfo.GetParent().AddChild(UnitInspect);
		SkillTree = new SkillTreeWindow { Name = "SkillTree" };
		FieldInfo.GetParent().AddChild(SkillTree);
		Build = new BuildWindow { Name = "Build" };
		FieldInfo.GetParent().AddChild(Build);
		// The windows share the centre of the screen, so only one is open at a time
		SelectedUnit.InspectRequested += selected => { CloseWindows(); UnitInspect.Open(selected); };
		SelectedUnit.SkillsRequested += selected => { CloseWindows(); SkillTree.Open(selected); };
		FieldInfo.CastleRequested += castle => { CloseWindows(); Build.Open(castle); };
		BattleButton.Pressed += () => SetBattleVisible(!BattleOverlay.Visible);
		CloseBattleButton.Pressed += () => SetBattleVisible(false);
		SetBattleVisible(false);
	}

	private void CloseWindows()
	{
		UnitInspect.Close();
		SkillTree.Close();
		Build.Close();
	}

	public void ShowField(field selectedField)
	{
		FieldInfo.ShowField(selectedField);
	}

	public void ShowFieldHover(field hoveredField)
	{
		FieldHoverCard.ShowFor(hoveredField);
	}

	// True when the mouse is over a visible HUD panel (the map shouldn't react then)
	public bool IsMouseOverPanel()
	{
		Vector2 mouse = GetViewport().GetMousePosition();
		foreach (var child in FieldInfo.GetParent().GetChildren())
		{
			if (child is Control panel && panel.Visible && panel.MouseFilter != Control.MouseFilterEnum.Ignore
					&& panel.GetGlobalRect().HasPoint(mouse))
				return true;
		}
		return false;
	}

	public void ShowUnit(unit selectedUnit)
	{
		SelectedUnit.ShowUnit(selectedUnit);
		// An open inspect window follows the selection
		if (UnitInspect.Visible)
			UnitInspect.Open(selectedUnit);
		if (SkillTree.Visible)
			SkillTree.Open(selectedUnit);
	}

	// The scene has Gold and Wood counters; the other resource types get a copy of them,
	// placed right after the last counter, so new ResourceType values show up automatically
	private void CreatePlayerResourceCounters()
	{
		var bar = PlayerGold.GetParent();
		foreach (var child in bar.GetChildren())
			if (child is ResourceCounter counter)
				_playerCounters[counter.Type] = counter;

		int insertAt = PlayerWood.GetIndex() + 1;
		foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
		{
			if (_playerCounters.ContainsKey(type))
				continue;
			var counter = (ResourceCounter)PlayerGold.Duplicate();
			counter.Name = $"Player{type}";
			counter.Type = type;
			counter.Amount = 0;
			bar.AddChild(counter);
			bar.MoveChild(counter, insertAt++);
			_playerCounters[type] = counter;
		}
	}

	public void SetPlayerResource(ResourceType type, float amount)
	{
		if (_playerCounters.TryGetValue(type, out var counter))
			counter.SetAmount(amount);
	}

	public void SetBattleVisible(bool visible)
	{
		BattleOverlay.Visible = visible;
	}
}
