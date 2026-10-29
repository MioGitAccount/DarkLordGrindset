using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public enum QuestStageKind
{
	Roll,   // a weighted attribute mix against a difficulty, rolled after Duration seconds
	Battle, // enemies appear; the player opens the battle with the quest's agents, winning passes
}

// What a failed stage does to the rest of the quest
public enum QuestStageFailure
{
	EndQuest,        // the quest is failed
	Retry,           // try the stage again after RetryDelay seconds
	InjureContinue,  // go on to the next stage anyway (the reward is kept)
}

// One step of a quest, e.g. "Lift the ring: QUI, difficulty 5". See QuestDefinition.
[GlobalClass]
public partial class QuestStage : Resource
{
	// A stage passes at 75% when the score equals the difficulty, +10% per point above it (100% at
	// +3) and -15% per point below it, never under 5%
	public const float BaseChance = 0.75f;
	public const float ChancePerPointAbove = 0.1f;
	public const float ChancePerPointBelow = 0.15f;
	public const float MinChance = 0.05f;
	public const float MaxChance = 1f;

	[Export] public string Name = "Stage";
	[Export(PropertyHint.MultilineText)] public string Description = "";
	[Export] public QuestStageKind Kind = QuestStageKind.Roll;
	// Seconds of work before the roll (Roll stages)
	[Export] public int Duration = 30;

	// Roll stages: score = sum of attribute x weight, compared with Difficulty
	[Export] public float WeightDeadliness;
	[Export] public float WeightStrength;
	[Export] public float WeightDurability;
	[Export] public float WeightCleverness;
	[Export] public float WeightQuickness;
	[Export(PropertyHint.Range, "0,10,0.5")] public float Difficulty = 5;

	// Battle stages: who the agents fight (they don't join the field's own neutrals)
	[Export] public Godot.Collections.Array<UnitTemplate> Enemies = new();

	[Export] public QuestStageFailure OnFail = QuestStageFailure.EndQuest;
	[Export] public int RetryDelay = 20;
	// Damage the agent who rolled takes on a failure (never kills); DeathChance can kill outright
	[Export] public int FailDamage;
	[Export(PropertyHint.Range, "0,1,0.05")] public float DeathChance;

	// Weight of attribute `index` in pentagon order (0 = Deadliness ... 4 = Quickness)
	public float Weight(int index) => index switch
	{
		0 => WeightDeadliness,
		1 => WeightStrength,
		2 => WeightDurability,
		3 => WeightCleverness,
		4 => WeightQuickness,
		_ => throw new ArgumentOutOfRangeException(nameof(index))
	};

	public float Score(SpecialAttributes attributes)
	{
		if (attributes == null)
			return 0;
		float score = 0;
		for (int i = 0; i < SpecialAttributes.Count; i++)
			score += attributes.Get(i) * Weight(i);
		return score;
	}

	public float Chance(SpecialAttributes attributes)
	{
		float margin = Score(attributes) - Difficulty;
		float chance = BaseChance + margin * (margin >= 0 ? ChancePerPointAbove : ChancePerPointBelow);
		return Math.Clamp(chance, MinChance, MaxChance);
	}

	// e.g. "DEA x0.7 + QUI x0.3 vs 6"
	public string CheckText()
	{
		if (Kind == QuestStageKind.Battle)
			return "Battle";
		var parts = new List<string>();
		for (int i = 0; i < SpecialAttributes.Count; i++)
		{
			float weight = Weight(i);
			if (weight != 0)
				parts.Add(Mathf.IsEqualApprox(weight, 1) ? SpecialAttributes.ShortNames[i]
					: $"{SpecialAttributes.ShortNames[i]} x{weight.ToString("0.##", CultureInfo.InvariantCulture)}");
		}
		return $"{string.Join(" + ", parts)} vs {Difficulty.ToString("0.#", CultureInfo.InvariantCulture)}";
	}

	// e.g. "fail: retry in 20s, 4 damage"
	public string FailureText()
	{
		var parts = new List<string>
		{
			OnFail switch
			{
				QuestStageFailure.Retry => $"retry in {RetryDelay}s",
				QuestStageFailure.InjureContinue => "go on, reward kept",
				_ => "quest fails",
			}
		};
		if (FailDamage > 0)
			parts.Add($"{FailDamage} damage");
		if (DeathChance > 0)
			parts.Add($"{DeathChance * 100:0}% death");
		return "If failed: " + string.Join(", ", parts);
	}
}
