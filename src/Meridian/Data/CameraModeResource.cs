using Godot;

namespace Meridian.Data;

/// <summary>
/// Resource definition for a camera mode profile (Explore, Aim, Vehicle, etc.).
/// Enforces Section 5.4 requirements.
/// </summary>
[GlobalClass]
public partial class CameraModeResource : Resource
{
    [Export] public Vector3 PivotOffset { get; set; } = new Vector3(0, 0.5f, 0);
    [Export] public float SpringLength { get; set; } = 3.0f;
    [Export] public float Fov { get; set; } = 75.0f;
    [Export] public float ShoulderOffset { get; set; } = 0.5f; // Side alignment
    [Export] public float SmoothSpeed { get; set; } = 15.0f; // Interpolation weight

    /// <summary>Look sensitivity (radians per pixel of mouse motion) for this mode — data, not hardcoded.</summary>
    [Export] public float MouseSensitivity { get; set; } = 0.003f;
}
