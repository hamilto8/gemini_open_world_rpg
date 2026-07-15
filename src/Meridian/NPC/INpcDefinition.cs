using System.Collections.Generic;

namespace Meridian.NPC;

/// <summary>Engine-free NPC identity and daily schedule definition.</summary>
public interface INpcDefinition
{
    string Id { get; }
    string DisplayName { get; }
    string DialogueId { get; }
    string FactionId { get; }
    IReadOnlyList<NpcScheduleEntry> Schedule { get; }
}

public readonly record struct NpcScheduleEntry(
    int StartHour,
    int EndHour,
    NpcActivityState Activity,
    float X,
    float Y,
    float Z);
