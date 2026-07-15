using Godot;

namespace Meridian.World;

public enum CellLoadStatus
{
    NotRequested,
    Loading,
    Loaded,
    Failed
}

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
    /// Returns the complete request state. The default keeps existing test/content loaders compatible;
    /// production loaders should distinguish a failed request so the streamer can retry it.
    /// </summary>
    CellLoadStatus GetLoadStatus(string scenePath)
        => IsLoadComplete(scenePath) ? CellLoadStatus.Loaded : CellLoadStatus.Loading;

    /// <summary>Best-effort cancellation hook for loaders that support cancelling in-flight work.</summary>
    void CancelLoad(string scenePath)
    {
    }

    /// <summary>
    /// Instantiates and returns the loaded cell scene node.
    /// </summary>
    Node? InstantiateCell(string scenePath);
}
