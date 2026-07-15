using Godot;

namespace Meridian.Data;

/// <summary>Weighted, data-authored edge in the deterministic weather state machine.</summary>
[GlobalClass]
public partial class WeatherTransitionResource : Resource
{
    [Export] public string TargetWeatherId { get; set; } = "clear";
    [Export(PropertyHint.Range, "0.0,100.0,0.05")] public float Weight { get; set; } = 1f;
    [Export(PropertyHint.Range, "0.0,1.0,0.05")] public float Intensity { get; set; } = 1f;
    [Export(PropertyHint.Range, "1,10080,1")] public int MinDurationGameMinutes { get; set; } = 120;
    [Export(PropertyHint.Range, "1,10080,1")] public int MaxDurationGameMinutes { get; set; } = 360;
    [Export(PropertyHint.Range, "0.0,120.0,0.5")] public float TransitionSeconds { get; set; } = 8f;
}
