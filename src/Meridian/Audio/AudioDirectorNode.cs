using Godot;
using System;
using Meridian.Core;
using Meridian.Core.Registry;

namespace Meridian.Audio;

/// <summary>
/// Autoload Node implementing AudioDirector.
/// </summary>
public partial class AudioDirectorNode : Node, IAudioDirector
{
    private const int PoolSize = 16;
    private readonly AudioStreamPlayer[] _players = new AudioStreamPlayer[PoolSize];

    public override void _EnterTree()
    {
        Services.Register<IAudioDirector>(this);
    }

    public override void _Ready()
    {
        for (int index = 0; index < _players.Length; index++)
        {
            var player = new AudioStreamPlayer { Name = $"CuePlayer{index + 1}" };
            AddChild(player);
            _players[index] = player;
        }
    }

    public override void _ExitTree()
    {
        if (Services.TryGet<IAudioDirector>(out var current) && ReferenceEquals(current, this))
        {
            Services.Unregister<IAudioDirector>();
        }
    }

    public void SetVolume(string busName, float volumeDb)
    {
        int busIndex = AudioServer.GetBusIndex(busName);
        if (busIndex != -1)
        {
            AudioServer.SetBusVolumeDb(busIndex, volumeDb);
            GD.Print($"[AudioDirector] Set volume for bus '{busName}' to {volumeDb} dB");
        }
        else
        {
            GD.PrintErr($"[AudioDirector] Bus '{busName}' not found.");
        }
    }

    public float GetVolume(string busName)
    {
        int busIndex = AudioServer.GetBusIndex(busName);
        if (busIndex != -1)
        {
            return AudioServer.GetBusVolumeDb(busIndex);
        }
        return -80.0f; // Muted fallback
    }

    public void PlaySoundCue(string cueId)
    {
        if (string.IsNullOrWhiteSpace(cueId)) return;

        string streamPath = cueId;
        string busName = "SFX";
        float volumeDb = 0f;
        float minPitch = 1f;
        float maxPitch = 1f;
        if (Services.TryGet<IContentDatabase>(out var content)
            && content != null
            && content.SoundCues.TryGet(cueId, out var cue)
            && cue != null)
        {
            streamPath = cue.StreamPath;
            busName = cue.BusName;
            volumeDb = cue.VolumeDb;
            minPitch = cue.MinPitch;
            maxPitch = cue.MaxPitch;
        }

        AudioStream? stream = ResourceLoader.Load<AudioStream>(streamPath);
        if (stream == null)
        {
            GD.PushWarning($"[AudioDirector] Unknown or unloadable sound cue '{cueId}'.");
            return;
        }

        AudioStreamPlayer? player = null;
        foreach (var candidate in _players)
        {
            if (!candidate.Playing)
            {
                player = candidate;
                break;
            }
        }
        player ??= _players[0];
        player.Stop();
        player.Stream = stream;
        player.Bus = ResolveBus(busName);
        player.VolumeDb = volumeDb;
        player.PitchScale = (float)GD.RandRange(Math.Min(minPitch, maxPitch), Math.Max(minPitch, maxPitch));
        player.Play();
    }

    private static string ResolveBus(string requested)
        => AudioServer.GetBusIndex(requested) >= 0 ? requested : "Master";
}
