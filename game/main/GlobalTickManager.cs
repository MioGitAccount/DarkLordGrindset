using Godot;
using System;
using System.Threading.Tasks.Dataflow;

public partial class GlobalTickManager : Node
{
	public static GlobalTickManager Instance;
	public event Action OnTick;

	private float _timer;

	// Autoload; a second copy would take over Instance, and things subscribed to the first would never be unsubscribed
	public override void _Ready()
	{
		if (IsInstanceValid(Instance) && Instance != this)
		{
			GD.PushWarning($"Extra GlobalTickManager at {GetPath()} removed; it is an autoload");
			QueueFree();
			return;
		}
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
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
