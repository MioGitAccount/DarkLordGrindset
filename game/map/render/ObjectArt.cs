using Godot;
using System.Collections.Generic;

// Draws map objects with the painted sprites from ObjectSprites.
// Put a PNG at res://images/map_objects/<type>.png (object standing on the bottom middle
// of the image, trimmed, see tools/sprites/clean_icon.gd) and it is drawn instead, scaled to
// the type's Size. More looks: <type>_1.png, <type>_2.png, ... (picked by the object's variant).
public static class ObjectArt
{
	private static readonly Dictionary<string, Texture2D[]> Textures = new();

	// Rectangle (relative to the object's position) the object covers, for picking it in the editor
	public static Rect2 Bounds(MapObject obj)
	{
		var type = MapCatalog.ObjectTypes.GetValueOrDefault(obj.Type ?? "");
		float s = (type?.Size ?? 20f) * obj.Scale;
		var custom = type != null ? CustomTexture(type.Id, obj.Variant) : null;
		var units = custom != null
			? new Rect2(-custom.GetWidth() / (2f * custom.GetHeight()), -1f, custom.GetWidth() / (float)custom.GetHeight(), 1f)
			: ObjectSprites.UnitsOf(obj.Type);
		if (type?.Turnable == true)
		{
			// It turns, so be generous
			float r = Mathf.Max(units.Size.X, units.Size.Y) * s / 2f;
			return new Rect2(-r, -r, r * 2, r * 2);
		}
		return new Rect2(units.Position * s, units.Size * s);
	}

	public static void Draw(CanvasItem ci, MapObject obj, float alpha = 1f)
	{
		var type = MapCatalog.ObjectTypes.GetValueOrDefault(obj.Type ?? "");
		if (type == null)
		{
			ci.DrawCircle(MapData.V(obj.Pos), 6, new Color(1, 0, 1, alpha));
			return;
		}
		float s = type.Size * obj.Scale;
		ci.DrawSetTransform(MapData.V(obj.Pos), type.Turnable ? Mathf.DegToRad(obj.Rot) : 0f, new Vector2(obj.Flip ? -s : s, s));
		var custom = CustomTexture(type.Id, obj.Variant);
		if (custom != null)
		{
			float aspect = custom.GetWidth() / (float)custom.GetHeight();
			ci.DrawTextureRect(custom, new Rect2(-aspect / 2f, -1f, aspect, 1f), false, new Color(1, 1, 1, alpha));
		}
		else
		{
			var sprite = ObjectSprites.Get(type.Id, obj.Variant);
			ci.DrawTextureRect(sprite.Texture, sprite.Units, false, new Color(1, 1, 1, alpha));
		}
		ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
	}

	private static Texture2D CustomTexture(string id, int variant)
	{
		if (!Textures.TryGetValue(id, out var looks))
		{
			var found = new List<Texture2D>();
			string path = $"res://images/map_objects/{id}.png";
			if (ResourceLoader.Exists(path))
				found.Add(GD.Load<Texture2D>(path));
			for (int i = 1; ResourceLoader.Exists($"res://images/map_objects/{id}_{i}.png"); i++)
				found.Add(GD.Load<Texture2D>($"res://images/map_objects/{id}_{i}.png"));
			looks = found.ToArray();
			Textures[id] = looks;
		}
		if (looks.Length == 0)
			return null;
		return looks[((variant % looks.Length) + looks.Length) % looks.Length];
	}
}
