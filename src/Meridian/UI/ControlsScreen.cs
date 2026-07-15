using System.Collections.Generic;
using Godot;
using Meridian.Input;

namespace Meridian.UI;

/// <summary>Scene-authored control list with runtime-generated binding rows and conflict confirmation.</summary>
public partial class ControlsScreen : UIScreen
{
    private VBoxContainer? _bindings;
    private Label? _status;
    private string? _captureAction;
    private Key _pendingKey;
    private string? _pendingConflict;
    private readonly Dictionary<string, Button> _buttons = new();

    public override void _Ready()
    {
        base._Ready();
        _bindings = GetNodeOrNull<VBoxContainer>("Panel/Margin/Rows/Scroll/Bindings");
        _status = GetNodeOrNull<Label>("Panel/Margin/Rows/Status");
        BuildRows();
        Button? defaults = GetNodeOrNull<Button>("Panel/Margin/Rows/Buttons/Defaults");
        if (defaults != null) defaults.Pressed += RestoreDefaults;
        Button? replace = GetNodeOrNull<Button>("Panel/Margin/Rows/Buttons/Replace");
        if (replace != null) replace.Pressed += ReplaceConflict;
    }

    public override void _Input(InputEvent @event)
    {
        if (_captureAction == null || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.PhysicalKeycode == Key.Escape)
        {
            CancelCapture();
        }
        else
        {
            InputRebindResult result = InputRebindStore.Rebind(_captureAction, key.PhysicalKeycode);
            if (result.Applied)
            {
                CompleteCapture(Tr("ui.controls.binding_saved"));
            }
            else if (result.ConflictingAction != null)
            {
                _pendingKey = key.PhysicalKeycode;
                _pendingConflict = result.ConflictingAction;
                if (_status != null)
                    _status.Text = string.Format(Tr("ui.controls.conflict"), LabelFor(result.ConflictingAction));
            }
        }
        GetViewport().SetInputAsHandled();
    }

    private void BuildRows()
    {
        if (_bindings == null) return;
        foreach (Node child in _bindings.GetChildren()) child.QueueFree();
        _buttons.Clear();
        foreach (var (action, label) in InputActions.Rebindable)
        {
            var row = new HBoxContainer();
            var actionLabel = new Label { Text = label, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var button = new Button { Text = BindingText(action), CustomMinimumSize = new Vector2(180, 44) };
            string captured = action;
            button.Pressed += () => BeginCapture(captured);
            row.AddChild(actionLabel);
            row.AddChild(button);
            _bindings.AddChild(row);
            _buttons[action] = button;
        }
    }

    private void BeginCapture(string action)
    {
        _captureAction = action;
        _pendingConflict = null;
        _buttons[action].Text = Tr("ui.controls.press_key");
        if (_status != null) _status.Text = Tr("ui.controls.cancel_hint");
    }

    private void ReplaceConflict()
    {
        if (_captureAction == null || _pendingConflict == null || _pendingKey == Key.None) return;
        InputRebindStore.Rebind(_captureAction, _pendingKey, InputConflictResolution.Replace);
        CompleteCapture(Tr("ui.controls.conflict_replaced"));
    }

    private void CancelCapture() => CompleteCapture(Tr("ui.controls.cancelled"));

    private void CompleteCapture(string status)
    {
        string? action = _captureAction;
        _captureAction = null;
        _pendingConflict = null;
        _pendingKey = Key.None;
        if (action != null && _buttons.TryGetValue(action, out var button)) button.Text = BindingText(action);
        if (_status != null) _status.Text = status;
    }

    private void RestoreDefaults()
    {
        InputRebindStore.RestoreDefaults();
        foreach (var (action, button) in _buttons) button.Text = BindingText(action);
        if (_status != null) _status.Text = Tr("ui.settings.defaults_restored");
    }

    private static string BindingText(string action)
    {
        Key key = InputRebindStore.GetBoundKey(action);
        return key == Key.None ? "—" : OS.GetKeycodeString(key);
    }

    private static string LabelFor(string action)
    {
        foreach (var candidate in InputActions.Rebindable)
            if (candidate.Action == action) return candidate.Label;
        return action;
    }
}
