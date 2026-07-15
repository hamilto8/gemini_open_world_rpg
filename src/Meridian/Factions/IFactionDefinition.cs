namespace Meridian.Factions;

/// <summary>Stable authored faction metadata.</summary>
public interface IFactionDefinition
{
    string Id { get; }
    string DisplayName { get; }
    int MinimumReputation { get; }
    int MaximumReputation { get; }
    int StartingReputation { get; }
}
