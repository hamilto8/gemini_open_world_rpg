namespace Meridian.Audio;

/// <summary>
/// Service interface for the dynamic music manager, so consumers depend on an interface rather than
/// the concrete <see cref="MusicManagerNode"/> (Section 3.5, L1).
/// </summary>
public interface IMusicManager
{
    /// <summary>Current tension (0 = exploration, 1 = combat).</summary>
    float Tension { get; }

    /// <summary>Sets the target tension, crossfading exploration/combat stems.</summary>
    void SetTension(float targetTension);
}
