using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BattleGround : Node2D
{
	// Called when the node enters the scene tree for the first time.
	public List<BattleUnit> battleUnitsLeft = new List<BattleUnit>();
	public List<BattleUnit> battleUnitsRight = new List<BattleUnit>();
	public List<BattleUnit> allBattleUnits = new List<BattleUnit>();
	public override void _Ready()
	{
		LoadUnits();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}


	private async void DelayMethod()
	{
		await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
	}
	public async void Battle()
	{
		//BATTLE LOOP
		while (true)
		{
			foreach (BattleUnit unit in allBattleUnits)
			{
				if (unit.isAlive())
				{
					if (unit.isLeftSide())
					{
						BattleUnit attackedUnit = battleUnitsRight[0];
						unit.Position += new Vector2(100, 0);
						int damange = unit.AttackAction(attackedUnit);
						attackedUnit.takeDamange(damange);
						await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
						unit.Position -= new Vector2(100, 0);
						attackedUnit.resetDamange();

					}
					else
					{
						BattleUnit attackedUnit = battleUnitsLeft[0];
						unit.Position += new Vector2(100, 0);
						int damange = unit.AttackAction(attackedUnit);
						attackedUnit.takeDamange(damange);
						await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
						unit.Position -= new Vector2(100, 0);
						attackedUnit.resetDamange();

					}
					await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
				}
			}

		}
	}
	public void LoadUnits()
	{
		Node nodeParent = GetNode<Node>("BattleUnitListLeft");
		List<Node> listOfNodes = new List<Node>(nodeParent.GetChildren());
		foreach (Node node in listOfNodes)
		{
			if (node is BattleUnit)
			{
				battleUnitsLeft.Add((BattleUnit)node);
				allBattleUnits.Add((BattleUnit)node);
			}
		}
		Node nodeParentR = GetNode<Node>("BattleUnitListRight");
		List<Node> listOfNodesR = new List<Node>(nodeParentR.GetChildren());
		foreach (Node node in listOfNodesR)
		{
				if (node is BattleUnit)
				{
					battleUnitsRight.Add((BattleUnit)node);
					allBattleUnits.Add((BattleUnit)node);
				}
		}
	}
}
