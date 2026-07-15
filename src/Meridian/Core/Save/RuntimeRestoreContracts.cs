using Godot;

namespace Meridian.Core.Save;

/// <summary>
/// Scene/world bridge used by player restoration. Implemented by composition code so the save domain
/// does not know how regions stream or where possessable nodes live.
/// </summary>
public interface IPlayerRestoreCoordinator
{
    /// <summary>Starts or completes collision-first warm-up for the saved region and position.</summary>
    void PrepareRegion(string regionId, Vector3 worldPosition);

    /// <summary>Returns a stable authoring/runtime id for the currently possessed entity.</summary>
    string GetPersistentId(IPossessable possessable);

    /// <summary>Resolves a warmed-up on-foot avatar or vehicle by stable id.</summary>
    IPossessable? ResolvePossessable(string persistentId);
}

/// <summary>Adapter contract implemented by persistent authored and runtime-spawned vehicles.</summary>
public interface IPersistentVehicle
{
    string PersistentVehicleId { get; }
    string VehicleDefinitionId { get; }

    VehicleStateDto CaptureVehicleState(string currentRegionId, bool isPlayerPossessed);
    void RestoreVehicleState(VehicleStateDto state);
}
