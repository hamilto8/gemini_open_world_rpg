using Godot;

namespace Meridian.Data.Indexes;

[GlobalClass]
public partial class ProgressionProfileIndex : Resource
{
    [Export] public Godot.Collections.Array<ProgressionProfile> Definitions { get; set; } = new();
}
