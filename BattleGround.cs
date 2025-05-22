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
						BattleUnit attackedUnit = PickUnitToAttack(unit.BattlePosition(),battleUnitsRight);
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
						BattleUnit attackedUnit = PickUnitToAttack(unit.BattlePosition(),battleUnitsLeft);
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
		// foreach (BattleUnit unit in enemyUnits)
		// {
		// 	if(unit is null) continue;
		// 	if (unit.isAlive()) return unit;
		// }
		switch(position)
		{
			case 1:
			case 3:
				// these are units on top
				// check if 1. in front line is alive, cause then you cant attack 1. in back line
				if (enemyUnits[2] != null && enemyUnits[2].isAlive())
				{
					// check if you can attack 2. in back line
					if (enemyUnits[1] != null && enemyUnits[1].isAlive())
					{
						// its alive, but you must check 2. and 3. in front
						if ((enemyUnits[3] == null || !enemyUnits[3].isAlive()) && (enemyUnits[4] == null || !enemyUnits[4].isAlive()))
						{
							// attack 2. in back
							return enemyUnits[1];
						}
					}
					// if you cant attack 2. in back, then attack 1. in front
					return enemyUnits[2];
				}
				// check if 2. in front line is alive, cause then you cant attack 1. and 2. in back line
				if (enemyUnits[3] != null && enemyUnits[3].isAlive())
				{
					// attack 2. in front
					return enemyUnits[3];
				}
				// cause there are no 1. and 2. in front line, you can attack 1. in back
				if (enemyUnits[0] != null && enemyUnits[0].isAlive())
				{
					// attack 1. in back
					return enemyUnits[0];
				}
				// check if 3. in front is alive
				if (enemyUnits[4] != null && enemyUnits[4].isAlive())
				{
					// attack 3. in front
					return enemyUnits[4];
				}
				// check if 2. in back is alive
				if (enemyUnits[1] != null && enemyUnits[1].isAlive())
				{
					// attack 2. in back
					return enemyUnits[1];
				}
				// there is no one to attack
				break;
			case 2:
			case 5:
				// these are units on bottom
				// check if 3. in front line is alive, cause then you cant attack 2. in back line
				if (enemyUnits[4] != null && enemyUnits[4].isAlive())
				{
					// check if you can attack 1. in back line
					if (enemyUnits[0] != null && enemyUnits[0].isAlive())
					{
						// its alive, but you must check 1. and 2. in front
						if ((enemyUnits[3] == null || !enemyUnits[3].isAlive()) && (enemyUnits[2] == null || !enemyUnits[2].isAlive()))
						{
							// attack 1. in back
							return enemyUnits[0];
						}
					}
					// if you cant attack 1. in back, then attack 3. in front
					return enemyUnits[4];
				}
				// check if 2. in front line is alive, cause then you cant attack 1. and 2. in back line
				if (enemyUnits[3] != null && enemyUnits[3].isAlive())
				{
					// attack 2. in front
					return enemyUnits[3];
				}
				// cause there are no 2. and 3. in front line, you can attack 2. in back
				if (enemyUnits[1] != null && enemyUnits[1].isAlive())
				{
					// attack 2. in back
					return enemyUnits[1];
				}
				// check if 1. in front is alive
				if (enemyUnits[2] != null && enemyUnits[2].isAlive())
				{
					// attack 1. in front
					return enemyUnits[2];
				}
				// check if 1. in back is alive
				if (enemyUnits[0] != null && enemyUnits[0].isAlive())
				{
					// attack 1. in back
					return enemyUnits[0];
				}
				// there is no one to attack
				break;
			case 4:
				// this is unit in middle
				// check if 2. in front line is alive
				if (enemyUnits[3] != null && enemyUnits[3].isAlive())
				{
					// attack 2. in front
					return enemyUnits[3];
				}
				// check if 3. in front line is alive, cause then you cant attack 2. in back line
				if (enemyUnits[4] != null && enemyUnits[4].isAlive())
				{
					// check if you can attack 1. in back line
					if (enemyUnits[0] != null && enemyUnits[0].isAlive())
					{
						// its alive, but you must check 1. in front
						if (enemyUnits[2] == null || !enemyUnits[2].isAlive())
						{
							// attack 1. in back
							return enemyUnits[0];
						}
					}
					// if you cant attack 1. in back, then attack 3. in front
					return enemyUnits[4];
				}
				// cause there are no 2. and 3. in front line, you can attack 2. in back
				if (enemyUnits[1] != null && enemyUnits[1].isAlive())
				{
					// attack 2. in back
					return enemyUnits[1];
				}
				// check if 1. in front is alive
				if (enemyUnits[2] != null && enemyUnits[2].isAlive())
				{
					// attack 1. in front
					return enemyUnits[2];
				}
				// check if 1. in back is alive
				if (enemyUnits[0] != null && enemyUnits[0].isAlive())
				{
					// attack 1. in back
					return enemyUnits[0];
				}
				// there is no one to attack
				break;
		}
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
		// sort units by initiative
		allBattleUnits.Sort((a, b) => b.GetInitiative().CompareTo(a.GetInitiative()));
	}
}
