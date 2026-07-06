using Godot;
using System.Collections.Generic;

namespace Meridian.Data;

/// <summary>
/// Data-driven definition Resource mapping floor material tags to step sound clip paths.
/// Enforces Section 22.1 requirements.
/// </summary>
[GlobalClass]
public partial class AudioCueProfile : Resource
{
    [Export] public string MaterialId { get; set; } = "grass";
    [Export] public string FootstepSfxPath { get; set; } = "res://assets/audio/sfx/footsteps_grass.wav";
}
