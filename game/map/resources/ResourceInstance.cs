using Godot;
using System;

// One resource lying on a field: how much there is, the most it can hold and how much grows back per tick
public partial class ResourceInstance
{
	public float CurrentAmount{ get; set; }
	public float MaxAmount { get; set; }
	public float RegenRate { get; set; }
	public ResourceType type{ get; set; }
	// True while a perk like "Raided" stops the field from generating
	public bool Paused { get; set; }

	public event Action<float> OnAmountChanged;

	public ResourceInstance(ResourceType type, float initialAmount, float maxAmount, float regenRate)
	{
		this.type = type;
		CurrentAmount = initialAmount;
		MaxAmount = maxAmount;
		RegenRate = regenRate;
		GlobalTickManager.Instance.OnTick += HandleTick;
	}

	private void HandleTick()
	{
		if (Paused || CurrentAmount >= MaxAmount)
			return;
		CurrentAmount = Mathf.Min(CurrentAmount + RegenRate, MaxAmount);
		OnAmountChanged?.Invoke(CurrentAmount);
	}

	// Removes up to `amount` and returns how much was actually there to take
	public float Take(float amount)
	{
		float taken = Mathf.Clamp(amount, 0, CurrentAmount);
		if (taken <= 0)
			return 0;
		CurrentAmount -= taken;
		OnAmountChanged?.Invoke(CurrentAmount);
		return taken;
	}
}
