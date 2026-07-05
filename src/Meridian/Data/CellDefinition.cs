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
}
