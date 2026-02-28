using Godot;
using System;
using System.Collections.Generic;

public partial class ResourcesHolder : Node2D
{
	public ResourceInstance wood;
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

	public List<ResourceInstance> Resources
	{
		get
		{
			var list = new List<ResourceInstance>();
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
			wood = new ResourceInstance(ResourceType.Wood, amount/2, amount, regenRate);
	}

	private void setGold(float amount, float regenRate)
	{
		if (gold == null)
			gold = new ResourceInstance(ResourceType.Gold, amount/2, amount, regenRate);
	}
	public void stopDrainingResource(ResourceType type, unit source)
	{
		ResourceInstance resource = GetResourceByType(type);
		if (resource != null)
		{
			resource.StopDraining(source);
		}
	}

	
}
