using Godot;

namespace Meridian.Data.Indexes;

[GlobalClass]
public partial class FastTravelIndex : Resource
{
    [Export] public Godot.Collections.Array<FastTravelPointDefinition> Definitions { get; set; } = new();
}
