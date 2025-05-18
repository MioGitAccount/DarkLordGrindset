using Godot;
using System;

public partial class ResourcesHolder : Node2D
{
	[Export] public string TypeName;

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
		//get parent to decide stats
		Node parent = GetParent();
		if(parent == null)
		 return;
		if(parent is field)
		{
			maxGold = 500;
			maxWood = 500;
			maxOre = 500;
			maxIronOre = 500;

		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
