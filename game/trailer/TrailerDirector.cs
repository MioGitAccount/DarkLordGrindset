using Godot;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

// Plays the shots of the game trailer on the default map, for recording with Movie Maker
// (see tools/trailer/README.md). Prints "MARK <name> <seconds>" at the start of every shot,
// in game time, so the editing script can cut the recording.
//   map:     cinematic camera moves over the world map, an army marching, the HUD in use
//   battles: staged battles on each biome, player units casting their spells on their own
// Run the scene with `-- map`, `-- battles` or `-- battle_forest` (one battle; add `long` to play
// it to the end); add a folder after it for a screenshot per shot.
public partial class TrailerDirector : Node
{
	private const string Templates = "res://unit_templates/";
	private const string Neutrals = "res://unit_templates/neutral_unit_templates/";
	// Recording stops this long into a battle even if it isn't over ("long": play it out)
	private double _maxBattleSeconds = 38;

	private game _game;
	private map _map;
	private Camera2D _camera;
	private UIManager _ui;
	private BattleScreen _battle;
	private string _shots = "";
	private double _time;
	private string _only = "battles";

	public override async void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always; // battles pause the tree
		var args = OS.GetCmdlineUserArgs();
		string mode = args.Length > 0 ? args[0] : "map";
		if (args.Contains("long"))
		{
			_maxBattleSeconds = 120;
			args = args.Where(a => a != "long").ToArray();
		}
		_shots = args.Length > 1 ? args[1] : "";

		MapSelection.Path = MapSelection.DefaultMap;
		_game = GD.Load<PackedScene>(MapSelection.GameScene).Instantiate<game>();
		AddChild(_game);
		_map = _game.GetNode<map>("Map");
		_camera = _game.GetNode<Camera2D>("CameraController");
		_ui = _game.GetNode<UIManager>("UI");
		_battle = _ui.FindChild("Battle", true, false) as BattleScreen;
		_battle.AutoCastPlayerAbilities = true;
		RevealMap();
		SetHud(false);
		await Wait(0.5);

		if (mode.StartsWith("battle"))
			await Battles(mode);
		else
			await MapShots();
		Mark("end");
		await Wait(0.5);
		GetTree().Quit();
	}

	public override void _Process(double delta) => _time += delta;

	// --- World map -------------------------------------------------------------

	private async Task MapShots()
	{
		var castle = Field("Field31");

		// 1. Wide view of the land, drifting
		Mark("map_wide");
		await Fly(castle.Position + new Vector2(-500, 350), 0.62f, castle.Position + new Vector2(150, -50), 0.7f, 7);

		// 2. Push in on the castle
		Mark("map_castle");
		await Fly(castle.Position + new Vector2(-220, 120), 0.9f, castle.Position, 1.7f, 5);

		// 3. The army marches out of the castle toward the forest
		var forest = Field("Field19");
		var army = new List<unit>();
		foreach (var name in new[] { "orc", "imp", "witch", "orc" })
			army.Add(Spawn(name, castle, 5));
		await Wait(0.2);
		Mark("map_march");
		for (int i = 0; i < army.Count; i++)
		{
			army[i].MoveAlongPath(PathFindingUtil.FindPath(castle, forest));
			await Wait(0.35);
		}
		await Follow(army[0], 2f, 7);

		// 4. Dangerous places: elven woods, dwarf mines, villages
		foreach (var (id, shot) in new[] { ("Field19", "map_forest"), ("F28", "map_mine"), ("Field6", "map_village") })
		{
			var place = Field(id);
			Mark(shot);
			await Fly(place.Position + new Vector2(-160, 60), 1.5f, place.Position + new Vector2(60, -20), 1.75f, 4);
		}

		// 5. Playing: the HUD with a field and a unit selected
		SetHud(true);
		_ui.ShowField(forest);
		_ui.ShowUnit(army[2]);
		Mark("map_hud");
		await Fly(forest.Position + new Vector2(-40, 30), 1.3f, forest.Position, 1.4f, 5);
		SetHud(false);
	}

	// Moves and zooms the camera from one framing to another
	private async Task Fly(Vector2 from, float fromZoom, Vector2 to, float toZoom, double seconds)
	{
		_camera.Position = from;
		_camera.Zoom = Vector2.One * fromZoom;
		var tween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(_camera, "position", to, seconds);
		tween.TweenProperty(_camera, "zoom", Vector2.One * toZoom, seconds);
		await Shot();
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private async Task Follow(Node2D target, float zoom, double seconds)
	{
		_camera.Zoom = Vector2.One * zoom;
		double end = _time + seconds;
		bool shot = false;
		while (_time < end)
		{
			_camera.Position = _camera.Position.Lerp(target.GlobalPosition, 0.06f);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (!shot && _time > end - seconds / 2)
			{
				shot = true;
				await Shot();
			}
		}
	}

	// --- Battles ---------------------------------------------------------------

	// "battles" plays them all, "battle_forest" (etc.) just that one
	private async Task Battles(string only)
	{
		_only = only;
		await Battle("battle_forest", "Field19",
			new[] { ("orc", 10), ("orc", 9), ("imp", 11), ("witch", 11) },
			new[] { "wolf" });
		await Battle("battle_mine", "F28",
			new[] { ("orc", 10), ("orc", 10), ("imp", 11), ("witch", 11), ("imp", 10) },
			new string[0]);
		await Battle("battle_village", "Field6",
			new[] { ("orc", 10), ("witch", 10), ("imp", 10), ("imp", 10) },
			new[] { "village_sergeant", "night_watchman", "villager_archer" });
		await Battle("battle_bridge", "Field9",
			new[] { ("orc", 8), ("imp", 10), ("imp", 10), ("witch", 10) },
			new[] { "wolf", "wolf", "wolf", "elf_blade_dancer" });
		await Battle("battle_plains", "Field12",
			new[] { ("orc", 10), ("orc", 9), ("witch", 10), ("imp", 10) },
			new[] { "druid", "treant", "elf_archer", "elf_archer" });
	}

	private async Task Battle(string shot, string fieldId, (string, int)[] army, string[] extraNeutrals)
	{
		if (_only != "battles" && _only != shot)
			return;
		var place = Field(fieldId);
		foreach (var name in extraNeutrals)
			place.presentNeutralUnits.Add(new NeutralUnit { Template = GD.Load<UnitTemplate>(Neutrals + name + ".tres") });
		foreach (var (name, level) in army)
			Spawn(name, place, level);
		_camera.Position = place.Position;
		await Wait(0.3);

		_battle.Open(place);
		Mark(shot);
		await Wait(1.2);
		await Shot();
		Press("Start battle");
		await Wait(0.2);
		// 2x is real-time pace; the speed button keeps its setting between battles
		for (int i = 0; i < 3 && Button("Speed 2x") == null; i++)
			Press("Speed");
		double end = _time + _maxBattleSeconds;
		while (_time < end && Button("Return to map") == null)
		{
			await Wait(3);
			await Shot();
		}
		await Wait(2.5);
		await Shot();
		Mark(shot + "_end");
		Press("Return to map");
		await Wait(0.3);
	}

	private void Press(string textStart)
	{
		var button = Button(textStart);
		button?.EmitSignal(BaseButton.SignalName.Pressed);
	}

	private Button Button(string textStart) =>
		_battle.FindChildren("*", "Button", true, false).OfType<Button>()
			.FirstOrDefault(b => b.Text.StartsWith(textStart) && b.IsVisibleInTree());

	// --- Helpers ---------------------------------------------------------------

	private field Field(string id) => _map.GetNode<field>(id);

	// A player unit of the given type, levelled up with every skill it can learn
	private unit Spawn(string templateName, field place, int level)
	{
		var newUnit = _map.SpawnUnit(GD.Load<UnitTemplate>(Templates + templateName + ".tres"), place, level);
		if (newUnit.SkillTree != null)
		{
			bool learned = true;
			while (learned)
			{
				learned = false;
				foreach (var skill in newUnit.SkillTree.Skills)
					learned |= newUnit.Progression.TryUnlock(skill);
			}
		}
		return newUnit;
	}

	// No fog: the whole map is shown as seen
	private void RevealMap()
	{
		var fog = _map.GetNode<Node2D>("FogOfWar");
		fog.ProcessMode = ProcessModeEnum.Disabled;
		fog.Visible = false;
		foreach (var place in _map.GetChildren().OfType<field>())
			place.UpdateFog(FogState.Visible);
	}

	// The map HUD; the battle screen (also under HUD) is left alone
	private void SetHud(bool visible)
	{
		foreach (var name in new[] { "TopBar", "FieldInfo", "SelectedUnit" })
			if (_ui.GetNodeOrNull<Control>("HUD/" + name) is { } panel)
				panel.Visible = visible;
		// Follows the real mouse, which isn't part of the trailer
		if (_ui.GetNodeOrNull<Control>("HUD/FieldHoverCard") is { } hover)
			hover.Modulate = Colors.Transparent;
	}

	private void Mark(string name) => GD.Print("MARK " + name + " " + _time.ToString("0.000", CultureInfo.InvariantCulture));

	private async Task Wait(double seconds) =>
		await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);

	private async Task Shot()
	{
		if (_shots == "")
			return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(_shots + "/" + _time.ToString("000.0", CultureInfo.InvariantCulture) + ".png");
	}
}
