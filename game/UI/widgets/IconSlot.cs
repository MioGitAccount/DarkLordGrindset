using Godot;
using System;

// Square icon button used for units and abilities. The tooltip is pulled from a
// provider on hover, so it always shows current values (e.g. a unit's health).
public partial class IconSlot : Button
{
	private Func<string> _tooltipProvider;

	public void Setup(Texture2D icon, Func<string> tooltipProvider)
	{
		Icon = icon;
		_tooltipProvider = tooltipProvider;
		// Godot only starts the tooltip timer when there is some tooltip text
		TooltipText = tooltipProvider?.Invoke() ?? "";
	}

	public override string _GetTooltip(Vector2 atPosition)
	{
		return _tooltipProvider?.Invoke() ?? TooltipText;
	}
}
