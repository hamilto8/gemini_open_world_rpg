using Godot;
using Meridian.Core;

namespace Meridian.Audio;

/// <summary>
/// Autoload Node implementing AudioDirector.
/// </summary>
public partial class AudioDirectorNode : Node, IAudioDirector
{
    public override void _EnterTree()
    {
        Services.Register<IAudioDirector>(this);
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
        GD.Print($"[AudioDirector] Playing sound cue: {cueId}");
    }
}
