using Godot;

namespace Meridian.Data.Indexes;

[GlobalClass]
public partial class FactionIndex : Resource
{
    [Export] public Godot.Collections.Array<FactionDefinition> Definitions { get; set; } = new();
}
