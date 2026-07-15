using System.Collections.Generic;
using Godot;
using Meridian.NPC;

namespace Meridian.Data;

[GlobalClass]
public partial class NpcDefinition : Resource, INpcDefinition
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public string DialogueId { get; set; } = "";
    [Export] public string FactionId { get; set; } = "";
    [Export] public Godot.Collections.Array<NpcScheduleEntryResource> ScheduleEntries { get; set; } = new();

    public IReadOnlyList<NpcScheduleEntry> Schedule
    {
        get
        {
            var result = new List<NpcScheduleEntry>();
            foreach (var entry in ScheduleEntries)
            {
                if (entry is not null)
                {
                    result.Add(new NpcScheduleEntry(
                        entry.StartHour,
                        entry.EndHour,
                        entry.Activity,
                        entry.Position.X,
                        entry.Position.Y,
                        entry.Position.Z));
                }
            }

            return result;
        }
    }
}
