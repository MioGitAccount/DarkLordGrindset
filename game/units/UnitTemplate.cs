using Godot;
using System;
[GlobalClass]
public partial class UnitTemplate : Resource
{

	[Export] public string Name = "Imp";
	[Export] public Texture2D Icon;
	// Animated map sprite with "idle" and "walk" animations; units without one show Icon instead
	[Export] public SpriteFrames MapSprite;

	[Export] public AudioStream SelectSound;
	[Export] public BattleStats DefaultStats;
}
