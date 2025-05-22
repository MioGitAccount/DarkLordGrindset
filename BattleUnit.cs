using Godot;
using System;
using System.Net.Http;
using System.Threading.Tasks;

public partial class BattleUnit : Node2D
{
	// Called when the node enters the scene tree for the first time.
	[Export] BattleStats battleStats;
	[Export] public int battlePosition;
	[Export] public bool leftSide;
	[Export] public bool alive;
	public bool isLeftSide() { return leftSide; }
	public bool isAlive() { return alive; }
	public int BattlePosition() { return battlePosition; }

	private ProgressBar healthBar;
	private Label damangeLabel;

	public override void _Ready()
	{
		healthBar = GetNode<ProgressBar>("VBoxContainer/ProgressBar");
		if(alive)
		healthBar.Value = battleStats.getCurrentHeathInProcent();
		damangeLabel = GetNode<Label>("VBoxContainer/Control/DamangeLabel");
		
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
	public void takeDamange(int damange)
	{
		this.battleStats.health -= damange;
		if (this.battleStats.health <= 0)
		{
			this.battleStats.health = 0;
			alive = false;
		}
		healthBar.Value = battleStats.getCurrentHeathInProcent();
		damangeLabel.Text = damange.ToString();
		GD.Print("Current health: " + battleStats.getCurrentHeathInProcent());
	}
	
	public void resetDamange()
	{
		damangeLabel.Text = "";
	}

}
