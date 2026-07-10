using System.Collections.Generic;

namespace Meridian.Data;

/// <summary>
/// Engine-free view of a region for the registry and validator (ADR-0003): its permanent id plus the cell
/// scene paths the validator resolves against the filesystem (§4.2, §19.10).
/// </summary>
public interface IRegionDefinition
{
    /// <summary>Permanent snake_case id, unique within the region category (§19.9).</summary>
    string Id { get; }

    /// <summary>Non-empty <c>res://</c> scene paths for this region's cells; each must exist on disk.</summary>
    IReadOnlyList<string> CellScenePaths { get; }
}
