using Godot;

// Centred window with a closer look at one unit: large portrait, stats, each special
// attribute with what it adds, and a big attribute pentagon. Built in code, like FieldHoverCard.
public partial class UnitInspectWindow : PanelContainer
{
	private const int PortraitSize = 200;

	private Label _title;
	private TextureRect _portrait;
	private Label _stats;
	private GridContainer _attributeRows;
	private AttributePentagon _pentagon;
	private unit _unit;
	private double _refreshTimer;
	private int _shownRevision = -1;

	public override void _Ready()
	{
		Visible = false;
		SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
		GrowHorizontal = GrowDirection.Both;
		GrowVertical = GrowDirection.Both;

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 10);
		AddChild(box);

		// Header: name + close button
		var header = new HBoxContainer();
		_title = new Label { ThemeTypeVariation = "HeaderLabel", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		var close = new Button { Text = "Close", FocusMode = FocusModeEnum.None };
		close.Pressed += Close;
		header.AddChild(_title);
		header.AddChild(close);
		box.AddChild(header);
		box.AddChild(new HSeparator());

		var body = new HBoxContainer();
		body.AddThemeConstantOverride("separation", 16);
		box.AddChild(body);

		// Left column: portrait, stats, attribute list
		var left = new VBoxContainer();
		left.AddThemeConstantOverride("separation", 8);
		body.AddChild(left);
		_portrait = new TextureRect
		{
			CustomMinimumSize = new Vector2(PortraitSize, PortraitSize),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		left.AddChild(_portrait);
		_stats = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(PortraitSize, 0) };
		left.AddChild(_stats);
		left.AddChild(new Label { Text = "Special attributes", ThemeTypeVariation = "MutedLabel" });
		_attributeRows = new GridContainer { Columns = 3 };
		_attributeRows.AddThemeConstantOverride("h_separation", 12);
		left.AddChild(_attributeRows);

		// Right column: the big pentagon
		_pentagon = new AttributePentagon
		{
			CustomMinimumSize = new Vector2(380, 360),
			FontSize = 15,
			FullNames = true,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddChild(_pentagon);
	}

	public void Open(unit inspectedUnit)
	{
		_unit = IsInstanceValid(inspectedUnit) ? inspectedUnit : null;
		if (_unit == null)
		{
			Close();
			return;
		}
		_title.Text = _unit.Template?.Name ?? _unit.Name;
		_portrait.Texture = _unit.GetIcon();
		_shownRevision = -1;
		Refresh();
		Visible = true;
		ResetSize();
		SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.KeepSize);
	}

	public void Close()
	{
		Visible = false;
		_unit = null;
	}

	public override void _Process(double delta)
	{
		// Health/mana change during play; refresh a few times per second while open
		_refreshTimer -= delta;
		if (!Visible || _refreshTimer > 0)
			return;
		_refreshTimer = 0.25;
		if (!IsInstanceValid(_unit))
		{
			Close();
			return;
		}
		Refresh();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Visible && @event.IsActionPressed("ui_cancel"))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Refresh()
	{
		var progression = _unit.Progression;
		string xp = progression.IsMaxLevel ? "max level" : $"XP {progression.Experience}/{UnitProgression.ExperienceToNextLevel(progression.Level)}";
		_stats.Text = $"Level {progression.Level}  ({xp})\n{UIText.Stats(_unit.battleStats)}";
		if (_unit.Revision != _shownRevision)
		{
			// New skills can change attributes
			_shownRevision = _unit.Revision;
			_pentagon.Attributes = _unit.Attributes;
			BuildAttributeRows();
		}
	}

	private void BuildAttributeRows()
	{
		UIText.ClearChildren(_attributeRows);
		var attributes = _unit.Attributes;
		if (attributes == null)
			return;
		for (int i = 0; i < SpecialAttributes.Count; i++)
		{
			_attributeRows.AddChild(new Label { Text = SpecialAttributes.Names[i] });
			_attributeRows.AddChild(new Label { Text = $"{attributes.Get(i)}/{SpecialAttributes.MaxValue}", HorizontalAlignment = HorizontalAlignment.Right });
			_attributeRows.AddChild(new Label { Text = UIText.AttributeBonus(attributes, i), ThemeTypeVariation = "MutedLabel" });
		}
	}
}
