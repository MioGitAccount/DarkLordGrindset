using Godot;
using System;

// Runs one ticking ability at a time for its unit
public partial class AbilityRunner : Node
{
	// Units learn by doing: every tick of work done by an ability earns experience
	[Export] public float ExperiencePerTick = 5f / 3f;

	private TickingAbility _ability;
	private AbilityExecutionContext _context;
	private float _timeAccumulator;
	// Fractions of experience not handed out yet, so small per-tick amounts add up exactly
	private float _experienceCarry;

	public TickingAbility ActiveAbility => _ability;

	public bool StartAbility(TickingAbility ability, AbilityExecutionContext context)
	{
		StopAbility();
		if (!ability.Start(context))
			return false;
		_ability = ability;
		_context = context;
		_timeAccumulator = 0f;
		return true;
	}

	public void StopAbility()
	{
		_ability?.Stop(_context);
		_ability = null;
		_context = null;
	}

	public override void _Process(double delta)
	{
		if (_ability == null)
			return;

		_timeAccumulator += (float)delta;
		if (_timeAccumulator >= 1f)
		{
			_timeAccumulator -= 1f;
			var context = _context;
			if (_ability.OnTick(context))
				GiveExperience(context.Source);
			else if (_context == context)
				StopAbility();
		}
	}

	private void GiveExperience(unit source)
	{
		if (source == null)
			return;
		_experienceCarry += ExperiencePerTick * source.ExperienceMultiplier(_ability);
		int whole = (int)_experienceCarry;
		_experienceCarry -= whole;
		if (whole > 0)
			source.AddExperience(whole);
	}
}
