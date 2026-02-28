using Godot;
using System;
using System.Collections.Generic;


public partial class PanelContainerForResources : PanelContainer
{
	[Export] public PackedScene ResourcePanelScene;
	private Control _resourceListContainer;

	public override void _Ready()
	{
		_resourceListContainer = GetNode<Control>("HBoxContainer");
	}

	public void ShowResources(List<ResourceInstance> resources)
	{
		//_resourceListContainer.QueueFreeChildren();
		foreach (var child in _resourceListContainer.GetChildren())
			child.QueueFree();

		if(resources == null) return;

		foreach (var resource in resources)
		{
			var entry = ResourcePanelScene.Instantiate<ResourcePanel>();
			entry.Setup(resource);
			// Forward child signal
			
			_resourceListContainer.AddChild(entry);
		}
	}
	
}
