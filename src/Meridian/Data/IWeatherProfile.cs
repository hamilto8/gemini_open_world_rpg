namespace Meridian.Data;

/// <summary>
/// Engine-free view of a weather profile for the registry and validator (ADR-0003) exposing its permanent
/// id (§19.10). The underlying export stays <c>WeatherId</c>; ids are snake_case and permanent (§19.9).
/// </summary>
public interface IWeatherProfile
{
    /// <summary>Permanent snake_case id, unique within the weather-profile category (§19.9).</summary>
    string Id { get; }
}
