using System;
using System.Collections.Generic;
using System.Linq;

public enum SkillStatus
{
	Unlocked,
	Available,     // can be bought right now
	NoPoints,      // requirements met, but not enough skill points
	NeedsLevel,    // unit level too low for the skill's tier
	NeedsSkills,   // a required skill is still locked
}

// Level, experience, skill points and unlocked skills of one unit
public class UnitProgression
{
	public const int MaxLevel = 20;
	public const int SkillPointsPerLevel = 1;
	public const int StartingSkillPoints = 1; // so a new unit can pick its first skill right away

	// Experience needed to go from `level` to level + 1
	public static int ExperienceToNextLevel(int level) => 100 * level;

	public int Level { get; private set; } = 1;
	public int Experience { get; private set; } // towards the next level
	public int SkillPoints { get; private set; } = StartingSkillPoints;
	public bool IsMaxLevel => Level >= MaxLevel;

	private readonly HashSet<SkillDefinition> _unlocked = new();
	public IReadOnlyCollection<SkillDefinition> Unlocked => _unlocked;

	// Raised after anything here changes; the bool is true when skills changed (stats need recalculating)
	public event Action<bool> Changed;

	public void AddExperience(int amount)
	{
		if (amount <= 0 || IsMaxLevel)
			return;
		Experience += amount;
		while (!IsMaxLevel && Experience >= ExperienceToNextLevel(Level))
		{
			Experience -= ExperienceToNextLevel(Level);
			Level++;
			SkillPoints += SkillPointsPerLevel;
		}
		if (IsMaxLevel)
			Experience = 0;
		Changed?.Invoke(false);
	}

	// Levels the unit straight up to `level` (e.g. a recruit trained in the castle), with the skill points of every level
	public void LevelUpTo(int level)
	{
		while (Level < level && !IsMaxLevel)
			AddExperience(ExperienceToNextLevel(Level) - Experience);
	}

	public bool IsUnlocked(SkillDefinition skill) => skill != null && _unlocked.Contains(skill);

	public SkillStatus GetStatus(SkillDefinition skill)
	{
		if (IsUnlocked(skill))
			return SkillStatus.Unlocked;
		if (skill.Requires.Any(required => required != null && !IsUnlocked(required)))
			return SkillStatus.NeedsSkills;
		if (Level < skill.RequiredUnitLevel)
			return SkillStatus.NeedsLevel;
		if (SkillPoints < skill.Cost)
			return SkillStatus.NoPoints;
		return SkillStatus.Available;
	}

	public bool TryUnlock(SkillDefinition skill)
	{
		if (skill == null || GetStatus(skill) != SkillStatus.Available)
			return false;
		SkillPoints -= skill.Cost;
		_unlocked.Add(skill);
		Changed?.Invoke(true);
		return true;
	}
}
