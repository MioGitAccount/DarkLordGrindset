using Godot;

// Small card that floats above the field under the mouse: the biome name and the
// icons of the units on it (gold frame = yours, red frame = hostile). Fields in
// grey fog show the hostiles as they were last seen.
public partial class FieldHoverCard : PanelContainer
{
	private const int IconSize = 28;
	private const float GapAboveField = 22f;

	private static readonly StyleBoxFlat PlayerFrame = MakeFrame(new Color(0.85f, 0.66f, 0.3f));
	private static readonly StyleBoxFlat HostileFrame = MakeFrame(new Color(0.85f, 0.25f, 0.2f));

	private Label _title;
	private Label _emptyLabel;
	private HBoxContainer _icons;
	private Label _questsLabel;
	private field _field;
	// Set by UIManager; quests on the field are listed with their timers
	public QuestManager Quests;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 4);
		_title = new Label { ThemeTypeVariation = "MutedLabel", HorizontalAlignment = HorizontalAlignment.Center };
		_emptyLabel = new Label { Text = "No units", ThemeTypeVariation = "MutedLabel", HorizontalAlignment = HorizontalAlignment.Center };
		_icons = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
		_icons.AddThemeConstantOverride("separation", 3);
		box.AddChild(_title);
		box.AddChild(_icons);
		box.AddChild(_emptyLabel);
		_questsLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
		_questsLabel.AddThemeColorOverride("font_color", new Color(1f, 0.82f, 0.4f));
		box.AddChild(_questsLabel);
		AddChild(box);
		Visible = false;
	}

	public override void _ExitTree()
	{
		if (_field != null)
			_field.ContentsChanged -= Rebuild;
	}

	public void ShowFor(field hoveredField)
	{
		if (hoveredField == _field)
			return;
		if (_field != null)
			_field.ContentsChanged -= Rebuild;

		_field = hoveredField;
		Visible = _field != null;
		if (_field == null)
			return;

		_field.ContentsChanged += Rebuild;
		Rebuild();
	}

	public override void _Process(double delta)
	{
		if (_field == null || !IsInstanceValid(_field))
			return;

		// Follow the field on screen (the camera can move), centred above it and kept on screen
		Vector2 fieldOnScreen = _field.GetGlobalTransformWithCanvas().Origin;
		Vector2 pos = fieldOnScreen - new Vector2(Size.X / 2f, Size.Y + GapAboveField);
		Vector2 screen = GetViewportRect().Size;
		pos.X = Mathf.Clamp(pos.X, 4, screen.X - Size.X - 4);
		pos.Y = Mathf.Clamp(pos.Y, 4, screen.Y - Size.Y - 4);
		Position = pos;
		RefreshQuests();
	}

	private void RefreshQuests()
	{
		var lines = new System.Collections.Generic.List<string>();
		if (Quests != null)
			foreach (var quest in Quests.QuestsOn(_field))
				lines.Add($"! {quest.Title} (Lv {quest.Definition.Level})" + (quest.HasDeadline ? $"  {quest.TimeLeftText}" : "") + (quest.IsStarted ? "  (in progress)" : ""));
		string text = string.Join("\n", lines);
		if (text == _questsLabel.Text)
			return;
		_questsLabel.Text = text;
		_questsLabel.Visible = lines.Count > 0;
		ResetSize();
	}

	private void Rebuild()
	{
		bool visible = _field.Fog == FogState.Visible;
		_title.Text = (_field.type?.Name ?? _field.Name) + (visible ? "" : " (last seen)");
		UIText.ClearChildren(_icons);
		if (visible)
		{
			foreach (var playerUnit in _field.presentPlayerUnits)
				_icons.AddChild(MakeIcon(playerUnit.GetIcon(), PlayerFrame));
		}
		foreach (var neutral in visible ? _field.presentNeutralUnits : _field.LastSeenNeutrals)
			_icons.AddChild(MakeIcon(neutral.GetIcon(), HostileFrame, neutral.Level));

		bool empty = _icons.GetChildCount() == 0;
		_icons.Visible = !empty;
		_emptyLabel.Visible = empty;
		_questsLabel.Text = "";
		RefreshQuests();
		ResetSize(); // shrink back to fit after showing a busier field
	}

	// level > 0 puts the threat level in the icon's corner (hostiles)
	private static Control MakeIcon(Texture2D icon, StyleBox frame, int level = 0)
	{
		var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
		panel.AddThemeStyleboxOverride("panel", frame);
		panel.AddChild(new TextureRect
		{
			Texture = icon,
			CustomMinimumSize = new Vector2(IconSize, IconSize),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore,
		});
		if (level > 0)
			panel.AddChild(new Label
			{
				Text = level.ToString(),
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Bottom,
				MouseFilter = MouseFilterEnum.Ignore,
				LabelSettings = LevelBadge,
			});
		return panel;
	}

	private static readonly LabelSettings LevelBadge = new()
	{
		FontSize = 11,
		FontColor = new Color(1f, 0.82f, 0.4f),
		OutlineSize = 4,
		OutlineColor = Colors.Black,
	};

	private static StyleBoxFlat MakeFrame(Color border)
	{
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.1f, 0.09f, 0.1f),
			BorderColor = border,
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(4);
		style.SetContentMarginAll(1);
		return style;
	}
}
