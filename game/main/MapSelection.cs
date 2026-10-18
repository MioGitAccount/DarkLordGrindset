// Which map the game plays, and what the editor had open when it started a test play
public static class MapSelection
{
	public const string DefaultMap = "res://maps/deathland.json";
	public const string GameScene = "res://game/main/game.tscn";
	public const string EditorScene = "res://game/editor/map_editor.tscn";

	// File of the map to play / edit
	public static string Path = DefaultMap;
	// Map handed over in memory (editor "Play" with unsaved changes); wins over Path when set
	public static MapData Data;
	// Set while a map is being test-played from the editor, so the game can go back to it
	public static bool FromEditor;
	public static bool EditorDirty;

	public static MapData LoadSelected() => Data?.Clone() ?? MapData.Load(Path);
}
