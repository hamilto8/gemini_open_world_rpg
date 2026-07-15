using Godot;
using Meridian.Core;
using Meridian.Dialogue;

namespace Meridian.UI;

/// <summary>Thin presenter for the authored dialogue screen; conversation state stays in DialogueService.</summary>
public partial class DialogueScreen : UIScreen
{
    [Export] public NodePath SpeakerPath { get; set; } = "Bottom/Panel/Margin/Rows/Speaker";
    [Export] public NodePath LinePath { get; set; } = "Bottom/Panel/Margin/Rows/Line";
    [Export] public NodePath ChoicesPath { get; set; } = "Bottom/Panel/Margin/Rows/Choices";

    private Label? _speaker;
    private Label? _line;
    private VBoxContainer? _choices;
    private DialogueService? _dialogue;

    public override void _Ready()
    {
        base._Ready();
        _speaker = GetNodeOrNull<Label>(SpeakerPath);
        _line = GetNodeOrNull<Label>(LinePath);
        _choices = GetNodeOrNull<VBoxContainer>(ChoicesPath);
        Services.TryGet(out _dialogue);
        Refresh();
    }

    private void Refresh()
    {
        if (_choices == null || _speaker == null || _line == null || _dialogue?.CurrentNode == null)
        {
            EmitSignal(SignalName.BackRequested);
            return;
        }

        foreach (Node child in _choices.GetChildren())
        {
            child.QueueFree();
        }

        _speaker.Text = _dialogue.CurrentNode.Speaker;
        _line.Text = _dialogue.CurrentNode.Text;
        var available = _dialogue.AvailableChoices;
        for (int index = 0; index < available.Count; index++)
        {
            int capturedIndex = index;
            var button = new Button { Text = available[index].Text };
            button.Pressed += () => SelectChoice(capturedIndex);
            _choices.AddChild(button);
            if (index == 0)
            {
                button.CallDeferred(Control.MethodName.GrabFocus);
            }
        }
    }

    private void SelectChoice(int choiceIndex)
    {
        if (_dialogue == null || !_dialogue.SelectChoice(choiceIndex)) return;
        if (_dialogue.CurrentNode == null)
        {
            EmitSignal(SignalName.BackRequested);
        }
        else
        {
            Refresh();
        }
    }
}
