using Godot;

namespace Meridian.World;

/// <summary>
/// Interface representing cell loading operations.
/// Permits unit tests to mock and simulate cell instancing.
/// Enforces Section 3.2 and 4.3 requirements.
/// </summary>
public interface ICellLoader
{
    /// <summary>
    /// Initiates a background loading request for a cell scene.
    /// </summary>
    void RequestLoad(string scenePath);

    /// <summary>
    /// Checks if a cell scene has completed loading.
    /// </summary>
    bool IsLoadComplete(string scenePath);

    /// <summary>
    /// Instantiates and returns the loaded cell scene node.
    /// </summary>
    Node? InstantiateCell(string scenePath);
}
