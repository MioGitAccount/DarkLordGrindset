using Godot;
using System;

[GlobalClass]
public partial class BattleStats : Resource
{
	
	[Export] public int attack { get; set; }
	[Export] public int defence { get; set; }
	[Export] public int health { get; set; }
	[Export] public int maxHealth { get; set; }

 	public BattleStats() { }
	public BattleStats(int Attack, int Defence, int Health, int MaxHealth)
	{
		this.attack = Attack;
		this.defence = Defence;
		this.health = Health;
		this.maxHealth = MaxHealth;
	}
	public double getCurrentHeathInProcent()
	{
		return (double)health / maxHealth * 100;

	}
}
