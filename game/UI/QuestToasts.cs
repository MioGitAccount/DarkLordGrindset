using Godot;
using System;

// Short quest messages under the top bar ("New quest in Village: Steal the Ring", "Imp took 4
// damage", ...). Each fades out after a few seconds; clicking one opens its quest.
public partial class QuestToasts : VBoxContainer
{
	private const double ShowSeconds = 6;
	private const double FadeSeconds = 1;
	private const int MaxToasts = 5;

	public event Action<Quest> QuestClicked;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		AddThemeConstantOverride("separation", 4);
		Position = new Vector2(8, 60);
	}

	public void AddToast(Quest quest, string message)
	{
		var toast = new Button
		{
			Text = message,
			FocusMode = FocusModeEnum.None,
			Alignment = HorizontalAlignment.Left,
			TooltipText = quest.IsOpen ? "Click to open the quest" : "",
		};
		toast.Pressed += () =>
		{
			if (quest.IsOpen)
				QuestClicked?.Invoke(quest);
			toast.QueueFree();
		};
		AddChild(toast);
		while (GetChildCount() > MaxToasts)
		{
			var oldest = GetChild(0);
			RemoveChild(oldest);
			oldest.QueueFree();
		}
		var tween = toast.CreateTween();
		tween.TweenInterval(ShowSeconds);
		tween.TweenProperty(toast, "modulate:a", 0f, FadeSeconds);
		tween.TweenCallback(Callable.From(toast.QueueFree));
		ResetSize();
	}
}
