namespace Meridian.Core;

/// <summary>
/// Interface for the PlayerController. Manages active possession of possessable entities.
/// </summary>
public interface IPlayerController
{
    /// <summary>
    /// Currently possessed entity (e.g. PlayerAvatar, Vehicle).
    /// </summary>
    IPossessable? PossessedEntity { get; }

    /// <summary>
    /// Possesses the target entity, releasing any previously possessed entity.
    /// </summary>
    void Possess(IPossessable entity);

    /// <summary>
    /// Releases possession of the current entity.
    /// </summary>
    void Release();
}
