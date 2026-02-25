using Godot;
using System;

public partial class AbilityRunner : Node
{
	private TickingAbility _ability;
	private AbilityExecutionContext _context;
	private float _timeAccumulator;

	public void StartAbility(
		TickingAbility ability,
		AbilityExecutionContext context
	)
	{
		_ability = ability;
		_context = context;
		_ability.Start(context);
		_timeAccumulator = 0f;
	}

	public void StopAbility()
	{
		_ability?.Stop();
		_ability = null;
	}

	public override void _Process(double delta)
	{
		if (_ability == null || !_ability.IsActive)
			return;

		_timeAccumulator += (float)delta;
		//_context.Stats.TickInterval = 1f;
		if (_timeAccumulator >= 1f)
		{
			_timeAccumulator -= 1f;
			_ability.OnTick(_context);
		}
	}
}
