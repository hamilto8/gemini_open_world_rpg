namespace Meridian.Audio;

/// <summary>
/// Interface for the global AudioDirector.
/// Manages mixing buses (Master, Music, Ambience, SFX, UI, Voice) and triggers SoundCues.
/// </summary>
public interface IAudioDirector
{
    void SetVolume(string busName, float volumeDb);
    float GetVolume(string busName);
    void PlaySoundCue(string cueId);
}
