using Godot;
using System;
using System.Threading.Tasks.Dataflow;

public partial class GlobalTickManager : Node
{
	public static GlobalTickManager Instance;
	public event Action OnTick;

	private float _timer;

	public override void _Ready()
	{
		Instance = this;
	}
	
	public override void _Process(double delta)
	{
		_timer += (float)delta;
		if (_timer >= 1f)
		{
			_timer -= 1f;
			OnTick?.Invoke();
		}
	}


}
