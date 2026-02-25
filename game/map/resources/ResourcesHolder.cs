using Godot;
using System;

public partial class ResourcesHolder : Node2D
{
	[Export]
	public ResourceInstance wood;

	[Export]
	public ResourceInstance gold;
	
	
	// Every node or unit will have deferent resources on start
	public override void _Ready()
	{

	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void setResourcesBasedOnType(FieldTypeEnum typeEnum)
	{
		switch (typeEnum)
		{
			case FieldTypeEnum.AutumnForest:
				setWood(300, 1);
				break;
			case FieldTypeEnum.Village:
				setGold(1000, 2);
				break;
			default:
				break;
		}
	}

	public Godot.Collections.Array<ResourceInstance> Resources
	{
		get
		{
			var list = new Godot.Collections.Array<ResourceInstance>();
			if (wood != null) list.Add(wood);
			if (gold != null) list.Add(gold);
			return list;
		}
	}

	public float tryDrainResource(ResourceType type, unit source, float drainPerTick)
	{
		ResourceInstance resource = GetResourceByType(type);
		if (resource != null)
		{
			resource.StartDraining(source, drainPerTick);
			return resource.CurrentAmount;
		}
		return 0;
	}

	private ResourceInstance GetResourceByType(ResourceType type)
	{
		switch (type)
		{
			case ResourceType.Wood:
				return wood;
			case ResourceType.Gold:
				return gold;
			default:
				return null;
		}
	}
	private void setWood(float amount, float regenRate)
	{
		if (wood == null)
			wood = new ResourceInstance();
		wood.CurrentAmount = amount/2;
		wood.MaxAmount = amount;
		wood.RegenRate = regenRate;
		wood.type = ResourceType.Wood;
		AddChild(wood);
	}

	private void setGold(float amount, float regenRate)
	{
		if (gold == null)
			gold = new ResourceInstance();
		gold.CurrentAmount = amount/2;
		gold.MaxAmount = amount;
		gold.RegenRate = regenRate;
		gold.type = ResourceType.Gold;
		AddChild(gold);
	}

	
}
