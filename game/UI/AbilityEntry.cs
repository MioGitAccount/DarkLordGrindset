using Godot;
using System;

public partial class AbilityEntry : Panel
{
	public unit BoundUnit { get; private set; }
	public Ability BoundAbility { get; private set; }
	[Signal]
	public delegate void AbilitySelectedEventHandler(unit unit, Ability ability);

	public void Setup(unit unit, Ability ability)
	{
		BoundUnit = unit;
		BoundAbility = ability;
		GetNode<TextureRect>("IconRect").Texture = ability?.Icon;
	}

	public override void _GuiInput(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouseButton mouseEvent && mouseEvent.Pressed &&
			mouseEvent.ButtonIndex == MouseButton.Left)
		{
			BoundUnit.abilityRunner.StartAbility((TickingAbility)BoundAbility, new AbilityExecutionContext
			{
				Source = BoundUnit,
				Ability = BoundAbility,
				Stats = BoundAbility.BaseStats
			});
			GD.Print($"Ability {BoundAbility?.Name} clicked");
			EmitSignal(SignalName.AbilitySelected, BoundUnit, BoundAbility);
		}

	}
}
