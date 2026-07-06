using System;

namespace Meridian.Audio;

/// <summary>
/// Pure C# domain logic for Dynamic Tension audio stem calculations. Decoupled from Godot.
/// Enforces Section 3.2 and 19.1 requirements.
/// </summary>
public class MusicManager
{
    private float _tension = 0f; // 0 = Exploration, 1 = Combat
    private float _explorationVolumeDb = 0f;
    private float _combatVolumeDb = -80f;

    public float Tension => _tension;
    public float ExplorationVolumeDb => _explorationVolumeDb;
    public float CombatVolumeDb => _combatVolumeDb;

    public void SetTension(float targetTension)
    {
        _tension = Math.Clamp(targetTension, 0f, 1f);
        
        // Calculate crossfades (Section 19.1 dynamic stem layers)
        // Lerp exploration volume from 0 dB (full) down to -40 dB
        _explorationVolumeDb = Lerp(0f, -40f, _tension);
        
        // Lerp combat volume from -80 dB (muted) up to 0 dB (full)
        _combatVolumeDb = Lerp(-80f, 0f, _tension);
    }

    private static float Lerp(float start, float end, float t)
    {
        return start + (end - start) * t;
    }
}
