using Godot;
using System;

public partial class UnitEntry : Panel
{
	public unit BoundUnit { get; private set; }

	[Signal]
	public delegate void UnitSelectedEventHandler(unit unit);

	public event Action<unit> Hovered;
	public event Action HoverExited;

	public override void _Ready()
	{
		// Hover signals (built-in, reliable)
		MouseEntered += OnMouseEntered;
		MouseExited += OnMouseExited;
	}

	public void Setup(unit unit)
	{
		BoundUnit = unit;
		GetNode<TextureRect>("IconRect").Texture = unit.Template?.Icon;
	}

	public override void _GuiInput(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouseButton mouseEvent && mouseEvent.Pressed &&
			mouseEvent.ButtonIndex == MouseButton.Left)
		{
			GD.Print($"Unit {BoundUnit?.Name} clicked");
			BoundUnit.Select();
			EmitSignal(SignalName.UnitSelected, BoundUnit);
		}

	}
	private void OnMouseEntered()
	{
		if (BoundUnit == null)
			return;

		Hovered?.Invoke(BoundUnit);
	}

	private void OnMouseExited()
	{
		HoverExited?.Invoke();
	}

}
