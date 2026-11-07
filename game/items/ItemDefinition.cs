using Godot;
using System.Collections.Generic;

// The three places an agent can carry an item. Body and head items share the Wear slot.
public enum ItemSlot { Hand, Jewelry, Wear }

// What the item is; decides its slot and the icon drawn for it (see ItemIcon)
public enum ItemKind
{
	// Jewelry
	Ring, Bracelet, Necklace, Amulet, Coin,
	// Body (Wear slot)
	Cloak, Robe, Jerkin, Breastplate, Chainmail,
	// Head (Wear slot)
	Helmet, Crown, WizardCap, Hood,
	// One-handed (Hand slot)
	Sword, Knife, Axe, Mace, Shield, Book, Staff,
}

// What the item is made of. Flavour only: it names and tints the item, it doesn't change stats.
public enum ItemMaterial { Iron, Bronze, Steel, Silver, Gold, Leather, Cloth, Wood, Bone }

// One kind of item, with fixed stats (every Steel Sword is the same). See ItemCatalog for the list.
public class ItemDefinition
{
	public string Id;
	public string Name;
	public ItemKind Kind;
	public ItemMaterial Material;
	// 1 = base, 2, 3 = strongest so far; more tiers may come
	public int Tier = 1;
	public int Attack;
	public int Defence;
	public int Health;
	// Added to the agent's attributes, in SpecialAttributes order (DEA, STR, DUR, CLE, QUI)
	public int[] Attributes = new int[SpecialAttributes.Count];
	public string Flavour = "";

	public ItemSlot Slot => SlotOf(Kind);
	public bool IsHead => Kind is ItemKind.Helmet or ItemKind.Crown or ItemKind.WizardCap or ItemKind.Hood;

	public static ItemSlot SlotOf(ItemKind kind) => kind switch
	{
		ItemKind.Ring or ItemKind.Bracelet or ItemKind.Necklace or ItemKind.Amulet or ItemKind.Coin => ItemSlot.Jewelry,
		ItemKind.Cloak or ItemKind.Robe or ItemKind.Jerkin or ItemKind.Breastplate or ItemKind.Chainmail
			or ItemKind.Helmet or ItemKind.Crown or ItemKind.WizardCap or ItemKind.Hood => ItemSlot.Wear,
		_ => ItemSlot.Hand,
	};

	public static string SlotName(ItemSlot slot) => slot switch
	{
		ItemSlot.Hand => "One hand",
		ItemSlot.Jewelry => "Jewelry",
		_ => "Body or head",
	};

	public string CategoryName => Slot == ItemSlot.Wear ? (IsHead ? "Head" : "Body") : SlotName(Slot);

	// "+2 ATK, +1 QUI"
	public string StatsText()
	{
		var parts = new List<string>();
		if (Attack != 0) parts.Add($"{Attack:+0;-0} ATK");
		if (Defence != 0) parts.Add($"{Defence:+0;-0} DEF");
		if (Health != 0) parts.Add($"{Health:+0;-0} HP");
		for (int i = 0; i < SpecialAttributes.Count; i++)
			if (Attributes[i] != 0)
				parts.Add($"{Attributes[i]:+0;-0} {SpecialAttributes.ShortNames[i]}");
		return parts.Count > 0 ? string.Join(", ", parts) : "no bonus";
	}

	public string Tooltip()
	{
		string text = $"{Name}\nTier {Tier} {CategoryName.ToLower()} - {Material.ToString().ToLower()}\n{StatsText()}";
		if (!string.IsNullOrEmpty(Flavour))
			text += $"\n\"{Flavour}\"";
		return text;
	}

	public static Color TierColor(int tier) => tier switch
	{
		1 => new Color(0.62f, 0.6f, 0.56f),
		2 => new Color(0.35f, 0.62f, 0.95f),
		3 => new Color(0.78f, 0.45f, 0.95f),
		_ => new Color(1f, 0.65f, 0.2f),
	};

	public static Color MaterialColor(ItemMaterial material) => material switch
	{
		ItemMaterial.Iron => new Color(0.55f, 0.56f, 0.58f),
		ItemMaterial.Bronze => new Color(0.72f, 0.48f, 0.25f),
		ItemMaterial.Steel => new Color(0.75f, 0.8f, 0.86f),
		ItemMaterial.Silver => new Color(0.86f, 0.88f, 0.92f),
		ItemMaterial.Gold => new Color(0.96f, 0.78f, 0.25f),
		ItemMaterial.Leather => new Color(0.55f, 0.36f, 0.2f),
		ItemMaterial.Cloth => new Color(0.42f, 0.36f, 0.55f),
		ItemMaterial.Wood => new Color(0.6f, 0.43f, 0.25f),
		_ => new Color(0.88f, 0.85f, 0.74f), // bone
	};
}

// One item the player owns: in the inventory or carried by an agent. A GodotObject so it can be
// dragged between UI slots.
public partial class Item : RefCounted
{
	// Seconds an item stays on an agent after being given, so items can't be passed around in circles
	public const double LockSeconds = 60;

	public ItemDefinition Definition { get; }
	// Time.GetTicksMsec() when the lock ends (0 = not locked)
	public ulong LockedUntilMsec;

	public Item() { }
	public Item(ItemDefinition definition) { Definition = definition; }

	public string Name => Definition.Name;
	public double LockSecondsLeft => System.Math.Max(0, ((long)LockedUntilMsec - (long)Time.GetTicksMsec()) / 1000.0);
	public bool IsLocked => LockSecondsLeft > 0;

	public void Lock() => LockedUntilMsec = Time.GetTicksMsec() + (ulong)(LockSeconds * 1000);
}
