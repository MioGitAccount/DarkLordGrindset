using Godot;
using System;

public partial class BattleUIControler : Control
{
	public override void _Ready()
	{
		var button = GetNode<Button>("HBoxContainer/AttackButton");
		button.Pressed += OnSpawnButtonPressed;
	}

	private void OnSpawnButtonPressed()
	{
		var battleGround = GetNode("..") as BattleGround;
		battleGround?.Battle();
	}
}
