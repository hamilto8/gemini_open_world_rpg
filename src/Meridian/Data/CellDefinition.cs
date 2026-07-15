using Godot;

namespace Meridian.Data;

/// <summary>
/// Data-driven definition Resource for a single streaming cell.
/// Enforces Section 4.1 and 4.2 requirements.
/// </summary>
[GlobalClass]
public partial class CellDefinition : Resource
{
    [Export] public Vector2I GridPosition { get; set; } = Vector2I.Zero;
    [Export] public string ScenePath { get; set; } = "";

    /// <summary>
    /// Optional lightweight collision/navigation proxy. When supplied it is loaded and instantiated
    /// before the full visual cell, preventing fast vehicles from outrunning collision data.
    /// </summary>
    [Export(PropertyHint.File, "*.tscn")] public string CollisionScenePath { get; set; } = "";

    /// <summary>Higher values win when multiple cells compete for the same frame budget.</summary>
    [Export(PropertyHint.Range, "-100,100,1")] public int StreamPriority { get; set; }

    /// <summary>Relative physics/AI cost used by the region-wide simulation budget.</summary>
    [Export(PropertyHint.Range, "1,100,1")] public int SimulationCost { get; set; } = 1;

    /// <summary>For small hub/transition cells that must remain resident regardless of distance.</summary>
    [Export] public bool AlwaysLoaded { get; set; }
}
