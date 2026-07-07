namespace Meridian.World;

/// <summary>
/// Read-only service view of the world streamer, so consumers depend on an interface rather than
/// the concrete <see cref="WorldStreamerNode"/> (Section 3.5).
/// </summary>
public interface IWorldStreamer
{
    /// <summary>Id of the region currently being streamed, or null if none is active.</summary>
    string? CurrentRegionId { get; }
}
