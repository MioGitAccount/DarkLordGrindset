using Godot;
using System;

// Moving items between the inventory and agents, shared by every place items can be dragged to
public static class ItemMoves
{
	// Why the item can't move from `from` (null = inventory) to `to` (null = inventory), or null when it can
	public static string BlockedReason(Item item, unit from, unit to)
	{
		if (item?.Definition == null)
			return "No item";
		if (from != null && !GodotObject.IsInstanceValid(from))
			return "That agent is gone";
		if (to != null && !GodotObject.IsInstanceValid(to))
			return "That agent is gone";
		if (from == to)
			return from == null ? "Already in your items" : "Already carried by this agent";
		if (from != null && from.GetItem(item.Definition.Slot) != item)
			return "That agent no longer carries it";
		if (from == null && !PlayerInventory.Contains(item))
			return "Not in your items";
		if (from != null)
		{
			string reason = from.UnequipBlockedReason(item.Definition.Slot);
			if (reason != null)
				return reason;
		}
		return to?.EquipBlockedReason(item);
	}

	public static bool Move(Item item, unit from, unit to)
	{
		if (BlockedReason(item, from, to) != null)
			return false;
		if (from != null && from.Unequip(item.Definition.Slot) == null)
			return false;
		return to == null || to.Equip(item);
	}

	public static Variant DragData(Item item, unit from)
	{
		var data = new Godot.Collections.Dictionary { ["item"] = item };
		if (from != null)
			data["from"] = from;
		return data;
	}

	// Reads drag data made by DragData; false for anything else being dragged
	public static bool ReadDragData(Variant data, out Item item, out unit from)
	{
		item = null;
		from = null;
		if (data.VariantType != Variant.Type.Dictionary)
			return false;
		var dict = data.AsGodotDictionary();
		if (!dict.TryGetValue("item", out var itemValue) || itemValue.AsGodotObject() is not Item found)
			return false;
		item = found;
		if (dict.TryGetValue("from", out var fromValue))
			from = fromValue.AsGodotObject() as unit;
		return true;
	}
}

// One item slot in the UI: an inventory cell or one of an agent's three slots.
// Items are dragged out of it and dropped onto it; right-click runs Activated (equip / take off).
public partial class ItemSlotView : Control
{
	// The agent whose slot this is; null = an inventory cell
	public unit Agent { get; private set; }
	// For an agent's slot: which one; it only accepts items for that slot
	public ItemSlot Slot { get; private set; }
	public Item Item { get; private set; }
	// Right-click on the item
	public event Action<ItemSlotView> Activated;
	// Extra tooltip line, e.g. "Right-click to give it to Imp"
	public Func<string> Hint;

	private readonly ItemIcon _icon = new();

	public static ItemSlotView ForInventory(Item item, float size)
	{
		var view = new ItemSlotView { CustomMinimumSize = new Vector2(size, size) };
		view.SetItem(item);
		return view;
	}

	public static ItemSlotView ForAgent(unit agent, ItemSlot slot, float size)
	{
		var view = new ItemSlotView { Agent = agent, Slot = slot, CustomMinimumSize = new Vector2(size, size) };
		view._icon.EmptyText = slot switch { ItemSlot.Hand => "Hand", ItemSlot.Jewelry => "Jewel", _ => "Wear" };
		view._icon.ShowLock = true;
		view.SetItem(agent?.GetItem(slot));
		return view;
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		_icon.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_icon);
		// Godot only shows a tooltip when there is some text; _GetTooltip gives the real one
		TooltipText = " ";
	}

	public void SetItem(Item item)
	{
		Item = item;
		_icon.Item = item;
		_icon.Definition = item?.Definition;
	}

	public override string _GetTooltip(Vector2 atPosition)
	{
		if (Item == null)
			return Agent != null ? $"{ItemDefinition.SlotName(Slot)} slot - drag an item here" : "";
		string text = Item.Definition.Tooltip();
		if (Agent != null && Item.IsLocked)
			text += $"\nLocked on this agent for {Math.Ceiling(Item.LockSecondsLeft)}s";
		string hint = Hint?.Invoke();
		if (!string.IsNullOrEmpty(hint))
			text += "\n" + hint;
		return text;
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (Item != null && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
		{
			Activated?.Invoke(this);
			AcceptEvent();
		}
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (Item == null)
			return default;
		var preview = new ItemIcon { Definition = Item.Definition, Size = Size, Modulate = new Color(1, 1, 1, 0.85f) };
		var holder = new Control();
		holder.AddChild(preview);
		preview.Position = -Size / 2;
		SetDragPreview(holder);
		return ItemMoves.DragData(Item, Agent);
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		if (!ItemMoves.ReadDragData(data, out var item, out var from))
			return false;
		if (Agent != null && item.Definition.Slot != Slot)
			return false;
		return ItemMoves.BlockedReason(item, from, Agent) == null;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (ItemMoves.ReadDragData(data, out var item, out var from))
			ItemMoves.Move(item, from, Agent);
	}
}

public static class AgentItemsRow
{
	// Fills `row` with the agent's three item slots (hand, jewelry, wear). Right-click takes an item off.
	public static void Build(Container row, unit agent, float size)
	{
		UIText.ClearChildren(row);
		if (!GodotObject.IsInstanceValid(agent))
			return;
		foreach (ItemSlot slot in Enum.GetValues(typeof(ItemSlot)))
		{
			var view = ItemSlotView.ForAgent(agent, slot, size);
			view.Hint = () => view.Item == null ? null
				: view.Item.IsLocked ? "Can't be taken off yet" : "Right-click or drag to your items to take it off";
			view.Activated += v => agent.Unequip(v.Slot);
			row.AddChild(view);
		}
	}
}
