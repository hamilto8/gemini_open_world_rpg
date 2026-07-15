using Godot;
using System.Collections.Generic;

namespace Meridian.Data;

/// <summary>
/// Data-driven definition Resource for a World Region.
/// Implements <see cref="IRegionDefinition"/> for registry/validator decoupling (ADR-0003).
/// Enforces Section 4.2 requirements.
/// </summary>
[GlobalClass]
public partial class RegionDefinition : Resource, IRegionDefinition
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public float CellSize { get; set; } = 256.0f; // Dimension of one tile in meters
    [Export] public Rect2I Bounds { get; set; } = new Rect2I(0, 0, 4, 4); // Grid coordinate span
    [Export] public int StreamPriority { get; set; } = 10;

    [ExportGroup("Authoring Metadata")]
    [Export] public string BiomeId { get; set; } = "temperate";
    [Export] public Godot.Collections.Array<string> Tags { get; set; } = new();
    [Export] public string DefaultWeatherId { get; set; } = "clear";
    [Export] public string AmbientAudioCueId { get; set; } = "";

    [ExportGroup("Streaming Budgets")]
    [Export(PropertyHint.Range, "1,1024,1")] public int MaxResidentCells { get; set; } = 96;
    [Export(PropertyHint.Range, "1,2048,1")] public int MaxSimulationCost { get; set; } = 128;

    [Export] public Godot.Collections.Array<CellDefinition> Cells { get; set; } = new();

    // Projects the non-empty cell scene paths so the validator can check each exists on disk (§19.10).
    IReadOnlyList<string> IRegionDefinition.CellScenePaths
    {
        get
        {
            var paths = new List<string>(Cells.Count);
            foreach (var cell in Cells)
            {
                if (cell != null && !string.IsNullOrEmpty(cell.ScenePath))
                {
                    paths.Add(cell.ScenePath);
                }
                if (cell != null && !string.IsNullOrEmpty(cell.CollisionScenePath))
                {
                    paths.Add(cell.CollisionScenePath);
                }
            }
            return paths;
        }
    }
}
