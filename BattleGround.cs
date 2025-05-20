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
		bool battleFinished = false;
		while (!battleFinished)
		{
			foreach (BattleUnit unit in allBattleUnits)
			{
				if (unit.isAlive())
				{
					if (unit.isLeftSide())
					{
						BattleUnit attackedUnit = PickUnitToAttack(1,battleUnitsRight);
						if(attackedUnit is null)
						{
							battleFinished = true;
							break;
						}
						unit.Position += new Vector2(100, 0);
						int damange = unit.AttackAction(attackedUnit);
						attackedUnit.takeDamange(damange);
						await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
						unit.Position -= new Vector2(100, 0);
						attackedUnit.resetDamange();

					}
					else
					{
						BattleUnit attackedUnit = PickUnitToAttack(1,battleUnitsLeft);
						if(attackedUnit is null)
						{
							battleFinished = true;
							break;
						}
						unit.Position += new Vector2(-100, 0);
						int damange = unit.AttackAction(attackedUnit);
						attackedUnit.takeDamange(damange);
						await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
						unit.Position -= new Vector2(-100, 0);
						attackedUnit.resetDamange();

					}
					await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
				}
			}

		}
	}
	public BattleUnit PickUnitToAttack(int position, List<BattleUnit> enemyUnits)
	{
		foreach (BattleUnit unit in enemyUnits)
		{
			if(unit is null) continue;
			if (unit.isAlive()) return unit;
		}
		// switch(position)
		// {
		// 	case 1:
		// 	case 3:
		// 		if(enemyUnits[0] != null && enemyUnits[0].isAlive())
		// 		{
		// 			if (enemyUnits[2])
		// 		}
		// 		break;
		// }
		return null;
	}
	public void LoadUnits()
	{
		for (int i=0; i<5; i++)
		{
			battleUnitsLeft.Add(null);
			battleUnitsRight.Add(null);
		}
		Node nodeParent = GetNode<Node>("BattleUnitListLeft");
		List<Node> listOfNodes = new List<Node>(nodeParent.GetChildren());
		foreach (Node node in listOfNodes)
		{
			if (node is BattleUnit)
			{
				BattleUnit unit = (BattleUnit)node;
				battleUnitsLeft[unit.BattlePosition()-1] = unit;
				allBattleUnits.Add(unit);
			}
		}
		Node nodeParentR = GetNode<Node>("BattleUnitListRight");
		List<Node> listOfNodesR = new List<Node>(nodeParentR.GetChildren());
		foreach (Node node in listOfNodesR)
		{
				if (node is BattleUnit)
				{
					BattleUnit unit = (BattleUnit)node;
					battleUnitsRight[unit.BattlePosition()-1] = unit;
					allBattleUnits.Add(unit);
				}
		}
	}
}
