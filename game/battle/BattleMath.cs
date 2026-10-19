using System;

// Damage rules. A hit deals Attack minus a defence roll, never below 0.
// The defence roll goes from 0 to Defence with a triangular distribution: for Defence 6 it is
// one pick from (0,1,1,2,2,2,3,3,3,3,4,4,4,5,5,6), so the middle values come up most often.
public static class BattleMath
{
	// How many times `value` appears in the roll table of `defence` (1 at the ends, most in the middle)
	public static int DefenceWeight(int defence, int value) => Math.Min(value, defence - value) + 1;

	public static int RollDefence(int defence, Random rng)
	{
		if (defence <= 0)
			return 0;
		int total = 0;
		for (int value = 0; value <= defence; value++)
			total += DefenceWeight(defence, value);
		int pick = rng.Next(total);
		for (int value = 0; value <= defence; value++)
		{
			pick -= DefenceWeight(defence, value);
			if (pick < 0)
				return value;
		}
		return defence;
	}

	public static int Damage(int attack, int defenceRoll) => Math.Max(0, attack - defenceRoll);

	// Average damage of `attack` against `defence`, for tooltips
	public static float AverageDamage(int attack, int defence)
	{
		if (defence <= 0)
			return Math.Max(0, attack);
		float total = 0, sum = 0;
		for (int value = 0; value <= defence; value++)
		{
			int weight = DefenceWeight(defence, value);
			total += weight;
			sum += weight * Damage(attack, value);
		}
		return sum / total;
	}
}
