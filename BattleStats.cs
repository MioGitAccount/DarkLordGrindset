using Godot;
using System;

public class BattleStats
{
	public int attack { get; set; }
	public int defence { get; set; }
	public int health { get; set; }

	public BattleStats(int Attack, int Defence, int Health)
	{
		this.attack = Attack;
		this.defence = Defence;
		this.health = Health;
	}
}
