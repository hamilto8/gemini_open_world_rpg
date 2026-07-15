using Godot;
using System;
using Meridian.Core;

namespace Meridian.Audio;

/// <summary>
/// Autoload Node managing dynamic tension audio states and crossfading Exploration vs Combat track stems.
/// Delegates calculations to the pure C# MusicManager.
/// Enforces Section 19.1 requirements.
/// </summary>
public partial class MusicManagerNode : Node, IMusicManager
{
    [Export] public AudioStream? ExplorationStream { get; set; }
    [Export] public AudioStream? CombatStream { get; set; }
    [Export] public string MusicBus { get; set; } = "Music";

    private readonly MusicManager _manager = new();
    private AudioStreamPlayer? _explorationPlayer;
    private AudioStreamPlayer? _combatPlayer;

    public float Tension => _manager.Tension;
    public float ExplorationVolumeDb => _manager.ExplorationVolumeDb;
    public float CombatVolumeDb => _manager.CombatVolumeDb;

    public override void _EnterTree()
    {
        Services.Register<IMusicManager>(this);
    }

    public override void _Ready()
    {
        string bus = AudioServer.GetBusIndex(MusicBus) >= 0 ? MusicBus : "Master";
        _explorationPlayer = CreateStem("ExplorationStem", ExplorationStream, bus);
        _combatPlayer = CreateStem("CombatStem", CombatStream, bus);
        ApplyMix();
    }

    public override void _ExitTree()
    {
        if (Services.TryGet<IMusicManager>(out var current) && ReferenceEquals(current, this))
        {
            Services.Unregister<IMusicManager>();
        }
    }

    public void SetTension(float targetTension)
    {
        _manager.SetTension(targetTension);

        ApplyMix();
    }

    private AudioStreamPlayer? CreateStem(string name, AudioStream? stream, string bus)
    {
        if (stream == null) return null;
        var player = new AudioStreamPlayer { Name = name, Stream = stream, Bus = bus };
        AddChild(player);
        player.Play();
        return player;
    }

    private void ApplyMix()
    {
        if (_explorationPlayer != null) _explorationPlayer.VolumeDb = ExplorationVolumeDb;
        if (_combatPlayer != null) _combatPlayer.VolumeDb = CombatVolumeDb;
    }
}
