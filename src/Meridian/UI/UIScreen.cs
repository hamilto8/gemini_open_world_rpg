using Godot;

namespace Meridian.UI;

/// <summary>
/// Thin behavior shared by authored screens. Designers wire navigation by adding buttons to the
/// ui_back or ui_open_screen groups and setting target_screen metadata on open buttons.
/// </summary>
public partial class UIScreen : Control
{
    [Signal] public delegate void BackRequestedEventHandler();
    [Signal] public delegate void ScreenRequestedEventHandler(int screenId);
    [Export] public NodePath DefaultFocusPath { get; set; } = new();

    public override void _Ready()
    {
        foreach (var node in GetTree().GetNodesInGroup("ui_back"))
        {
            if (IsAncestorOf(node) && node is Button button)
            {
                button.Pressed += () => EmitSignal(SignalName.BackRequested);
            }
        }
        foreach (var node in GetTree().GetNodesInGroup("ui_open_screen"))
        {
            if (IsAncestorOf(node) && node is Button button && button.HasMeta("target_screen"))
            {
                int target = button.GetMeta("target_screen").AsInt32();
                button.Pressed += () => EmitSignal(SignalName.ScreenRequested, target);
            }
        }
        foreach (var node in GetTree().GetNodesInGroup("ui_quit"))
        {
            if (IsAncestorOf(node) && node is Button button)
            {
                button.Pressed += () => GetTree().Quit();
            }
        }
    }

    public void FocusDefault()
    {
        Control? target = DefaultFocusPath.IsEmpty ? FindFirstFocusable(this) : GetNodeOrNull<Control>(DefaultFocusPath);
        target?.GrabFocus();
    }

    private static Control? FindFirstFocusable(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is Control { Visible: true, FocusMode: not FocusModeEnum.None } control)
                return control;
            Control? nested = FindFirstFocusable(child);
            if (nested != null) return nested;
        }
        return null;
    }
}
