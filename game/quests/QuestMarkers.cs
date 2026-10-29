using Godot;
using System.Linq;

// "!" badges next to fields with quests, drawn above the fog, with the quest's level in the corner:
// gold = available, blue = in progress, red (pulsing) = expiring soon or a fight is waiting.
// Clicking one opens its quest (see map).
public partial class QuestMarkers : Node2D
{
	private const float Radius = 8f;
	private static readonly Vector2 Offset = new Vector2(15, -15);

	private static readonly Color AvailableColor = new Color(1f, 0.78f, 0.3f);
	private static readonly Color ActiveColor = new Color(0.45f, 0.7f, 1f);
	private static readonly Color UrgentColor = new Color(1f, 0.35f, 0.3f);
	private static readonly Color Outline = new Color(0.08f, 0.06f, 0.06f, 0.9f);

	private QuestManager _quests;
	private float _time;

	public void Setup(QuestManager quests)
	{
		_quests = quests;
		_quests.QuestAdded += _ => QueueRedraw();
		_quests.QuestRemoved += _ => QueueRedraw();
		_quests.QuestChanged += _ => QueueRedraw();
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;
		if (_quests != null && _quests.Quests.Any(IsUrgent))
			QueueRedraw(); // pulse
	}

	private static bool IsUrgent(Quest quest) => quest.IsExpiringSoon || quest.State == QuestState.WaitingForBattle;

	// The quest whose marker is under `point` (map coordinates), or null
	public Quest QuestAt(Vector2 point)
	{
		if (_quests == null)
			return null;
		foreach (var quest in _quests.Quests)
			if (GodotObject.IsInstanceValid(quest.Field) && (quest.Field.Position + Offset).DistanceTo(point) <= Radius + 3)
				return quest;
		return null;
	}

	public override void _Draw()
	{
		if (_quests == null)
			return;
		var font = ThemeDB.FallbackFont;
		foreach (var quest in _quests.Quests)
		{
			if (!GodotObject.IsInstanceValid(quest.Field))
				continue;
			Vector2 at = quest.Field.Position + Offset;
			Color color = IsUrgent(quest) ? UrgentColor : quest.IsStarted ? ActiveColor : AvailableColor;
			float r = Radius;
			if (IsUrgent(quest))
				r += 1.5f * Mathf.Sin(_time * 6f);
			DrawCircle(at, r + 1.5f, Outline);
			DrawCircle(at, r, color);
			string text = quest.IsStarted && quest.State != QuestState.WaitingForBattle ? "..." : "!";
			const int size = 13;
			Vector2 textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
			DrawString(font, at + new Vector2(-textSize.X / 2, size * 0.35f), text, HorizontalAlignment.Left, -1, size, Outline);
			// Quest level in the corner, like the threat level on hostile icons
			const int levelSize = 9;
			DrawStringOutline(font, at + new Vector2(Radius - 2, Radius + 3), quest.Definition.Level.ToString(), HorizontalAlignment.Left, -1, levelSize, 3, Outline);
			DrawString(font, at + new Vector2(Radius - 2, Radius + 3), quest.Definition.Level.ToString(), HorizontalAlignment.Left, -1, levelSize, color);
		}
	}
}
