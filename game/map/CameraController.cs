using Godot;
using System;

public partial class CameraController : Camera2D
{
[Export] public float Speed = 400f;
	private Vector2 velocity = Vector2.Zero;
	public override void _Process(double delta)
	{
		velocity = Vector2.Zero;

		if (Input.IsActionPressed("ui_up")) velocity.Y -= 1;
		if (Input.IsActionPressed("ui_down")) velocity.Y += 1;
		if (Input.IsActionPressed("ui_left")) velocity.X -= 1;
		if (Input.IsActionPressed("ui_right")) velocity.X += 1;

		if (velocity.Length() > 0)
		{
			velocity = velocity.Normalized() * Speed * (float)delta;
			Position += velocity;
			
		}
		
	}
}
