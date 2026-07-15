using Godot;

namespace Meridian.Data.Indexes;

[GlobalClass]
public partial class NpcIndex : Resource
{
    [Export] public Godot.Collections.Array<NpcDefinition> Definitions { get; set; } = new();
}
