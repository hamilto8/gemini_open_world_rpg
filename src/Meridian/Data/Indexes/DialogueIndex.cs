using Godot;

namespace Meridian.Data.Indexes;

[GlobalClass]
public partial class DialogueIndex : Resource
{
    [Export] public Godot.Collections.Array<DialogueDefinition> Definitions { get; set; } = new();
}
