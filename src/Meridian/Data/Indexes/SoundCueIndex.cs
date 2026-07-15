using Godot;

namespace Meridian.Data.Indexes;

[GlobalClass]
public partial class SoundCueIndex : Resource
{
    [Export] public Godot.Collections.Array<SoundCueResource> Definitions { get; set; } = new();
}
