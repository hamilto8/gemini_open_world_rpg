using Godot;
using System;
using Meridian.Core;

namespace Meridian.Audio;

/// <summary>
/// Autoload Node managing dynamic tension audio states and crossfading Exploration vs Combat track stems.
/// Delegates calculations to the pure C# MusicManager.
/// Enforces Section 19.1 requirements.
/// </summary>
public partial class MusicManagerNode : Node
{
    private readonly MusicManager _manager = new();

    public float Tension => _manager.Tension;
    public float ExplorationVolumeDb => _manager.ExplorationVolumeDb;
    public float CombatVolumeDb => _manager.CombatVolumeDb;

    public override void _EnterTree()
    {
        Services.Register<MusicManagerNode>(this);
    }

    public void SetTension(float targetTension)
    {
        _manager.SetTension(targetTension);
        
        // Under Godot, we would update AudioServer bus volume:
        // AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("ExplorationBus"), ExplorationVolumeDb);
        // AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("CombatBus"), CombatVolumeDb);
        
        GD.Print($"[MusicManager] Tension updated to {Tension:P0}. Exploration: {ExplorationVolumeDb:F1}dB, Combat: {CombatVolumeDb:F1}dB");
    }
}
