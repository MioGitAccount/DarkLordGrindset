using Godot;
using System;

// The player's items that no agent carries. Opened from the Items button in the top bar or the
// Inspect window; it stays open next to the Inspect window so items can be dragged onto an agent.
// Dropping an agent's item here takes it off (once its lock is over).
public partial class InventoryWindow : PanelContainer
{
	private const int Columns = 5;
	private const float SlotSize = 44;
	private const float GridWidth = Columns * (SlotSize + 4);

	// The agent a right-click gives items to (the selected unit); set by UIManager
	public Func<unit> TargetAgent;
	// While this window is open the inventory sits just left of it (the Inspect window)
	public Control Beside;

	private Label _title;
	private GridContainer _grid;
	private Label _emptyLabel;
	private Label _hint;

	public override void _Ready()
	{
		Visible = false;
		SetAnchorsPreset(LayoutPreset.CenterLeft);
		Position = new Vector2(8, 0);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		AddChild(box);

		var header = new HBoxContainer();
		_title = new Label { ThemeTypeVariation = "HeaderLabel", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		var close = new Button { Text = "Close", FocusMode = FocusModeEnum.None };
		close.Pressed += Close;
		header.AddChild(_title);
		header.AddChild(close);
		box.AddChild(header);
		box.AddChild(new HSeparator());

		_grid = new GridContainer { Columns = Columns };
		_grid.AddThemeConstantOverride("h_separation", 4);
		_grid.AddThemeConstantOverride("v_separation", 4);
		var scroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(GridWidth + 8, 4 * (SlotSize + 4)),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		scroll.AddChild(_grid);
		box.AddChild(scroll);
		_emptyLabel = new Label
		{
			Text = "No items yet.\nGuards sit on loot - clear their field to take it.\nSome quests give items too.",
			ThemeTypeVariation = "MutedLabel",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(GridWidth, 0),
		};
		box.AddChild(_emptyLabel);
		_hint = new Label
		{
			ThemeTypeVariation = "MutedLabel",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(GridWidth, 0),
			Text = $"Drag onto an agent's slot, or right-click to give to the selected agent. Given items stay {Item.LockSeconds:0}s.",
		};
		box.AddChild(_hint);

		PlayerInventory.Changed += Rebuild;
		Rebuild();
	}

	public override void _ExitTree()
	{
		PlayerInventory.Changed -= Rebuild;
	}

	public void Toggle()
	{
		if (Visible)
			Close();
		else
			Open();
	}

	public void Open()
	{
		Rebuild();
		Visible = true;
		ResetSize();
		SetAnchorsAndOffsetsPreset(LayoutPreset.CenterLeft, LayoutPresetMode.KeepSize, 8);
	}

	public void Close() => Visible = false;

	public override void _Process(double delta)
	{
		if (!Visible)
			return;
		// Next to the Inspect window when it is open, else at the left edge
		var screen = GetViewportRect().Size;
		Vector2 pos = new(8, (screen.Y - Size.Y) / 2);
		if (Beside != null && Beside.Visible)
			pos = new Vector2(Mathf.Max(8, Beside.Position.X - Size.X - 8), Beside.Position.Y);
		Position = new Vector2(pos.X, Mathf.Clamp(pos.Y, 8, Mathf.Max(8, screen.Y - Size.Y - 8)));
	}

	private void Rebuild()
	{
		if (_grid == null)
			return;
		_title.Text = $"Items ({PlayerInventory.Count})";
		UIText.ClearChildren(_grid);
		foreach (var item in PlayerInventory.Items)
		{
			var view = ItemSlotView.ForInventory(item, SlotSize);
			view.Hint = () =>
			{
				var target = TargetAgent?.Invoke();
				if (!IsInstanceValid(target))
					return "Select an agent, then right-click to give it";
				string blocked = target.EquipBlockedReason(item);
				return blocked ?? $"Right-click to give it to {target.Template?.Name ?? target.Name}";
			};
			view.Activated += v =>
			{
				var target = TargetAgent?.Invoke();
				if (IsInstanceValid(target))
					ItemMoves.Move(v.Item, null, target);
			};
			_grid.AddChild(view);
		}
		_emptyLabel.Visible = PlayerInventory.Count == 0;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Visible && @event.IsActionPressed("ui_cancel"))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	// An agent's item dropped anywhere on the window goes back to the inventory
	public override bool _CanDropData(Vector2 atPosition, Variant data) =>
		ItemMoves.ReadDragData(data, out var item, out var from) && from != null && ItemMoves.BlockedReason(item, from, null) == null;

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (ItemMoves.ReadDragData(data, out var item, out var from))
			ItemMoves.Move(item, from, null);
	}
}
