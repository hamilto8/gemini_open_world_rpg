namespace Meridian.Data;

/// <summary>
/// Engine-free view of a movement profile for the registry and validator (ADR-0003) exposing its permanent
/// id (§19.10).
/// </summary>
public interface IMovementProfile
{
    /// <summary>Permanent snake_case id, unique within the movement-profile category (§19.9).</summary>
    string Id { get; }
}
