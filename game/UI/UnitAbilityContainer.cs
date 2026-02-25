using Godot;
using System;

public partial class UnitAbilityContainer : PanelContainer
{
	[Signal]
	public delegate void AbilitySelectedEventHandler(unit unit, Ability ability);

	[Export] public PackedScene AbilityEntryScene;

	private Control _unitListContainer;

	public override void _Ready()
	{
		_unitListContainer = GetNode<Control>("HBoxContainer");
	}

	public void ShowAbilities(unit unit,Godot.Collections.Array<Ability> abilities)
	{
		//_unitListContainer.QueueFreeChildren();
		foreach (var child in _unitListContainer.GetChildren())
			child.QueueFree();

		if(abilities == null) return;

		foreach (var ability in abilities)
		{
			var entry = AbilityEntryScene.Instantiate<AbilityEntry>();
			entry.Setup(unit, ability);
			// Forward child signal
			entry.AbilitySelected += OnAbilityEntrySelected;

			// Hover signals
			
			_unitListContainer.AddChild(entry);
		}
	}
	private void OnAbilityEntrySelected(unit unit, Ability ability)
	{
		EmitSignal(SignalName.AbilitySelected, unit, ability);
	}

}

