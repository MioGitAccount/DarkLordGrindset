using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;

public partial class ResourceInstance
{
	[Export]
	public float CurrentAmount{ get; set; }
	[Export]
	public float MaxAmount { get; set; }
	[Export]
	public float RegenRate { get; set; }
	[Export]
	public ResourceType type{ get; set; }

	public event Action<float> OnAmountChanged;

	private readonly List<ResourceDrainRequest> _activeDrains = new();


	public  override void _Ready()
	{
		GlobalTickManager.Instance.OnTick += HandleTick;
	}
	private void HandleTick()
	{
		// 1️⃣ Regenerate first
		if (CurrentAmount < MaxAmount)
		{
			CurrentAmount += RegenRate;
			CurrentAmount = Mathf.Min(CurrentAmount, MaxAmount);
		}

		// 2️⃣ Process drains
		foreach (var drain in _activeDrains)
		{
			if (CurrentAmount <= 0)
				break;

			float actualDrain = Mathf.Min(drain.DrainPerTick, CurrentAmount);

			CurrentAmount -= actualDrain;

			// Give resources to player
			//drain.Source.Owner.ResourceHolder.AddWood(actualDrain);
		}
		OnAmountChanged?.Invoke(CurrentAmount);
	}

	public void StartDraining(unit source, float drainPerTick)
	{
		_activeDrains.Add(new ResourceDrainRequest(source, drainPerTick));
	}
	public void StopDraining(unit source)
	{
		_activeDrains.RemoveAll(d => d.Source == source);
	}

}

public class ResourceDrainRequest
{
	public unit Source { get; }
	public float DrainPerTick { get; }

	public ResourceDrainRequest(unit source, float drainPerTick)
	{
		Source = source;
		DrainPerTick = drainPerTick;
	}
}
