namespace Meridian.World;

/// <summary>Engine-free fast-travel point metadata.</summary>
public interface IFastTravelPointDefinition
{
    string Id { get; }
    string DisplayName { get; }
    float X { get; }
    float Y { get; }
    float Z { get; }
    bool DiscoveredByDefault { get; }
}
