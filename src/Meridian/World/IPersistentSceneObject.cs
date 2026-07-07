using System.Collections.Generic;

namespace Meridian.World;

/// <summary>
/// Implemented by dynamic cell objects (containers, doors, parked vehicles) whose runtime state must
/// survive streaming. The streamer walks a cell's descendants for these and captures/restores their
/// state keyed by <see cref="PersistentId"/> — a stable GUID/id, not the node name (doc §4.3/§16, M13).
/// </summary>
public interface IPersistentSceneObject
{
    /// <summary>Stable identifier for this object, independent of node name or tree position.</summary>
    string PersistentId { get; }

    /// <summary>Captures this object's dynamic state as string key/value pairs.</summary>
    Dictionary<string, string> CaptureState();

    /// <summary>Restores previously captured state.</summary>
    void RestoreState(Dictionary<string, string> state);
}
