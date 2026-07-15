using Godot;

namespace Meridian.Data;

[GlobalClass]
public partial class DialogueNodeResource : Resource
{
    [Export] public string NodeId { get; set; } = "";
    [Export] public string Speaker { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string Text { get; set; } = "";
    [Export] public Godot.Collections.Array<DialogueChoiceResource> Choices { get; set; } = new();
}
