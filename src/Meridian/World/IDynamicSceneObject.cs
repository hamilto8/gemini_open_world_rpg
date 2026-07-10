namespace Meridian.World;

/// <summary>
/// A runtime-spawned persistent object (dropped item, parked vehicle) that streaming must recreate
/// when its cell reloads. Authored <see cref="IPersistentSceneObject"/>s already exist in the cell
/// scene and only get their state re-applied; dynamic ones additionally record which PackedScene to
/// respawn from plus their transform (doc §4.3). Register with
/// <see cref="IWorldStreamer.RegisterDynamicObject"/> after spawning.
/// </summary>
public interface IDynamicSceneObject : IPersistentSceneObject
{
    /// <summary>res:// path of the PackedScene this object is instantiated from.</summary>
    string SceneFilePath { get; }
}
