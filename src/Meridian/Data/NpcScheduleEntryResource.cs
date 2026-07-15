using Godot;
using Meridian.NPC;

namespace Meridian.Data;

[GlobalClass]
public partial class NpcScheduleEntryResource : Resource
{
    [Export(PropertyHint.Range, "0,23,1")] public int StartHour { get; set; }
    [Export(PropertyHint.Range, "0,23,1")] public int EndHour { get; set; } = 23;
    [Export] public NpcActivityState Activity { get; set; } = NpcActivityState.Sleeping;
    [Export] public Vector3 Position { get; set; }
}
