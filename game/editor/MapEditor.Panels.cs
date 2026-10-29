using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// The editor's UI: top bar, tool panel (left), selection properties (right), status bar and dialogs
public partial class MapEditor
{
	private Control _uiRoot;
	private Label _titleLabel;
	private Label _statusLabel;
	private Label _coordsLabel;
	private VBoxContainer _leftBox;
	private VBoxContainer _rightBox;
	private PanelContainer _rightPanel;
	private readonly Dictionary<Layer, Button> _layerButtons = new();
	private FileDialog _fileDialog;
	private bool _showUnderlay;
	private bool _chainFields = true;

	private void BuildUI()
	{
		var layer = new CanvasLayer { Name = "UI" };
		AddChild(layer);
		_uiRoot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = GD.Load<Theme>("res://game/UI/theme/hud_theme.tres") };
		_uiRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(_uiRoot);

		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		column.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		column.AddThemeConstantOverride("separation", 0);
		_uiRoot.AddChild(column);

		column.AddChild(BuildTopBar());

		var middle = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		column.AddChild(middle);
		middle.AddChild(SidePanel(out _leftBox, 240));
		middle.AddChild(new Control { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		_rightPanel = SidePanel(out _rightBox, 270);
		middle.AddChild(_rightPanel);

		var statusBar = new PanelContainer();
		var statusRow = new HBoxContainer();
		statusBar.AddChild(statusRow);
		_titleLabel = new Label { ThemeTypeVariation = "HeaderLabel" };
		_statusLabel = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
		_coordsLabel = new Label { ThemeTypeVariation = "MutedLabel" };
		statusRow.AddThemeConstantOverride("separation", 16);
		statusRow.AddChild(_titleLabel);
		statusRow.AddChild(_statusLabel);
		statusRow.AddChild(_coordsLabel);
		column.AddChild(statusBar);

		_fileDialog = new FileDialog
		{
			Access = FileDialog.AccessEnum.Filesystem,
			Filters = new[] { "*.json ; Map files" },
			Size = new Vector2I(800, 520),
		};
		_fileDialog.FileSelected += OnFileChosen;
		_uiRoot.AddChild(_fileDialog);

		SetLayer(Layer.Terrain);
	}

	private Control BuildTopBar()
	{
		var bar = new PanelContainer();
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		bar.AddChild(row);

		row.AddChild(MakeButton("New", "New empty map", AskNew));
		row.AddChild(MakeButton("Open", "Open a map file (Ctrl+O)", AskOpen));
		row.AddChild(MakeButton("Save", "Save (Ctrl+S)", () => { if (_path == null) ShowFileDialog(true); else Save(_path); }));
		row.AddChild(MakeButton("Save as", "Save to a new file (Ctrl+Shift+S)", () => ShowFileDialog(true)));
		row.AddChild(MakeButton("Map settings", "Name, size, base terrain, reference image, problems", () => ShowMapSettings(false)));
		row.AddChild(new VSeparator());
		row.AddChild(MakeButton("Undo", "Ctrl+Z", Undo));
		row.AddChild(MakeButton("Redo", "Ctrl+Y", Redo));
		row.AddChild(new VSeparator());

		var group = new ButtonGroup();
		foreach (var (layer, text) in new[] { (Layer.Terrain, "1 Terrain"), (Layer.Objects, "2 Objects"), (Layer.Fields, "3 Fields") })
		{
			var button = new Button { Text = text, ToggleMode = true, ButtonGroup = group, TooltipText = $"Edit layer {text}" };
			button.Pressed += () => SetLayer(layer);
			_layerButtons[layer] = button;
			row.AddChild(button);
		}
		row.AddChild(new VSeparator());

		var objects = new CheckBox { Text = "Objects", ButtonPressed = true, TooltipText = "Show layer 2" };
		objects.Toggled += on => _view.Objects.Visible = on;
		row.AddChild(objects);
		var fields = new CheckBox { Text = "Fields", ButtonPressed = true, TooltipText = "Show layer 3" };
		fields.Toggled += on => _fieldOverlay.Visible = on;
		row.AddChild(fields);
		var underlay = new CheckBox { Text = "Reference", TooltipText = "Show the map's reference image (Map settings) to trace over" };
		underlay.Toggled += on =>
		{
			_showUnderlay = on;
			_view.UnderlayVisible = on;
			if (on && !_view.UnderlayVisible)
				SetStatus("This map has no reference image (set one in Map settings)");
		};
		row.AddChild(underlay);
		var opacity = new HSlider { MinValue = 0.1, MaxValue = 1, Step = 0.05, Value = 0.5, CustomMinimumSize = new Vector2(70, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, TooltipText = "Reference image opacity" };
		opacity.ValueChanged += v => _view.UnderlayOpacity = (float)v;
		row.AddChild(opacity);
		CallDeferred(nameof(ApplyUnderlayOpacity), 0.5f);

		row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		var play = MakeButton("Play", "Try this map in the game (F10 in the game comes back here)", Play);
		play.AddThemeColorOverride("font_color", Accent);
		row.AddChild(play);
		return bar;
	}

	private void ApplyUnderlayOpacity(float value) => _view.UnderlayOpacity = value;

	private static PanelContainer SidePanel(out VBoxContainer box, float width)
	{
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(width, 0) };
		var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		panel.AddChild(scroll);
		box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		box.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(box);
		return panel;
	}

	private static Button MakeButton(string text, string tooltip, Action onPressed)
	{
		var button = new Button { Text = text, TooltipText = tooltip };
		button.Pressed += onPressed;
		return button;
	}

	private void SetLayer(Layer layer)
	{
		_layer = layer;
		_layerButtons[layer].ButtonPressed = true;
		_stroke = null;
		_polygon.Clear();
		_painting = false;
		_dragging = false;
		_fieldOverlay.LinkFrom = null;
		_fieldOverlay.ShowIds = layer == Layer.Fields || _fieldIdsAlways;
		_fieldOverlay.QueueRedraw();
		RebuildSidePanels();
	}

	private bool _fieldIdsAlways;

	private void RebuildSidePanels()
	{
		RebuildLeftPanel();
		RebuildRightPanel();
	}

	private static void Clear(Container box)
	{
		foreach (var child in box.GetChildren())
		{
			box.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void SetStatus(string text) => _statusLabel.Text = text;

	private void UpdateTitle()
	{
		if (_titleLabel == null || _data == null)
			return;
		string file = _path == null ? "not saved yet" : _path.GetFile();
		_titleLabel.Text = $"{_data.Name}  ({file}){(_dirty ? " *" : "")}";
	}

	private void UpdateCoords()
	{
		var mouse = _mouse;
		_coordsLabel.Text = $"x {mouse.X:0}  y {mouse.Y:0}   zoom {_camera.Zoom.X * 100:0}%   " +
			$"{_data.Terrain.Count} shapes · {_data.Objects.Count} objects · {_data.Fields.Count} fields";
	}

	// ---------------------------------------------------------------- left panel: tools

	private bool _leftPending, _rightPending;

	// Panels are rebuilt at the end of the frame, never right away: a rebuild often comes from
	// a click on a control inside the panel, and Godot keeps using that control after our handler
	// returns (e.g. ItemList reads the mouse position for item_clicked), so it must still be there.
	private void RebuildLeftPanel()
	{
		if (_leftPending)
			return;
		_leftPending = true;
		Callable.From(RebuildLeftPanelNow).CallDeferred();
	}

	private void RebuildRightPanel()
	{
		if (_rightPending)
			return;
		_rightPending = true;
		Callable.From(RebuildRightPanelNow).CallDeferred();
	}

	private void RebuildLeftPanelNow()
	{
		_leftPending = false;
		if (_leftBox == null || !IsInsideTree())
			return;
		Clear(_leftBox);
		switch (_layer)
		{
			case Layer.Terrain: BuildTerrainPanel(); break;
			case Layer.Objects: BuildObjectPanel(); break;
			case Layer.Fields: BuildFieldPanel(); break;
		}
	}

	private void Header(VBoxContainer box, string text) => box.AddChild(new Label { Text = text, ThemeTypeVariation = "HeaderLabel" });

	private void Hint(VBoxContainer box, string text) =>
		box.AddChild(new Label { Text = text, ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(200, 0) });

	private void ToolButtons<T>(VBoxContainer box, T current, Action<T> select, params (T tool, string text, string tip)[] tools)
	{
		var grid = new GridContainer { Columns = 2 };
		var group = new ButtonGroup();
		foreach (var (tool, text, tip) in tools)
		{
			var button = new Button { Text = text, ToggleMode = true, ButtonGroup = group, TooltipText = tip, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			button.ButtonPressed = EqualityComparer<T>.Default.Equals(tool, current);
			button.Pressed += () => select(tool);
			grid.AddChild(button);
		}
		box.AddChild(grid);
	}

	private void Slider(VBoxContainer box, string label, double min, double max, double step, double value, Action<double> changed, bool exponential = false)
	{
		var title = new Label { Text = $"{label}: {value:0.##}" };
		box.AddChild(title);
		var slider = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, ExpEdit = exponential };
		slider.ValueChanged += v =>
		{
			title.Text = $"{label}: {v:0.##}";
			changed(v);
		};
		box.AddChild(slider);
	}

	private void BuildTerrainPanel()
	{
		Header(_leftBox, "Layer 1 - Terrain");
		ToolButtons(_leftBox, _terrainTool, t => { _terrainTool = t; _polygon.Clear(); _selectedShape = null; RebuildSidePanels(); },
			(TerrainTool.Brush, "Brush", "Paint strokes (rivers, roads, coastlines). Right-drag paints the base terrain (eraser)."),
			(TerrainTool.Fill, "Fill", "Drag around an area to fill it (islands, lakes, deserts). Right-drag fills with the base terrain."),
			(TerrainTool.Polygon, "Polygon", "Click points, double-click / Enter / click the first point to finish. Right-click removes the last point."),
			(TerrainTool.Pick, "Pick", "Click a shape to select it, drag to move it, Delete to remove it."));

		_leftBox.AddChild(new Label { Text = "Terrain type" });
		var list = new ItemList { AutoHeight = true, FixedIconSize = new Vector2I(28, 28), IconMode = ItemList.IconModeEnum.Left };
		foreach (var type in MapCatalog.TerrainTypes.Values)
		{
			int index = list.AddItem(type.Name, TerrainTextures.Get(type.Id));
			list.SetItemMetadata(index, type.Id);
			list.SetItemTooltip(index, type.Hint ?? type.Name);
			if (type.Id == _terrainType)
				list.Select(index);
		}
		list.ItemSelected += index =>
		{
			_terrainType = list.GetItemMetadata((int)index).AsString();
			// Thin brush for roads and rivers, wide for areas
			_brushWidth = MapCatalog.TerrainTypes[_terrainType].BrushWidth;
			RebuildLeftPanel();
		};
		_leftBox.AddChild(list);

		Slider(_leftBox, "Brush width", 2, 600, 1, _brushWidth, v => _brushWidth = (float)v, exponential: true);
		var rough = new CheckBox { Text = "Natural edges", ButtonPressed = _roughEdges, TooltipText = "Fill and Polygon shapes get rounded, wobbly coastline edges" };
		rough.Toggled += on => _roughEdges = on;
		_leftBox.AddChild(rough);
		Hint(_leftBox, "Later shapes are painted on top of earlier ones. Right mouse paints the base terrain " +
			$"({MapCatalog.Terrain(_data?.Base).Name}).\n[ and ] change the brush size.\n" +
			"Pan: middle mouse, Space+drag or WASD. Zoom: mouse wheel.");
	}

	private void BuildObjectPanel()
	{
		Header(_leftBox, "Layer 2 - Objects");
		ToolButtons(_leftBox, _objectTool, t => { _objectTool = t; _selectedObject = null; RebuildSidePanels(); },
			(ObjectTool.Place, "Place", "Click to place one. Right-click removes the object under the mouse."),
			(ObjectTool.Scatter, "Scatter", "Drag to scatter many (forests, mountain ranges). Right-drag erases."),
			(ObjectTool.Select, "Select", "Click to select, drag to move, Delete to remove."),
			(ObjectTool.Erase, "Erase", "Drag to remove objects under the brush."));

		_leftBox.AddChild(new Label { Text = "Object" });
		var list = new ItemList { AutoHeight = true };
		foreach (var category in MapCatalog.ObjectTypes.Values.GroupBy(t => t.Category))
		{
			int header = list.AddItem(category.Key.ToUpper(), null, false);
			list.SetItemDisabled(header, true);
			foreach (var type in category)
			{
				int index = list.AddItem("   " + type.Name);
				list.SetItemMetadata(index, type.Id);
				if (type.Id == _objectType)
					list.Select(index);
			}
		}
		list.ItemSelected += index =>
		{
			var id = list.GetItemMetadata((int)index).AsString();
			if (!string.IsNullOrEmpty(id))
				_objectType = id;
		};
		_leftBox.AddChild(list);

		Slider(_leftBox, "Size", 0.2, 4, 0.05, _objectScale, v => _objectScale = (float)v);
		if (_objectTool is ObjectTool.Scatter or ObjectTool.Erase)
			Slider(_leftBox, "Brush radius", 8, 600, 1, _brushRadius, v => _brushRadius = (float)v, exponential: true);
		if (_objectTool == ObjectTool.Scatter)
			Slider(_leftBox, "Density", 0.2, 3, 0.05, _density, v => _density = (float)v);
		if (_objectTool == ObjectTool.Erase)
		{
			var only = new CheckBox { Text = "Only the selected type", ButtonPressed = _eraseOnlyType };
			only.Toggled += on => _eraseOnlyType = on;
			_leftBox.AddChild(only);
		}
		Hint(_leftBox, "Objects are decoration; what units can do on the map is set by fields (layer 3). " +
			"A field placed next to a castle, mine, village or forest gets a matching type.");
	}

	private void BuildFieldPanel()
	{
		Header(_leftBox, "Layer 3 - Fields");
		ToolButtons(_leftBox, _fieldTool, t => { _fieldTool = t; RebuildLeftPanel(); },
			(FieldTool.Select, "Select", "Click a field to edit it, drag to move it."),
			(FieldTool.Add, "Add", "Click empty ground to add a field."),
			(FieldTool.Connect, "Connect", "Drag from one field to another to connect them (or disconnect if they already are)."),
			(FieldTool.Delete, "Delete", "Click a field to delete it with its connections."));
		if (_fieldTool == FieldTool.Add)
		{
			var chain = new CheckBox { Text = "Connect to selected field", ButtonPressed = _chainFields, TooltipText = "Each new field is connected to the field selected before it, so you can lay out a path by clicking" };
			chain.Toggled += on => _chainFields = on;
			_leftBox.AddChild(chain);
		}
		var ids = new CheckBox { Text = "Show field ids on all layers", ButtonPressed = _fieldIdsAlways };
		ids.Toggled += on => { _fieldIdsAlways = on; _fieldOverlay.ShowIds = true; _fieldOverlay.QueueRedraw(); };
		_leftBox.AddChild(ids);

		int alone = _data?.Fields.Count(f => !_data.Connections.Any(c => c[0] == f.Id || c[1] == f.Id)) ?? 0;
		_leftBox.AddChild(new Label { Text = $"{_data?.Fields.Count ?? 0} fields, {_data?.Connections.Count ?? 0} connections" + (alone > 0 ? $"\n{alone} field(s) not connected" : "") });
		Hint(_leftBox, "With a field selected, Shift+click or right-click another field to connect / disconnect them.\n" +
			"The player's starting units begin on the first Castle field.");
	}

	// ---------------------------------------------------------------- right panel: selection

	private void RebuildRightPanelNow()
	{
		_rightPending = false;
		if (_rightBox == null || !IsInsideTree())
			return;
		Clear(_rightBox);
		if (_layer == Layer.Fields && _selectedField != null)
			BuildFieldProperties(_selectedField);
		else if (_layer == Layer.Objects && _selectedObject != null)
			BuildObjectProperties(_selectedObject);
		else if (_layer == Layer.Terrain && _selectedShape != null)
			BuildShapeProperties(_selectedShape);
		_rightPanel.Visible = _rightBox.GetChildCount() > 0;
	}

	// Up to three of the given unit types, 1-2 of each
	private static List<NeutralData> RandomGuards(List<string> templates)
	{
		var rng = new Random();
		return templates.OrderBy(_ => rng.Next()).Take(3)
			.Select(name => new NeutralData { Template = name, Count = rng.Next(1, 3) }).ToList();
	}

	private OptionButton Options(IEnumerable<string> items, string selected, Action<string> changed)
	{
		var options = new OptionButton();
		int i = 0;
		foreach (var item in items)
		{
			options.AddItem(item);
			if (item == selected)
				options.Select(i);
			i++;
		}
		options.ItemSelected += index => changed(options.GetItemText((int)index));
		return options;
	}

	private SpinBox Spin(double min, double max, double step, double value, Action<double> changed)
	{
		var spin = new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		spin.ValueChanged += v => changed(v);
		return spin;
	}

	private void Row(VBoxContainer box, string label, Control control)
	{
		var row = new HBoxContainer();
		row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(80, 0) });
		control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddChild(control);
		box.AddChild(row);
	}

	private void BuildShapeProperties(TerrainShape shape)
	{
		Header(_rightBox, "Terrain shape");
		Row(_rightBox, "Type", Options(MapCatalog.TerrainTypes.Keys, shape.Type, type =>
		{
			PushUndo();
			shape.Type = type;
			_view.Terrain.Refresh();
		}));
		_rightBox.AddChild(new Label { Text = $"{shape.Shape}, {shape.Points?.Count ?? 0} points", ThemeTypeVariation = "MutedLabel" });
		if (shape.Shape == "stroke")
			Row(_rightBox, "Width", Spin(1, 2000, 1, shape.Width, v =>
			{
				PushUndo();
				shape.Width = (float)v;
				_view.Terrain.Invalidate(shape);
				_view.Terrain.Refresh();
			}));
		var rough = new CheckBox { Text = "Natural edges", ButtonPressed = shape.IsRough };
		rough.Toggled += on =>
		{
			PushUndo();
			shape.Rough = on ? null : false;
			_view.Terrain.Invalidate(shape);
			_view.Terrain.Refresh();
		};
		_rightBox.AddChild(rough);
		var order = new HBoxContainer();
		order.AddChild(MakeButton("To front", "Paint this shape over all others", () => MoveShapeOrder(shape, true)));
		order.AddChild(MakeButton("To back", "Paint this shape under all others", () => MoveShapeOrder(shape, false)));
		_rightBox.AddChild(order);
		_rightBox.AddChild(MakeButton("Delete shape", "Delete", DeleteSelection));
	}

	private void BuildObjectProperties(MapObject obj)
	{
		var type = MapCatalog.ObjectTypes.GetValueOrDefault(obj.Type);
		Header(_rightBox, type?.Name ?? obj.Type);
		void Changed()
		{
			_view.Objects.Refresh(obj.Pos[1], obj.Pos[1]);
		}
		Row(_rightBox, "Size", Spin(0.1, 10, 0.05, obj.Scale, v => { PushUndo(); obj.Scale = (float)v; Changed(); }));
		Row(_rightBox, "Variant", Spin(0, 999, 1, obj.Variant, v => { PushUndo(); obj.Variant = (int)v; Changed(); }));
		if (type?.Turnable == true)
			Row(_rightBox, "Rotation", Spin(-360, 360, 5, obj.Rot, v => { PushUndo(); obj.Rot = (float)v; Changed(); }));
		var flip = new CheckBox { Text = "Mirrored", ButtonPressed = obj.Flip };
		flip.Toggled += on => { PushUndo(); obj.Flip = on; Changed(); };
		_rightBox.AddChild(flip);
		_rightBox.AddChild(MakeButton("Delete object", "Delete", DeleteSelection));
	}

	private void BuildFieldProperties(FieldData field)
	{
		Header(_rightBox, "Field");
		var id = new LineEdit { Text = field.Id };
		void Rename()
		{
			string newId = id.Text.Trim();
			if (newId == field.Id)
				return;
			if (newId.Length == 0 || _data.FindField(newId) != null)
			{
				SetStatus(newId.Length == 0 ? "A field needs an id" : $"There already is a field {newId}");
				id.Text = field.Id;
				return;
			}
			PushUndo();
			_data.RenameField(field, newId);
			_fieldOverlay.QueueRedraw();
		}
		id.TextSubmitted += _ => Rename();
		id.FocusExited += Rename;
		Row(_rightBox, "Id", id);
		Row(_rightBox, "Type", Options(FieldTypeManager.FieldTypes.Keys, field.Type, type =>
		{
			PushUndo();
			field.Type = type;
			_fieldOverlay.QueueRedraw();
		}));
		var fieldType = FieldTypeManager.GetFieldType(field.Type ?? "");
		if (fieldType != null && fieldType.Perks.Count > 0)
			_rightBox.AddChild(new Label { Text = "Type gives: " + string.Join(", ", fieldType.Perks.Select(p => p.ResourcePath.GetFile().GetBaseName())), ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart });

		_rightBox.AddChild(new HSeparator());
		_rightBox.AddChild(new Label { Text = "Extra perks" });
		foreach (var perk in MapCatalog.PerkNames)
		{
			var check = new CheckBox { Text = perk, ButtonPressed = field.Perks?.Contains(perk) == true };
			check.Toggled += on =>
			{
				PushUndo();
				field.Perks ??= new List<string>();
				if (on)
					field.Perks.Add(perk);
				else
					field.Perks.Remove(perk);
				if (field.Perks.Count == 0)
					field.Perks = null;
			};
			_rightBox.AddChild(check);
		}

		_rightBox.AddChild(new HSeparator());
		_rightBox.AddChild(new Label { Text = "Story quests (no timer)" });
		foreach (var quest in MapCatalog.QuestNames)
		{
			var check = new CheckBox { Text = MapCatalog.LoadQuest(quest)?.Title ?? quest, ButtonPressed = field.Quests?.Contains(quest) == true };
			check.Toggled += on =>
			{
				PushUndo();
				field.Quests ??= new List<string>();
				if (on)
					field.Quests.Add(quest);
				else
					field.Quests.Remove(quest);
				if (field.Quests.Count == 0)
					field.Quests = null;
			};
			_rightBox.AddChild(check);
		}

		_rightBox.AddChild(new HSeparator());
		_rightBox.AddChild(new Label { Text = "Neutral units" });
		var biome = Biomes.Of(field.Type);
		var fitting = MapCatalog.UnitTemplatesFor(biome);
		if (biome != Biome.None)
			_rightBox.AddChild(new Label
			{
				Text = $"Biome: {biome}" + (fitting.Count > 0 ? $" - fits {string.Join(", ", fitting)}" : " - no guards for it yet"),
				ThemeTypeVariation = "MutedLabel",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			});
		// Guards that live in this biome first
		var templateNames = fitting.Concat(MapCatalog.UnitTemplateNames.Except(fitting)).ToList();
		foreach (var neutral in field.Neutrals ?? new List<NeutralData>())
		{
			var row = new HBoxContainer();
			var template = Options(templateNames, neutral.Template, t => { PushUndo(); neutral.Template = t; });
			template.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			row.AddChild(template);
			var count = Spin(1, 99, 1, neutral.Count, v => { PushUndo(); neutral.Count = (int)v; _fieldOverlay.QueueRedraw(); });
			count.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
			row.AddChild(count);
			row.AddChild(MakeButton("x", "Remove", () =>
			{
				PushUndo();
				field.Neutrals.Remove(neutral);
				if (field.Neutrals.Count == 0)
					field.Neutrals = null;
				_fieldOverlay.QueueRedraw();
				RebuildRightPanel();
			}));
			_rightBox.AddChild(row);
		}
		_rightBox.AddChild(MakeButton("+ Add neutral unit", "Neutral units guard this field at the start", () =>
		{
			PushUndo();
			field.Neutrals ??= new List<NeutralData>();
			field.Neutrals.Add(new NeutralData { Template = templateNames.FirstOrDefault() ?? "wolf", Count = 1 });
			_fieldOverlay.QueueRedraw();
			RebuildRightPanel();
		}));
		if (fitting.Count > 0)
			_rightBox.AddChild(MakeButton("Fill guards by biome", "Replace this field's guards with a random group of the units that live in its biome", () =>
			{
				PushUndo();
				field.Neutrals = RandomGuards(fitting);
				_fieldOverlay.QueueRedraw();
				RebuildRightPanel();
			}));

		_rightBox.AddChild(new HSeparator());
		var links = _data.Connections.Where(c => c[0] == field.Id || c[1] == field.Id).Select(c => c[0] == field.Id ? c[1] : c[0]).ToList();
		_rightBox.AddChild(new Label { Text = links.Count == 0 ? "Not connected" : "Connected to: " + string.Join(", ", links), AutowrapMode = TextServer.AutowrapMode.WordSmart });
		_rightBox.AddChild(MakeButton("Delete field", "Delete", DeleteSelection));
	}

	// ---------------------------------------------------------------- dialogs

	private void Confirm(string text, Action onYes, Action onNo = null)
	{
		var dialog = new ConfirmationDialog { DialogText = text, Title = "Map Editor" };
		bool answered = false;
		dialog.Confirmed += () => { answered = true; onYes(); };
		dialog.VisibilityChanged += () =>
		{
			if (dialog.Visible)
				return;
			if (!answered)
				onNo?.Invoke();
			dialog.QueueFree();
		};
		_uiRoot.AddChild(dialog);
		dialog.PopupCentered();
	}

	private void IfNoUnsavedChanges(Action action)
	{
		if (!_dirty)
			action();
		else
			Confirm("You have unsaved changes. Throw them away?", action);
	}

	private void AskNew() => IfNoUnsavedChanges(() => ShowMapSettings(true));

	private void AskOpen() => IfNoUnsavedChanges(() => ShowFileDialog(false));

	private static string MapsFolder()
	{
		// Inside the project while developing (so maps can be committed); next to the save games in a build
		string dir = OS.HasFeature("editor") ? ProjectSettings.GlobalizePath("res://maps") : ProjectSettings.GlobalizePath("user://maps");
		DirAccess.MakeDirRecursiveAbsolute(dir);
		return dir;
	}

	private bool _fileDialogSaves;

	private void ShowFileDialog(bool save)
	{
		_fileDialogSaves = save;
		_fileDialog.FileMode = save ? FileDialog.FileModeEnum.SaveFile : FileDialog.FileModeEnum.OpenFile;
		_fileDialog.Title = save ? "Save map" : "Open map";
		string current = _path != null && !_path.StartsWith("res://") ? _path : _path != null ? ProjectSettings.GlobalizePath(_path) : null;
		_fileDialog.CurrentDir = current?.GetBaseDir() ?? MapsFolder();
		if (save)
			_fileDialog.CurrentFile = current?.GetFile() ?? $"{_data.Name.ToLower().Replace(' ', '_')}.json";
		_fileDialog.PopupCentered();
	}

	private void OnFileChosen(string path)
	{
		if (_fileDialogSaves)
			Save(path);
		else
			Open(path);
	}

	// Map settings, also used to make a new map
	private void ShowMapSettings(bool createNew)
	{
		var source = createNew ? NewMap(3072, 2048, "ocean") : _data;
		var dialog = new AcceptDialog { Title = createNew ? "New map" : "Map settings", OkButtonText = createNew ? "Create" : "Apply" };
		var box = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
		dialog.AddChild(box);
		var name = new LineEdit { Text = source.Name };
		Row(box, "Name", name);
		var width = new SpinBox { MinValue = 256, MaxValue = 16384, Step = 16, Value = source.Width };
		Row(box, "Width", width);
		var height = new SpinBox { MinValue = 256, MaxValue = 16384, Step = 16, Value = source.Height };
		Row(box, "Height", height);
		string baseType = source.Base;
		Row(box, "Base terrain", Options(MapCatalog.TerrainTypes.Keys, source.Base, t => baseType = t));
		var seed = new SpinBox { MinValue = 0, MaxValue = 999999, Value = source.Seed, TooltipText = "Changes the wobble of natural edges" };
		Row(box, "Edge seed", seed);
		var underlay = new LineEdit { Text = source.Underlay ?? "", PlaceholderText = "res://... or C:/... image to trace over" };
		Row(box, "Reference", underlay);
		if (!createNew)
		{
			var problems = _data.Validate();
			box.AddChild(new HSeparator());
			box.AddChild(new Label
			{
				Text = problems.Count == 0 ? "No problems found." : "Problems:\n- " + string.Join("\n- ", problems.Take(15)) + (problems.Count > 15 ? $"\n... and {problems.Count - 15} more" : ""),
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(400, 0),
			});
		}
		dialog.Confirmed += () =>
		{
			if (createNew)
			{
				var data = NewMap((float)width.Value, (float)height.Value, baseType);
				data.Name = name.Text.Trim().Length > 0 ? name.Text.Trim() : "New map";
				data.Seed = (int)seed.Value;
				data.Underlay = underlay.Text.Trim().Length > 0 ? underlay.Text.Trim() : null;
				SetMap(data, null, keepView: false);
				_dirty = true;
				UpdateTitle();
				return;
			}
			PushUndo();
			_data.Name = name.Text.Trim().Length > 0 ? name.Text.Trim() : _data.Name;
			_data.Width = (float)width.Value;
			_data.Height = (float)height.Value;
			_data.Base = baseType;
			_data.Seed = (int)seed.Value;
			_data.Underlay = underlay.Text.Trim().Length > 0 ? underlay.Text.Trim() : null;
			ShowData();
		};
		dialog.VisibilityChanged += () => { if (!dialog.Visible) dialog.QueueFree(); };
		_uiRoot.AddChild(dialog);
		dialog.PopupCentered();
	}
}
