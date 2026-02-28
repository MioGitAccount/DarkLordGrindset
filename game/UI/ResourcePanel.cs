using Godot;
using System;
using System.Security.AccessControl;

public partial class ResourcePanel : Panel
{
	public ResourceInstance BoundResource { get; private set; }

	public override void _Ready()
	{

	}

	public void Setup(ResourceInstance resource)
	{
		BoundResource = resource;
		GetNode<TextureRect>("TextureRect").Texture = FindIcon(resource.type);
		GetNode<Label>("Label").Text = resource.CurrentAmount.ToString();
		BoundResource.OnAmountChanged += UpdateAmount;
	}

	public void UpdateAmount(float newAmount)
	{
		GetNode<Label>("Label").Text = newAmount.ToString();
	}

	private Texture2D FindIcon(ResourceType type)
	{
		switch (type)
		{
			case ResourceType.Wood:
				return GD.Load<Texture2D>("res://images/resources/wood.png");
			case ResourceType.Gold:
				return GD.Load<Texture2D>("res://images/resources/gold.png");
			default:
				return null;
		}
	}
	public override void _ExitTree()
	{
		if (BoundResource != null)
			BoundResource.OnAmountChanged -= UpdateAmount;
	}
	
	



}
