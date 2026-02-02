using Godot;
using System;

public partial class UnitStatsPanel : Panel
{
	[Export] private Label NameLabel;
	[Export] private Label HpLabel;
	[Export] private Label AttackLabel;
	[Export] private Label DefenseLabel;

	public override void _Ready()
	{
		Visible = false;
	}
	
	public override void _Process(double delta)
	{
		GlobalPosition = GetViewport().GetMousePosition() + new Vector2(16, 16);
	}

	public void ShowStats(BattleStats stats, string unitName)
	{
		if (stats == null)
			return;

		NameLabel.Text = unitName;
		HpLabel.Text = $"HP: {stats.health}";
		AttackLabel.Text = $"ATK: {stats.attack}";
		DefenseLabel.Text = $"DEF: {stats.defence}";

		Visible = true;
	}

	public void HideStats()
	{
		Visible = false;
	}
}
