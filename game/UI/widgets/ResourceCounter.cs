using Godot;
using System.Collections.Generic;

// Resource icon + amount. Either shows a fixed Amount (player totals in the top bar)
// or is bound to a field's ResourceInstance and follows its changes.
public partial class ResourceCounter : HBoxContainer
{
	private static readonly Dictionary<ResourceType, Texture2D> Icons = new();

	[Export] public ResourceType Type = ResourceType.Gold;
	[Export] public float Amount;

	private ResourceInstance _bound;
	private TextureRect _icon;
	private Label _label;

	// Icons live at res://images/resources/<type in lower case>.png
	public static Texture2D GetIcon(ResourceType type)
	{
		if (!Icons.TryGetValue(type, out var icon))
		{
			string path = $"res://images/resources/{type.ToString().ToLowerInvariant()}.png";
			icon = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
			Icons[type] = icon;
		}
		return icon;
	}

	public override void _Ready()
	{
		_icon = GetNode<TextureRect>("Icon");
		_label = GetNode<Label>("Amount");
		Refresh();
	}

	public override void _EnterTree()
	{
		if (_bound != null)
			_bound.OnAmountChanged += OnAmountChanged;
	}

	public override void _ExitTree()
	{
		if (_bound != null)
			_bound.OnAmountChanged -= OnAmountChanged;
	}

	// Call before adding the counter to the tree
	public void Bind(ResourceInstance resource)
	{
		_bound = resource;
		Type = resource.type;
	}

	public void SetAmount(float amount)
	{
		Amount = amount;
		Refresh();
	}

	private void OnAmountChanged(float newAmount)
	{
		Refresh();
	}

	private void Refresh()
	{
		if (_label == null)
			return;

		_icon.Texture = GetIcon(Type);
		if (_bound != null)
		{
			_label.Text = $"{_bound.CurrentAmount:0} / {_bound.MaxAmount:0}";
			TooltipText = $"{Type}: +{_bound.RegenRate:0.##} per tick, holds up to {_bound.MaxAmount:0}";
		}
		else
		{
			// Floor, so 0.5 mana collected doesn't already read as 1
			_label.Text = $"{Mathf.Floor(Amount):0}";
			TooltipText = Type.ToString();
		}
	}
}
