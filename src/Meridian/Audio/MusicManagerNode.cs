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
    private readonly MusicManager _manager = new();

    public float Tension => _manager.Tension;
    public float ExplorationVolumeDb => _manager.ExplorationVolumeDb;
    public float CombatVolumeDb => _manager.CombatVolumeDb;

    public override void _EnterTree()
    {
        Services.Register<IMusicManager>(this);
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

        // TODO(audio): stub — the crossfade volumes are computed but not yet applied to real buses.
        // Wire up once the ExplorationBus/CombatBus audio buses exist (L9):
        // AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("ExplorationBus"), ExplorationVolumeDb);
        // AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("CombatBus"), CombatVolumeDb);

        GD.Print($"[MusicManager] Tension updated to {Tension:P0}. Exploration: {ExplorationVolumeDb:F1}dB, Combat: {CombatVolumeDb:F1}dB");
    }
}
