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
	public int GetInitiative() { return battleStats.initiative; }
	public bool isRangeUnit() { return battleStats.rangeUnit; }

	private ProgressBar healthBar;
	private Label damangeLabel;
	private Godot.Sprite2D unitImage;

	public override void _Ready()
	{
		healthBar = GetNode<ProgressBar>("VBoxContainer/ProgressBar");
		healthBar.Value = battleStats.getCurrentHeathInProcent();
		damangeLabel = GetNode<Label>("VBoxContainer/Control/DamangeLabel");
		unitImage = GetNode<Godot.Sprite2D>("VBoxContainer/Control/Sprite2D");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public int AttackAction(BattleUnit attackedUnit)
	{
		Random rand = new Random();
		int damange = rand.Next(this.battleStats.attack/2, this.battleStats.attack+1) - rand.Next(0,attackedUnit.battleStats.defence+1);
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
			UpdateVisualState();
		}
		healthBar.Value = battleStats.getCurrentHeathInProcent();
		damangeLabel.Text = damange.ToString();
	}
	
	public void resetDamange()
	{
		damangeLabel.Text = "";
	}

	public void UpdateVisualState()
	{
		if (alive)
			unitImage.Modulate = new Color(1, 1, 1, 1); // resurected
		else
			unitImage.Modulate = new Color(0.5f, 0.5f, 0.5f, 0.5f); // died
	}

}
