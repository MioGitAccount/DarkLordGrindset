using Godot;
using System;

public partial class ResourcesHolder : Node2D
{
	[Export] public int gold;

	[Export] public int maxGold;

	[Export] public int wood;

	[Export] public int maxWood;

	[Export] public int ore;

	[Export] public int maxOre;


	[Export] public int ironOre;

	[Export] public int maxIronOre;
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
				setWood(300, 300);
				break;
			case FieldTypeEnum.Village:
				setGold(1000, 1000);
				break;
			default:
				break;
		}
	}
	private void setWood(int Wood, int MaxWood)
	{
		this.wood = Wood;
		this.maxWood = MaxWood;
	}
		private void setGold(int Gold, int MaxGold)
	{
		this.gold = Gold;
		this.maxGold = MaxGold;
	}
}
