using Godot;
using System;
using System.Collections.Generic;

// Items the player owns that no agent carries. Loot from cleared fields and quest rewards land here;
// the player drags them onto agents (see unit.Equip). The Items button in the top bar shows it.
public static class PlayerInventory
{
	private static readonly List<Item> _items = new();

	public static IReadOnlyList<Item> Items => _items;
	public static int Count => _items.Count;

	public static event Action Changed;
	// (item, where it came from, e.g. "loot from Dark Forest") - for the "Found ..." toast
	public static event Action<Item, string> ItemFound;

	public static Item Add(ItemDefinition definition, string source = null)
	{
		if (definition == null)
			return null;
		var item = new Item(definition);
		_items.Add(item);
		Changed?.Invoke();
		if (source != null)
			ItemFound?.Invoke(item, source);
		return item;
	}

	// An item coming back from an agent (taken off, or the agent died)
	public static void Return(Item item)
	{
		if (item == null || _items.Contains(item))
			return;
		item.LockedUntilMsec = 0;
		_items.Add(item);
		Changed?.Invoke();
	}

	public static bool Remove(Item item)
	{
		if (!_items.Remove(item))
			return false;
		Changed?.Invoke();
		return true;
	}

	public static bool Contains(Item item) => _items.Contains(item);

	// Start of a new game: empty
	public static void Reset()
	{
		_items.Clear();
		Changed?.Invoke();
	}
}
