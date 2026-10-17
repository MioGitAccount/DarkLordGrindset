using Godot;
using System.Collections.Generic;

// Resource icon + amount. Either shows a fixed Amount (player totals in the top bar)
// or is bound to a field's ResourceInstance and follows its changes.
public partial class ResourceCounter : HBoxContainer
{
	private static readonly Dictionary<ResourceType, string> IconPaths = new()
	{
		{ ResourceType.Wood, "res://images/resources/wood.png" },
		{ ResourceType.Gold, "res://images/resources/gold.png" },
	};

	[Export] public ResourceType Type = ResourceType.Gold;
	[Export] public float Amount;

	private ResourceInstance _bound;
	private TextureRect _icon;
	private Label _label;

	public static Texture2D GetIcon(ResourceType type)
	{
		return IconPaths.TryGetValue(type, out var path) ? GD.Load<Texture2D>(path) : null;
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
			TooltipText = $"{Type}: +{_bound.RegenRate:0.#} per tick";
		}
		else
		{
			_label.Text = $"{Amount:0}";
			TooltipText = Type.ToString();
		}
	}
}
