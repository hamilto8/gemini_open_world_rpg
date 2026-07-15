using Godot;

namespace Meridian.Data.Indexes;

[GlobalClass]
public partial class QuestIndex : Resource
{
    [Export] public Godot.Collections.Array<QuestDefinition> Definitions { get; set; } = new();
}
