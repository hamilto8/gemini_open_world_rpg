using Godot;
using Meridian.World;

namespace Meridian.Data;

[GlobalClass]
public partial class FastTravelPointDefinition : Resource, IFastTravelPointDefinition
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public Vector3 Position { get; set; }
    [Export] public bool DiscoveredByDefault { get; set; }

    public float X => Position.X;
    public float Y => Position.Y;
    public float Z => Position.Z;
}
