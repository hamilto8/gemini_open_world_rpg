using Godot;

namespace Meridian.Data.Indexes;

[GlobalClass]
public partial class ScheduledEventIndex : Resource
{
    [Export] public Godot.Collections.Array<ScheduledEventDefinition> Definitions { get; set; } = new();
}
