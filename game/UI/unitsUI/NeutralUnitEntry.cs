using Godot;
using System;

public partial class NeutralUnitEntry : Panel
{
	public NeutralUnit BoundUnit { get; private set; }

	public event Action<NeutralUnit> Hovered;
	public event Action HoverExited;

	public override void _Ready()
	{
		// Hover signals (built-in, reliable)
		MouseEntered += OnMouseEntered;
		MouseExited += OnMouseExited;
	}

	public void Setup(NeutralUnit neutralUnit)
	{
		BoundUnit = neutralUnit;
		GetNode<TextureRect>("IconRect").Texture = neutralUnit.Template?.Icon;
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
