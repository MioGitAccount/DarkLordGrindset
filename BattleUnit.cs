using Godot;
using System;
using System.Net.Http;
using System.Threading.Tasks;

public partial class BattleUnit : Node2D
{
	// Called when the node enters the scene tree for the first time.
	BattleStats battleStats;
	[Export] public int battlePosition;
	[Export] public bool leftSide;
	[Export] public bool alive;
	public bool isLeftSide() { return leftSide; }
	public bool isAlive() { return alive; }

	public override void _Ready()
	{
		battleStats = new BattleStats(5, 2, 15);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public int AttackAction(BattleUnit attackedUnit)
	{
		int damange = this.battleStats.attack - attackedUnit.battleStats.defence;
		if (damange < 0) damange = 0;
		return damange;


	}
	public bool takeDamange(int damange)
	{
		this.battleStats.health -= damange;
		GD.Print("Unit takes " + damange + " damange");
		if (this.battleStats.health <= 0)
		{
			this.battleStats.health = 0;
			return true;
		}
		return false;
	}

}
