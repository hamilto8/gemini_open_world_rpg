using Godot;

namespace Meridian.World;

/// <summary>
/// Service view of the world streamer, so consumers depend on an interface rather than
/// the concrete <see cref="WorldStreamerNode"/> (Section 3.5).
/// </summary>
public interface IWorldStreamer
{
    /// <summary>Id of the region currently being streamed, or null if none is active.</summary>
    string? CurrentRegionId { get; }

    /// <summary>
    /// Hands a runtime-spawned <see cref="IDynamicSceneObject"/> (dropped item, parked vehicle) to
    /// streaming: it is parented under its position's cell and captured/respawned with it (§4.3).
    /// The owning cell must currently be instanced. No-op with a warning for ineligible nodes.
    /// </summary>
    void RegisterDynamicObject(Node3D node);

    /// <summary>
    /// Removes a previously registered dynamic object from streaming tracking (e.g. it was picked
    /// back up and now lives in an inventory). The caller keeps ownership of the node.
    /// </summary>
    /// <returns>True when the node was tracked and has been forgotten.</returns>
    bool UnregisterDynamicObject(Node3D node);
}
