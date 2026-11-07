using Godot;
using System;
using System.Collections.Generic;

// Background music. The world map themes play one after another, crossfading into each other;
// while a battle is open the map theme slowly fades out and the battle theme slowly fades in,
// and back again when the battle closes.
// Tracks are rendered by tools/music/render_score.py and built to loop seamlessly.
public partial class MusicPlayer : Node
{
	// (path, volume offset in dB so the themes sound equally loud)
	private static readonly (string Path, float GainDb)[] MapTracks =
	{
		("res://sound/music/beyond_the_ashen_crown.ogg", 0f),
		("res://sound/music/pastoral_morning.ogg", 0f),
		("res://sound/music/morning_over_the_meadows.ogg", 0f),
	};
	private const string BattleTrack = "res://sound/music/march_of_the_black_legion.ogg";
	// Linear volume of the playing theme (1 = full level; the rendered tracks are already mastered softly)
	private const float Volume = 1f;
	private const float FadeOutSeconds = 4f;
	private const float FadeInSeconds = 3.5f;
	// The incoming theme waits a moment so the outgoing one is already quieter
	private const float FadeInDelay = 1f;
	// Seconds before a map theme ends that the next one starts fading in
	private const float NextTrackOverlap = 4f;

	private readonly List<AudioStreamPlayer> _mapPlayers = new();
	private readonly Dictionary<AudioStreamPlayer, float> _gain = new();
	private readonly Dictionary<AudioStreamPlayer, Tween> _fades = new();
	private AudioStreamPlayer _battle;
	private int _mapIndex;
	private bool _inBattle;

	private AudioStreamPlayer Map => _mapPlayers[_mapIndex];

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		foreach (var (path, gainDb) in MapTracks)
		{
			var player = CreatePlayer($"MapMusic{_mapPlayers.Count}", path, gainDb);
			if (player.Stream != null)
				_mapPlayers.Add(player);
		}
		_battle = CreatePlayer("BattleMusic", BattleTrack, 0f);
		if (_mapPlayers.Count == 0)
			return;
		_mapIndex = (int)(GD.Randi() % (uint)_mapPlayers.Count);
		Map.Play();
		Fade(Map, Volume, 2f, 0f);
	}

	// Moves on to the next map theme shortly before the current one ends
	public override void _Process(double delta)
	{
		if (_inBattle || _mapPlayers.Count < 2 || !Map.Playing || Map.StreamPaused)
			return;
		if (Map.Stream.GetLength() - Map.GetPlaybackPosition() > NextTrackOverlap)
			return;
		var previous = Map;
		Fade(previous, 0f, NextTrackOverlap, 0f, () => previous.Stop());
		_mapIndex = (_mapIndex + 1) % _mapPlayers.Count;
		Map.Play();
		Fade(Map, Volume, NextTrackOverlap, 0f);
	}

	public void SetBattle(bool inBattle)
	{
		if (inBattle == _inBattle || _mapPlayers.Count == 0)
			return;
		_inBattle = inBattle;
		var map = Map;
		// The map theme pauses and resumes where it was; each battle starts its theme from the top
		if (inBattle)
		{
			_battle.Play();
			Fade(map, 0f, FadeOutSeconds, 0f, () => map.StreamPaused = true);
			Fade(_battle, Volume, FadeInSeconds, FadeInDelay);
		}
		else
		{
			map.StreamPaused = false;
			Fade(_battle, 0f, FadeOutSeconds, 0f, () => _battle.Stop());
			Fade(map, Volume, FadeInSeconds, FadeInDelay);
		}
	}

	private AudioStreamPlayer CreatePlayer(string name, string path, float gainDb)
	{
		var player = new AudioStreamPlayer { Name = name, VolumeDb = -80f };
		if (GD.Load<AudioStream>(path) is AudioStreamOggVorbis stream)
		{
			stream.Loop = true;
			player.Stream = stream;
		}
		else
			GD.PushWarning($"MusicPlayer: could not load {path} (is it imported?)");
		_gain[player] = Mathf.DbToLinear(gainDb);
		AddChild(player);
		return player;
	}

	// Tweens the player's linear volume (so the fade sounds even) and replaces its running fade
	private void Fade(AudioStreamPlayer player, float to, float seconds, float delay, Action done = null)
	{
		if (_fades.TryGetValue(player, out var running))
			running.Kill();
		float gain = _gain[player];
		var tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(v => player.VolumeDb = Mathf.LinearToDb(Mathf.Max(v * gain, 0.0001f))),
			Mathf.DbToLinear(player.VolumeDb) / gain, to, seconds).SetDelay(delay);
		if (done != null)
			tween.TweenCallback(Callable.From(done));
		_fades[player] = tween;
	}
}
