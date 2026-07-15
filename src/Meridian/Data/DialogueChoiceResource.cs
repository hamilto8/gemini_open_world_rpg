using Godot;

namespace Meridian.Data;

[GlobalClass]
public partial class DialogueChoiceResource : Resource
{
    [Export] public string Text { get; set; } = "";
    [Export] public string TargetNodeId { get; set; } = "end";
    [Export] public Godot.Collections.Array<ConditionResource> Conditions { get; set; } = new();
    [Export] public Godot.Collections.Array<GameActionResource> Actions { get; set; } = new();
}
