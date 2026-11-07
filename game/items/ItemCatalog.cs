using Godot;
using System.Collections.Generic;
using System.Linq;

// Every item in the game. Stats are fixed per item; add a line to add an item.
// Tier guide: 1 = one small bonus (1-2 ATK, 1 DEF, 1-3 HP or 1 attribute),
// 2 = 2-3 ATK or a pair (1 QUI + 1 ATK, 1 CLE + 2 HP, 4-5 HP...), 3 = stronger mixes (2 DUR + 6 HP...).
public static class ItemCatalog
{
	private static ItemDefinition I(string id, string name, ItemKind kind, ItemMaterial material, int tier,
		int atk = 0, int def = 0, int hp = 0, int dea = 0, int str = 0, int dur = 0, int cle = 0, int qui = 0,
		string flavour = "") => new()
	{
		Id = id, Name = name, Kind = kind, Material = material, Tier = tier,
		Attack = atk, Defence = def, Health = hp,
		Attributes = new[] { dea, str, dur, cle, qui },
		Flavour = flavour,
	};

	public static readonly Dictionary<string, ItemDefinition> Items = new[]
	{
		// --- Tier 1 ------------------------------------------------------------------------------
		I("iron_ring", "Iron Ring", ItemKind.Ring, ItemMaterial.Iron, 1, dur: 1, flavour: "Heavy, plain, honest."),
		I("silver_ring", "Silver Ring", ItemKind.Ring, ItemMaterial.Silver, 1, cle: 1),
		I("bone_bracelet", "Bone Bracelet", ItemKind.Bracelet, ItemMaterial.Bone, 1, dea: 1, flavour: "Finger bones on a string."),
		I("lucky_coin", "Lucky Coin", ItemKind.Coin, ItemMaterial.Silver, 1, qui: 1, flavour: "Always lands on the right side."),
		I("iron_chain", "Iron Chain", ItemKind.Necklace, ItemMaterial.Iron, 1, hp: 2),
		I("tattered_cloak", "Tattered Cloak", ItemKind.Cloak, ItemMaterial.Cloth, 1, hp: 2),
		I("leather_jerkin", "Leather Jerkin", ItemKind.Jerkin, ItemMaterial.Leather, 1, def: 1),
		I("iron_cap", "Iron Cap", ItemKind.Helmet, ItemMaterial.Iron, 1, def: 1),
		I("wool_hood", "Wool Hood", ItemKind.Hood, ItemMaterial.Cloth, 1, qui: 1),
		I("pointed_hat", "Pointed Hat", ItemKind.WizardCap, ItemMaterial.Cloth, 1, cle: 1),
		I("rusty_sword", "Rusty Sword", ItemKind.Sword, ItemMaterial.Iron, 1, atk: 2),
		I("skinning_knife", "Skinning Knife", ItemKind.Knife, ItemMaterial.Iron, 1, atk: 1),
		I("wooden_shield", "Wooden Shield", ItemKind.Shield, ItemMaterial.Wood, 1, def: 1),
		I("oak_staff", "Oak Staff", ItemKind.Staff, ItemMaterial.Wood, 1, hp: 3),
		I("prayer_book", "Worn Prayer Book", ItemKind.Book, ItemMaterial.Leather, 1, cle: 1, flavour: "Someone else's god. Still useful."),
		I("iron_mace", "Iron Mace", ItemKind.Mace, ItemMaterial.Iron, 1, str: 1),

		// --- Tier 2 ------------------------------------------------------------------------------
		I("silver_bracelet", "Silver Bracelet", ItemKind.Bracelet, ItemMaterial.Silver, 2, atk: 1, qui: 1),
		I("gold_ring", "Gold Ring", ItemKind.Ring, ItemMaterial.Gold, 2, hp: 2, cle: 1),
		I("gilded_coin", "Gilded Luck Coin", ItemKind.Coin, ItemMaterial.Gold, 2, cle: 1, qui: 1),
		I("bone_amulet", "Bone Amulet", ItemKind.Amulet, ItemMaterial.Bone, 2, atk: 1, dea: 1),
		I("fur_cloak", "Fur Cloak", ItemKind.Cloak, ItemMaterial.Leather, 2, hp: 5),
		I("iron_breastplate", "Iron Breastplate", ItemKind.Breastplate, ItemMaterial.Iron, 2, def: 1, hp: 2),
		I("dark_robe", "Dark Robe", ItemKind.Robe, ItemMaterial.Cloth, 2, dur: 1, cle: 1),
		I("iron_helmet", "Iron Helmet", ItemKind.Helmet, ItemMaterial.Iron, 2, hp: 2, dur: 1),
		I("bronze_circlet", "Bronze Circlet", ItemKind.Crown, ItemMaterial.Bronze, 2, str: 1, cle: 1),
		I("wizard_cap", "Wizard's Cap", ItemKind.WizardCap, ItemMaterial.Cloth, 2, cle: 1, qui: 1),
		I("steel_sword", "Steel Sword", ItemKind.Sword, ItemMaterial.Steel, 2, atk: 3),
		I("bearded_axe", "Bearded Axe", ItemKind.Axe, ItemMaterial.Iron, 2, atk: 2, str: 1),
		I("curved_dagger", "Curved Dagger", ItemKind.Knife, ItemMaterial.Steel, 2, atk: 2, qui: 1),
		I("kite_shield", "Kite Shield", ItemKind.Shield, ItemMaterial.Iron, 2, def: 1, hp: 3),
		I("book_of_hexes", "Book of Hexes", ItemKind.Book, ItemMaterial.Leather, 2, cle: 2),
		I("bone_staff", "Bone Staff", ItemKind.Staff, ItemMaterial.Bone, 2, atk: 1, cle: 1),

		// --- Tier 3 ------------------------------------------------------------------------------
		I("gold_necklace", "Gold Necklace", ItemKind.Necklace, ItemMaterial.Gold, 3, hp: 4, cle: 2),
		I("gold_bracelet", "Gold Bracelet", ItemKind.Bracelet, ItemMaterial.Gold, 3, atk: 1, qui: 2),
		I("bloodstone_ring", "Bloodstone Ring", ItemKind.Ring, ItemMaterial.Gold, 3, atk: 1, dea: 2, flavour: "Warm to the touch. Always."),
		I("silver_signet", "Silver Signet", ItemKind.Ring, ItemMaterial.Silver, 3, hp: 3, dur: 2),
		I("steel_plate", "Steel Plate Armour", ItemKind.Breastplate, ItemMaterial.Steel, 3, hp: 6, dur: 2),
		I("silvered_chainmail", "Silvered Chainmail", ItemKind.Chainmail, ItemMaterial.Silver, 3, def: 1, hp: 4, dur: 1),
		I("shadow_cloak", "Shadow Cloak", ItemKind.Cloak, ItemMaterial.Cloth, 3, hp: 3, qui: 2),
		I("gold_crown", "Gold Crown", ItemKind.Crown, ItemMaterial.Gold, 3, hp: 2, str: 1, cle: 2, flavour: "Taken from a head that no longer needed it."),
		I("horned_helm", "Horned Helm", ItemKind.Helmet, ItemMaterial.Steel, 3, hp: 4, str: 2),
		I("archmage_hat", "Archmage's Hat", ItemKind.WizardCap, ItemMaterial.Cloth, 3, hp: 2, cle: 3),
		I("rune_sword", "Rune Sword", ItemKind.Sword, ItemMaterial.Steel, 3, atk: 4, dea: 1),
		I("executioners_axe", "Executioner's Axe", ItemKind.Axe, ItemMaterial.Steel, 3, atk: 3, str: 2),
		I("assassins_kris", "Assassin's Kris", ItemKind.Knife, ItemMaterial.Steel, 3, atk: 1, dea: 2, qui: 1),
		I("tower_shield", "Tower Shield", ItemKind.Shield, ItemMaterial.Steel, 3, def: 2, hp: 4),
		I("grimoire_of_bones", "Grimoire of Bones", ItemKind.Book, ItemMaterial.Bone, 3, dea: 1, cle: 3),
		I("gilded_staff", "Gilded Staff", ItemKind.Staff, ItemMaterial.Gold, 3, atk: 2, cle: 2),
	}.ToDictionary(i => i.Id);

	public static int MaxTier => Items.Values.Max(i => i.Tier);

	public static ItemDefinition Get(string id) => id != null ? Items.GetValueOrDefault(id) : null;

	public static ItemDefinition RandomOfTier(int tier, RandomNumberGenerator rng = null)
	{
		var pool = Items.Values.Where(i => i.Tier == Mathf.Clamp(tier, 1, MaxTier)).ToList();
		if (pool.Count == 0)
			return null;
		int index = rng != null ? rng.RandiRange(0, pool.Count - 1) : (int)(GD.Randi() % (uint)pool.Count);
		return pool[index];
	}

	// --- Guard loot --------------------------------------------------------------------------------

	// Chance that a guarded field holds an item, by its strongest guard's level (1-5)
	private static readonly float[] LootChance = { 0.5f, 0.6f, 0.75f, 0.9f, 1f };
	// Chance of tier 1 / 2 / 3 by the strongest guard's level
	private static readonly float[][] TierWeights =
	{
		new[] { 1f, 0f, 0f },
		new[] { 0.65f, 0.35f, 0f },
		new[] { 0f, 0.75f, 0.25f },
		new[] { 0f, 0.4f, 0.6f },
		new[] { 0f, 0f, 1f },
	};

	// The item guards of this level sit on, or null when they hold none
	public static ItemDefinition RollGuardLoot(int guardLevel, RandomNumberGenerator rng)
	{
		int level = Mathf.Clamp(guardLevel, 1, 5) - 1;
		if (rng.Randf() >= LootChance[level])
			return null;
		float roll = rng.Randf();
		var weights = TierWeights[level];
		int tier = weights.Length;
		for (int i = 0; i < weights.Length; i++)
		{
			roll -= weights[i];
			if (roll < 0) { tier = i + 1; break; }
		}
		return RandomOfTier(tier, rng);
	}
}
