using System;
using System.Collections.Generic;
using Godot;

namespace Meridian.Audio;

/// <summary>
/// Domain model mapping and matching detected physical materials to step sound paths.
/// Decoupled from Godot for headless unit testing.
/// Enforces Section 22.1 requirements.
/// </summary>
public class FootstepMaterialDetector
{
    private readonly Dictionary<string, string> _materialAudioMap = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterMaterialSfx(string materialId, string sfxPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(materialId);
        ArgumentException.ThrowIfNullOrEmpty(sfxPath);
        _materialAudioMap[materialId] = sfxPath;
    }

    public string GetFootstepSfx(string detectedMaterial)
    {
        if (string.IsNullOrEmpty(detectedMaterial)) return "";
        return _materialAudioMap.TryGetValue(detectedMaterial, out var sfx) ? sfx : "";
    }
}

/// <summary>
/// Node component class that performs floor raycasts and plays correct footstep SFX.
/// </summary>
public partial class FootstepMaterialDetectorNode : RayCast3D
{
    [Export] public Godot.Collections.Array<Data.AudioCueProfile> Profiles { get; set; } = new();

    private readonly FootstepMaterialDetector _detector = new();

    public override void _Ready()
    {
        foreach (var profile in Profiles)
        {
            if (profile != null)
            {
                _detector.RegisterMaterialSfx(profile.MaterialId, profile.FootstepSfxPath);
            }
        }
    }

    // TODO(audio): stub — not yet called by the locomotion system, and material detection hardcodes
    // the "metal"/"dirt" group names. Wire this to the animation/step events and play through the
    // AudioDirector once footstep cues are authored (L9).
    public void PlayFootstep()
    {
        if (!IsColliding()) return;

        var collider = GetCollider();
        if (collider is Node colNode)
        {
            // Simple material tag check: look up group or custom property
            string material = "grass"; // Default fallback
            if (colNode.IsInGroup("metal"))
            {
                material = "metal";
            }
            else if (colNode.IsInGroup("dirt"))
            {
                material = "dirt";
            }

            string sfxPath = _detector.GetFootstepSfx(material);
            if (!string.IsNullOrEmpty(sfxPath))
            {
                GD.Print($"[FootstepDetector] Playing footstep SFX '{sfxPath}' for material '{material}'");
                
                // Under Godot, we play the audio stream:
                // AudioDirector.PlaySfx(sfxPath, GlobalPosition);
            }
        }
    }
}
