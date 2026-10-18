using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// In-game World Map Editor. Edits a MapData in three layers:
//   1 Terrain - paint grass/desert/water/ocean/roads/... as strokes and filled shapes
//   2 Objects - trees, mountains, castles, towns, ... (decoration)
//   3 Fields  - the nodes units walk on, their connections, perks and neutral units
// The panels are built in MapEditor.Panels.cs. The file on disk is watched, so a map written
// by another program (e.g. an AI session) shows up here as soon as it is saved.
public partial class MapEditor : Node2D
{
	public enum Layer { Terrain, Objects, Fields }
	private enum TerrainTool { Brush, Fill, Polygon, Pick }
	private enum ObjectTool { Place, Scatter, Select, Erase }
	private enum FieldTool { Select, Add, Connect, Delete }

	private const int MaxUndo = 100;
	private static readonly Color Accent = new(1f, 0.82f, 0.35f);

	private MapData _data;
	private string _path;
	private bool _dirty;
	private ulong _diskTime;
	private double _watchTimer;
	private bool _askingReload;
	private readonly List<string> _undo = new();
	private readonly List<string> _redo = new();

	private MapView _view;
	private FieldOverlay _fieldOverlay;
	private Overlay _overlay;
	private Camera2D _camera;
	private readonly Random _rng = new();

	private Layer _layer = Layer.Terrain;
	private TerrainTool _terrainTool = TerrainTool.Brush;
	private ObjectTool _objectTool = ObjectTool.Place;
	private FieldTool _fieldTool = FieldTool.Select;

	private string _terrainType = "grass";
	private float _brushWidth = 80;
	private bool _roughEdges = true;
	private string _objectType = "pine";
	private float _objectScale = 1f;
	private float _brushRadius = 80;
	private float _density = 1f;
	private bool _eraseOnlyType;

	// What the mouse is doing right now
	private List<Vector2> _stroke;           // brush / fill outline being drawn
	private bool _strokeErases;              // right-drag brush paints the base terrain
	private readonly List<Vector2> _polygon = new();
	private TerrainShape _selectedShape;
	private MapObject _selectedObject;
	private FieldData _selectedField;
	private bool _dragging;                  // moving the selection
	private bool _dragUndoPushed;
	private Vector2 _dragLast;
	private bool _painting;                  // scatter / erase brush held down
	private bool _panning;
	private bool _spaceHeld;
	private Vector2 _mouse; // world position of the mouse, from the last mouse event

	public override void _Ready()
	{
		_view = new MapView { Name = "MapView" };
		AddChild(_view);
		_fieldOverlay = new FieldOverlay { Name = "Fields" };
		AddChild(_fieldOverlay);
		_overlay = new Overlay { Name = "ToolOverlay", Editor = this };
		AddChild(_overlay);
		_camera = new Camera2D { Name = "Camera" };
		AddChild(_camera);
		_camera.MakeCurrent();

		BuildUI();

		// Back from a test play: continue with the same map; otherwise open the selected map
		if (MapSelection.FromEditor && MapSelection.Data != null)
		{
			SetMap(MapSelection.Data, MapSelection.Path, keepView: false);
			_dirty = MapSelection.EditorDirty;
		}
		else
		{
			var data = MapData.Load(MapSelection.Path);
			if (data != null)
				SetMap(data, MapSelection.Path, keepView: false);
			else
				SetMap(NewMap(3072, 2048, "ocean"), null, keepView: false);
		}
		MapSelection.FromEditor = false;
		MapSelection.Data = null;
		UpdateTitle();
	}

	private static MapData NewMap(float width, float height, string baseTerrain) => new()
	{
		Name = "New map",
		Width = width,
		Height = height,
		Base = baseTerrain,
		Seed = new Random().Next(1, 99999),
	};

	// ---------------------------------------------------------------- map & files

	private void SetMap(MapData data, string path, bool keepView)
	{
		_data = data;
		_path = path;
		_dirty = false;
		_diskTime = path != null && FileAccess.FileExists(path) ? FileAccess.GetModifiedTime(path) : 0;
		_undo.Clear();
		_redo.Clear();
		ShowData();
		if (!keepView)
		{
			_camera.Position = data.Size / 2f;
			float fit = Math.Min(GetViewportRect().Size.X / data.Width, GetViewportRect().Size.Y / data.Height) * 0.9f;
			_camera.Zoom = Vector2.One * Math.Clamp(fit, 0.1f, 2f);
		}
		var problems = data.Validate();
		if (problems.Count > 0)
			SetStatus($"Map has {problems.Count} problem(s): {problems[0]}{(problems.Count > 1 ? " ... (see Map settings)" : "")}");
	}

	// Shows the current _data everywhere (after load, undo, redo)
	private void ShowData()
	{
		_selectedShape = null;
		_selectedObject = null;
		_selectedField = _selectedField != null ? _data.FindField(_selectedField.Id) : null;
		_view.SetMap(_data);
		_view.UnderlayVisible = _showUnderlay;
		_fieldOverlay.Data = _data;
		_fieldOverlay.Selected = _selectedField;
		_fieldOverlay.QueueRedraw();
		_overlay.QueueRedraw();
		RebuildSidePanels();
		UpdateTitle();
	}

	private void Save(string path)
	{
		if (!path.EndsWith(".json"))
			path += ".json";
		if (!_data.Save(path))
		{
			SetStatus($"Could not save {path}");
			return;
		}
		_path = path;
		_dirty = false;
		_diskTime = FileAccess.GetModifiedTime(path);
		SetStatus($"Saved {path}");
		UpdateTitle();
	}

	private void Open(string path)
	{
		var data = MapData.Load(path);
		if (data == null)
		{
			SetStatus($"Could not open {path} (see the output log)");
			return;
		}
		SetMap(data, path, keepView: false);
		SetStatus($"Opened {path}");
	}

	// Picks up changes another program saved into the open file
	private void WatchFile(double delta)
	{
		_watchTimer += delta;
		if (_watchTimer < 1.0 || _path == null || _askingReload)
			return;
		_watchTimer = 0;
		if (!FileAccess.FileExists(_path))
			return;
		ulong time = FileAccess.GetModifiedTime(_path);
		if (time == _diskTime)
			return;
		_diskTime = time;
		if (!_dirty)
		{
			ReloadFromDisk();
			return;
		}
		_askingReload = true;
		Confirm("The map file was changed by another program.\nReload it and lose your unsaved changes?", ReloadFromDisk, () => _askingReload = false);
	}

	private void ReloadFromDisk()
	{
		_askingReload = false;
		var data = MapData.Load(_path);
		if (data == null)
		{
			SetStatus("The map file changed on disk but could not be read (is it valid JSON?)");
			return;
		}
		SetMap(data, _path, keepView: true);
		SetStatus($"Reloaded {_path} (changed on disk)");
	}

	private void Play()
	{
		MapSelection.Data = _data.Clone();
		MapSelection.Path = _path ?? MapSelection.DefaultMap;
		MapSelection.FromEditor = true;
		MapSelection.EditorDirty = _dirty;
		GetTree().ChangeSceneToFile(MapSelection.GameScene);
	}

	// ---------------------------------------------------------------- undo

	// Call right before changing _data
	private void PushUndo()
	{
		_undo.Add(_data.ToJson());
		if (_undo.Count > MaxUndo)
			_undo.RemoveAt(0);
		_redo.Clear();
		MarkDirty();
	}

	private void MarkDirty()
	{
		if (_dirty)
			return;
		_dirty = true;
		UpdateTitle();
	}

	private void Undo() => StepHistory(_undo, _redo, "Undo");
	private void Redo() => StepHistory(_redo, _undo, "Redo");

	private void StepHistory(List<string> from, List<string> to, string what)
	{
		if (from.Count == 0)
		{
			SetStatus($"Nothing to {what.ToLower()}");
			return;
		}
		to.Add(_data.ToJson());
		_data = MapData.FromJson(from[^1]);
		from.RemoveAt(from.Count - 1);
		_dirty = true;
		ShowData();
		SetStatus(what);
	}

	// ---------------------------------------------------------------- frame & camera

	public override void _Process(double delta)
	{
		WatchFile(delta);
		var focus = GetViewport().GuiGetFocusOwner();
		if (focus is not LineEdit && focus is not TextEdit)
		{
			var move = Vector2.Zero;
			if (Input.IsActionPressed("ui_left")) move.X -= 1;
			if (Input.IsActionPressed("ui_right")) move.X += 1;
			if (Input.IsActionPressed("ui_up")) move.Y -= 1;
			if (Input.IsActionPressed("ui_down")) move.Y += 1;
			if (move != Vector2.Zero)
				_camera.Position += move.Normalized() * 900f * (float)delta / _camera.Zoom.X;
		}
		UpdateCoords();
		_overlay.QueueRedraw();
	}

	private void ZoomAt(Vector2 screen, float factor)
	{
		var viewportCenter = GetViewportRect().Size / 2f;
		var world = _camera.Position + (screen - viewportCenter) / _camera.Zoom;
		float zoom = Math.Clamp(_camera.Zoom.X * factor, 0.08f, 4f);
		_camera.Zoom = Vector2.One * zoom;
		_camera.Position = world - (screen - viewportCenter) / zoom;
	}

	private float Px => 1f / _camera.Zoom.X; // one screen pixel in world units

	// ---------------------------------------------------------------- input

	public override void _UnhandledInput(InputEvent e)
	{
		if (e is InputEventKey key)
		{
			if (key.Keycode == Key.Space)
				_spaceHeld = key.Pressed;
			if (key.Pressed && !key.Echo && HandleKey(key))
				GetViewport().SetInputAsHandled();
			return;
		}
		if (e is InputEventMouse mouseEvent)
			_mouse = GetViewport().CanvasTransform.AffineInverse() * mouseEvent.Position;
		if (e is InputEventMouseButton button)
		{
			if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed)
				ZoomAt(button.Position, 1.15f);
			else if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed)
				ZoomAt(button.Position, 1f / 1.15f);
			else if (button.ButtonIndex == MouseButton.Middle || (button.ButtonIndex == MouseButton.Left && _spaceHeld && button.Pressed))
				_panning = button.Pressed;
			else if (_panning && !button.Pressed)
				_panning = false;
			else if (button.Pressed)
				OnPress(button.ButtonIndex, _mouse, button.ShiftPressed, button.DoubleClick);
			else
				OnRelease(button.ButtonIndex, _mouse);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (e is InputEventMouseMotion motion)
		{
			if (_panning)
				_camera.Position -= motion.Relative / _camera.Zoom;
			else
				OnDrag(_mouse);
		}
	}

	private bool HandleKey(InputEventKey key)
	{
		if (key.CtrlPressed)
		{
			switch (key.Keycode)
			{
				case Key.Z when key.ShiftPressed: Redo(); return true;
				case Key.Z: Undo(); return true;
				case Key.Y: Redo(); return true;
				case Key.S when key.ShiftPressed || _path == null: ShowFileDialog(save: true); return true;
				case Key.S: Save(_path); return true;
				case Key.O: AskOpen(); return true;
			}
			return false;
		}
		switch (key.Keycode)
		{
			case Key.Key1: SetLayer(Layer.Terrain); return true;
			case Key.Key2: SetLayer(Layer.Objects); return true;
			case Key.Key3: SetLayer(Layer.Fields); return true;
			case Key.Delete:
			case Key.Backspace when _polygon.Count == 0:
				DeleteSelection();
				return true;
			case Key.Backspace:
				_polygon.RemoveAt(_polygon.Count - 1);
				return true;
			case Key.Enter:
			case Key.KpEnter:
				FinishPolygon();
				return true;
			case Key.Escape:
				_polygon.Clear();
				_stroke = null;
				_fieldOverlay.LinkFrom = null;
				return true;
			case Key.Bracketleft:
				ResizeBrush(1f / 1.2f);
				return true;
			case Key.Bracketright:
				ResizeBrush(1.2f);
				return true;
		}
		return false;
	}

	private void ResizeBrush(float factor)
	{
		if (_layer == Layer.Terrain)
			_brushWidth = Math.Clamp(_brushWidth * factor, 2f, 600f);
		else
			_brushRadius = Math.Clamp(_brushRadius * factor, 8f, 600f);
		RebuildLeftPanel();
	}

	private void OnPress(MouseButton button, Vector2 pos, bool shift, bool doubleClick)
	{
		if (button != MouseButton.Left && button != MouseButton.Right)
			return;
		bool left = button == MouseButton.Left;
		switch (_layer)
		{
			case Layer.Terrain: TerrainPress(left, pos, doubleClick); break;
			case Layer.Objects: ObjectPress(left, pos); break;
			case Layer.Fields: FieldPress(left, pos, shift); break;
		}
	}

	private void OnDrag(Vector2 pos)
	{
		if (_stroke != null)
		{
			if (_stroke[^1].DistanceTo(pos) > 2f * Px)
				_stroke.Add(pos);
			return;
		}
		if (_painting)
		{
			if (_objectTool == ObjectTool.Scatter && !_strokeErases)
				ScatterAt(pos);
			else
				EraseObjectsAt(pos);
			return;
		}
		if (_fieldOverlay.LinkFrom != null)
		{
			_fieldOverlay.LinkTo = pos;
			_fieldOverlay.Hovered = _fieldOverlay.FieldAt(pos);
			_fieldOverlay.QueueRedraw();
			return;
		}
		if (_dragging)
		{
			DragSelection(pos);
			return;
		}
		if (_layer == Layer.Fields)
		{
			var hovered = _fieldOverlay.FieldAt(pos);
			if (hovered != _fieldOverlay.Hovered)
			{
				_fieldOverlay.Hovered = hovered;
				_fieldOverlay.QueueRedraw();
			}
		}
	}

	private void OnRelease(MouseButton button, Vector2 pos)
	{
		if (_stroke != null)
			FinishStroke();
		if (_fieldOverlay.LinkFrom != null)
			FinishLink(pos);
		if (_painting)
		{
			_painting = false;
			_strokeErases = false;
		}
		if (_dragging)
		{
			_dragging = false;
			if (_dragUndoPushed && _selectedField != null)
				RebuildRightPanel(); // shows the new position
		}
	}

	// ---------------------------------------------------------------- layer 1: terrain

	private void TerrainPress(bool left, Vector2 pos, bool doubleClick)
	{
		switch (_terrainTool)
		{
			case TerrainTool.Brush:
			case TerrainTool.Fill:
				_stroke = new List<Vector2> { pos };
				_strokeErases = !left;
				break;
			case TerrainTool.Polygon:
				if (!left)
				{
					if (_polygon.Count > 0)
						_polygon.RemoveAt(_polygon.Count - 1);
				}
				else if (doubleClick || (_polygon.Count >= 3 && pos.DistanceTo(_polygon[0]) < 10 * Px))
					FinishPolygon();
				else
					_polygon.Add(pos);
				break;
			case TerrainTool.Pick:
				_selectedShape = null;
				for (int i = _data.Terrain.Count - 1; i >= 0; i--)
				{
					if (TerrainGeometry.Contains(_data.Terrain[i], pos))
					{
						_selectedShape = _data.Terrain[i];
						break;
					}
				}
				StartDrag(pos, _selectedShape != null && left);
				RebuildRightPanel();
				break;
		}
	}

	// Terrain painted with the right mouse button: the map's base terrain (erases what's on top)
	private string PaintType => _strokeErases ? _data.Base : _terrainType;

	private void FinishStroke()
	{
		var points = _stroke;
		_stroke = null;
		var shape = new TerrainShape { Type = PaintType };
		if (_terrainTool == TerrainTool.Brush)
		{
			points = TerrainGeometry.Simplify(points, 1.2f);
			shape.Shape = "stroke";
			shape.Width = MathF.Round(_brushWidth, 1);
			shape.Rough = false; // what you paint is what you get
		}
		else
		{
			points = TerrainGeometry.Simplify(points, 2.5f);
			if (points.Count < 3)
				return;
			shape.Shape = "polygon";
			shape.Rough = _roughEdges ? null : false;
		}
		shape.Points = points.Select(MapData.P).ToList();
		AddShape(shape);
		_strokeErases = false;
	}

	private void FinishPolygon()
	{
		if (_polygon.Count < 3)
			return;
		AddShape(new TerrainShape
		{
			Type = _terrainType,
			Shape = "polygon",
			Points = _polygon.Select(MapData.P).ToList(),
			Rough = _roughEdges ? null : false,
		});
		_polygon.Clear();
	}

	private void AddShape(TerrainShape shape)
	{
		PushUndo();
		_data.Terrain.Add(shape);
		_view.Terrain.Refresh();
	}

	private void MoveShapeOrder(TerrainShape shape, bool toFront)
	{
		PushUndo();
		_data.Terrain.Remove(shape);
		if (toFront)
			_data.Terrain.Add(shape);
		else
			_data.Terrain.Insert(0, shape);
		_view.Terrain.Refresh();
	}

	// ---------------------------------------------------------------- layer 2: objects

	private void ObjectPress(bool left, Vector2 pos)
	{
		switch (_objectTool)
		{
			case ObjectTool.Place:
				if (left)
				{
					PushUndo();
					var obj = NewObject(pos);
					_data.Objects.Add(obj);
					_view.Objects.Refresh(pos.Y, pos.Y);
				}
				else
				{
					var hit = _view.Objects.ObjectAt(pos);
					if (hit != null)
						RemoveObjects(new[] { hit });
				}
				break;
			case ObjectTool.Scatter:
			case ObjectTool.Erase:
				PushUndo();
				_painting = true;
				_strokeErases = !left || _objectTool == ObjectTool.Erase;
				OnDrag(pos);
				break;
			case ObjectTool.Select:
				_selectedObject = _view.Objects.ObjectAt(pos);
				StartDrag(pos, _selectedObject != null && left);
				if (!left && _selectedObject != null)
					DeleteSelection();
				RebuildRightPanel();
				break;
		}
	}

	private MapObject NewObject(Vector2 pos)
	{
		var type = MapCatalog.ObjectTypes[_objectType];
		bool nature = type.Category == "Nature";
		return new MapObject
		{
			Type = _objectType,
			Pos = MapData.P(pos),
			Scale = MathF.Round(_objectScale * (nature ? 0.85f + (float)_rng.NextDouble() * 0.3f : 1f), 2),
			Variant = _rng.Next(100),
			Flip = nature && _rng.Next(2) == 0,
		};
	}

	private void ScatterAt(Vector2 center)
	{
		var type = MapCatalog.ObjectTypes[_objectType];
		float spacing = (type.ScatterSpacing > 0 ? type.ScatterSpacing : type.Size * 0.8f) * _objectScale / _density;
		int attempts = Math.Clamp((int)(_brushRadius * _brushRadius / (spacing * spacing) * 0.3f), 1, 30);
		var nearby = _data.Objects.Where(o => MapData.V(o.Pos).DistanceTo(center) < _brushRadius + spacing).ToList();
		float minY = float.MaxValue, maxY = float.MinValue;
		for (int i = 0; i < attempts; i++)
		{
			var p = center + Vector2.FromAngle((float)_rng.NextDouble() * Mathf.Tau) * MathF.Sqrt((float)_rng.NextDouble()) * _brushRadius;
			if (nearby.Any(o => MapData.V(o.Pos).DistanceTo(p) < spacing))
				continue;
			var obj = NewObject(p);
			_data.Objects.Add(obj);
			nearby.Add(obj);
			minY = Math.Min(minY, p.Y);
			maxY = Math.Max(maxY, p.Y);
		}
		if (minY <= maxY)
			_view.Objects.Refresh(minY, maxY);
	}

	private void EraseObjectsAt(Vector2 center)
	{
		var hits = _data.Objects.Where(o => MapData.V(o.Pos).DistanceTo(center) <= _brushRadius && (!_eraseOnlyType || o.Type == _objectType)).ToList();
		if (hits.Count > 0)
		{
			foreach (var o in hits)
				_data.Objects.Remove(o);
			_view.Objects.Refresh(center.Y - _brushRadius, center.Y + _brushRadius);
		}
	}

	private void RemoveObjects(IEnumerable<MapObject> objects)
	{
		PushUndo();
		foreach (var o in objects.ToList())
		{
			_data.Objects.Remove(o);
			_view.Objects.Refresh(o.Pos[1], o.Pos[1]);
		}
	}

	// ---------------------------------------------------------------- layer 3: fields

	private void FieldPress(bool left, Vector2 pos, bool shift)
	{
		var hit = _fieldOverlay.FieldAt(pos);
		// Shift+click or right-click a field: connect / disconnect it from the selected one
		if (hit != null && _selectedField != null && hit != _selectedField && (shift || !left) && _fieldTool != FieldTool.Delete)
		{
			ToggleLink(_selectedField, hit);
			return;
		}
		switch (_fieldTool)
		{
			case FieldTool.Select:
				SelectField(hit);
				StartDrag(pos, hit != null && left);
				break;
			case FieldTool.Add:
				if (hit != null)
				{
					SelectField(hit);
					StartDrag(pos, left);
					break;
				}
				if (!left)
					break;
				PushUndo();
				var (type, reason) = SuggestFieldType(pos);
				var field = new FieldData { Id = _data.NewFieldId(), Type = type, Pos = MapData.P(pos) };
				_data.Fields.Add(field);
				// Chain building: link the new field to the one selected before
				if (_selectedField != null && _chainFields)
					_data.ToggleConnection(_selectedField.Id, field.Id);
				SelectField(field);
				SetStatus($"Added {field.Id} as {type}{reason}");
				break;
			case FieldTool.Connect:
				if (hit != null)
				{
					_fieldOverlay.LinkFrom = hit;
					_fieldOverlay.LinkTo = pos;
				}
				break;
			case FieldTool.Delete:
				if (hit != null)
				{
					PushUndo();
					_data.RemoveField(hit);
					if (_selectedField == hit)
						SelectField(null);
					_fieldOverlay.QueueRedraw();
				}
				break;
		}
	}

	private void FinishLink(Vector2 pos)
	{
		var from = _fieldOverlay.LinkFrom;
		_fieldOverlay.LinkFrom = null;
		var to = _fieldOverlay.FieldAt(pos);
		if (to != null && to != from)
			ToggleLink(from, to);
		_fieldOverlay.QueueRedraw();
	}

	private void ToggleLink(FieldData a, FieldData b)
	{
		PushUndo();
		bool linked = _data.ToggleConnection(a.Id, b.Id);
		SetStatus(linked ? $"Connected {a.Id} - {b.Id}" : $"Disconnected {a.Id} - {b.Id}");
		_fieldOverlay.QueueRedraw();
		RebuildRightPanel();
	}

	private void SelectField(FieldData field)
	{
		_selectedField = field;
		_fieldOverlay.Selected = field;
		_fieldOverlay.QueueRedraw();
		RebuildRightPanel();
	}

	// Field type for a new field: taken from a building next to it, or from the trees around it
	private (string type, string reason) SuggestFieldType(Vector2 pos)
	{
		MapObject best = null;
		float bestDist = 70f;
		var trees = new Dictionary<string, int>();
		foreach (var obj in _data.Objects)
		{
			var type = MapCatalog.ObjectTypes.GetValueOrDefault(obj.Type);
			if (type?.SuggestsField == null)
				continue;
			float d = MapData.V(obj.Pos).DistanceTo(pos);
			if (type.Category == "Nature")
			{
				if (d < 80f)
					trees[type.SuggestsField] = trees.GetValueOrDefault(type.SuggestsField) + 1;
			}
			else if (d < bestDist)
			{
				best = obj;
				bestDist = d;
			}
		}
		if (best != null)
			return (MapCatalog.ObjectTypes[best.Type].SuggestsField, $" (next to a {MapCatalog.ObjectTypes[best.Type].Name.ToLower()})");
		var forest = trees.OrderByDescending(t => t.Value).FirstOrDefault();
		if (forest.Value >= 5)
			return (forest.Key, " (in a forest)");
		return ("GrassPlains", "");
	}

	// ---------------------------------------------------------------- selection (all layers)

	private void StartDrag(Vector2 pos, bool canDrag)
	{
		_dragging = canDrag;
		_dragUndoPushed = false;
		_dragLast = pos;
	}

	private void DragSelection(Vector2 pos)
	{
		var delta = pos - _dragLast;
		if (delta == Vector2.Zero)
			return;
		if (!_dragUndoPushed)
		{
			PushUndo();
			_dragUndoPushed = true;
		}
		_dragLast = pos;
		if (_layer == Layer.Terrain && _selectedShape != null)
		{
			if (_selectedShape.Points != null)
				foreach (var p in _selectedShape.Points)
				{
					p[0] = MathF.Round(p[0] + delta.X, 1);
					p[1] = MathF.Round(p[1] + delta.Y, 1);
				}
			if (_selectedShape.Center != null)
				_selectedShape.Center = MapData.P(MapData.V(_selectedShape.Center) + delta);
			_view.Terrain.Invalidate(_selectedShape);
			_view.Terrain.Refresh();
		}
		else if (_layer == Layer.Objects && _selectedObject != null)
		{
			float oldY = _selectedObject.Pos[1];
			_selectedObject.Pos = MapData.P(MapData.V(_selectedObject.Pos) + delta);
			_view.Objects.Refresh(Math.Min(oldY, _selectedObject.Pos[1]), Math.Max(oldY, _selectedObject.Pos[1]));
		}
		else if (_layer == Layer.Fields && _selectedField != null)
		{
			_selectedField.Pos = MapData.P(MapData.V(_selectedField.Pos) + delta);
			_fieldOverlay.QueueRedraw();
		}
	}

	private void DeleteSelection()
	{
		if (_layer == Layer.Terrain && _selectedShape != null)
		{
			PushUndo();
			_data.Terrain.Remove(_selectedShape);
			_selectedShape = null;
			_view.Terrain.Refresh();
		}
		else if (_layer == Layer.Objects && _selectedObject != null)
		{
			RemoveObjects(new[] { _selectedObject });
			_selectedObject = null;
		}
		else if (_layer == Layer.Fields && _selectedField != null)
		{
			PushUndo();
			_data.RemoveField(_selectedField);
			SelectField(null);
			return;
		}
		RebuildRightPanel();
	}

	// ---------------------------------------------------------------- tool overlay

	private void DrawOverlay(CanvasItem ci)
	{
		var mouse = _mouse;
		float px = Px;
		switch (_layer)
		{
			case Layer.Terrain:
				DrawTerrainOverlay(ci, mouse, px);
				break;
			case Layer.Objects:
				if (_objectTool == ObjectTool.Place)
					ObjectArt.Draw(ci, new MapObject { Type = _objectType, Pos = MapData.P(mouse), Scale = _objectScale }, 0.55f);
				else if (_objectTool is ObjectTool.Scatter or ObjectTool.Erase)
					ci.DrawArc(mouse, _brushRadius, 0, Mathf.Tau, 64, _objectTool == ObjectTool.Erase || _strokeErases ? new Color(1, 0.4f, 0.3f) : Accent, 1.5f * px, true);
				if (_selectedObject != null)
				{
					var rect = ObjectArt.Bounds(_selectedObject);
					rect.Position += MapData.V(_selectedObject.Pos);
					ci.DrawRect(rect, Accent, false, 1.5f * px);
				}
				break;
			case Layer.Fields:
				if (_fieldTool == FieldTool.Add && _fieldOverlay.FieldAt(mouse) == null)
					ci.DrawArc(mouse, FieldOverlay.FieldRadius, 0, Mathf.Tau, 32, new Color(1, 1, 1, 0.6f), 1.5f * px, true);
				break;
		}
	}

	private void DrawTerrainOverlay(CanvasItem ci, Vector2 mouse, float px)
	{
		var type = MapCatalog.Terrain(_stroke != null ? PaintType : _terrainType);
		var color = type.Light;
		color.A = 0.75f;
		switch (_terrainTool)
		{
			case TerrainTool.Brush:
				if (_stroke != null)
				{
					foreach (var p in _stroke)
						ci.DrawCircle(p, _brushWidth / 2f, color);
					if (_stroke.Count > 1)
						ci.DrawPolyline(_stroke.ToArray(), color, _brushWidth);
				}
				ci.DrawArc(mouse, _brushWidth / 2f, 0, Mathf.Tau, 48, Accent, 1.5f * px, true);
				break;
			case TerrainTool.Fill:
				if (_stroke != null && _stroke.Count > 1)
				{
					var closed = new List<Vector2>(_stroke) { _stroke[0] };
					ci.DrawPolyline(closed.ToArray(), Accent, 2f * px, true);
				}
				ci.DrawCircle(mouse, 3 * px, Accent);
				break;
			case TerrainTool.Polygon:
				if (_polygon.Count > 0)
				{
					var line = new List<Vector2>(_polygon) { mouse };
					ci.DrawPolyline(line.ToArray(), Accent, 2f * px, true);
					foreach (var p in _polygon)
						ci.DrawCircle(p, 4 * px, Accent);
					ci.DrawArc(_polygon[0], 10 * px, 0, Mathf.Tau, 24, Accent, 1f * px, true);
				}
				ci.DrawCircle(mouse, 3 * px, Accent);
				break;
		}
		if (_selectedShape != null)
			DrawShapeOutline(ci, _selectedShape, px);
	}

	private static void DrawShapeOutline(CanvasItem ci, TerrainShape shape, float px)
	{
		if (shape.Shape == "circle" && shape.Center != null)
		{
			ci.DrawArc(MapData.V(shape.Center), shape.Radius, 0, Mathf.Tau, 64, Accent, 2f * px, true);
			return;
		}
		if (shape.Points == null || shape.Points.Count < 2)
			return;
		var pts = TerrainGeometry.ToVectors(shape.Points).ToList();
		if (shape.Shape == "polygon")
			pts.Add(pts[0]);
		ci.DrawPolyline(pts.ToArray(), new Color(0, 0, 0, 0.6f), 4f * px, true);
		ci.DrawPolyline(pts.ToArray(), Accent, 2f * px, true);
	}

	private partial class Overlay : Node2D
	{
		public MapEditor Editor;
		public override void _Draw() => Editor.DrawOverlay(this);
	}
}
