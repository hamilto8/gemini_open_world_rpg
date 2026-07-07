using Godot;
using System;
using System.Collections.Generic;
using Meridian.Core;
using Meridian.Input;

namespace Meridian.UI;

/// <summary>
/// Escape-toggled pause menu: Resume / Controls / Debug Console / Quit. Pauses the SceneTree and frees
/// the cursor while open. The Controls page rebinds keyboard actions (persisted via
/// <see cref="InputRebindStore"/>) and shows the fixed Xbox controller layout. Enforces Section 17 (input)
/// and the GameDirector Paused state (Section 3.4).
/// </summary>
public partial class PauseMenu : Control
{
    private Control? _menuRoot;
    private Control? _mainPage;
    private Control? _controlsPage;
    private Label? _hintLabel;
    private DebugConsole? _console;

    private readonly Dictionary<string, Button> _rebindButtons = new();
    private string? _rebindingAction;
    private bool _isOpen;
    private bool _showingControls;

    public override void _Ready()
    {
        // Must keep processing while the tree is paused so its buttons and input work.
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore; // pass-through when closed; the overlay blocks when open

        _console = GetParent()?.GetNodeOrNull<DebugConsole>("DebugConsole");

        BuildHint();
        BuildMenu();

        GD.Print("[PauseMenu] Press ESC for the pause menu (controls / console / quit).");
    }

    public override void _Input(InputEvent @event)
    {
        // Capturing a key for a rebind takes priority over the toggle.
        if (_rebindingAction != null)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
            {
                if (keyEvent.PhysicalKeycode == Key.Escape)
                {
                    CancelRebind();
                }
                else
                {
                    InputRebindStore.Rebind(_rebindingAction, keyEvent.PhysicalKeycode);
                    CompleteRebind();
                }
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if (@event.IsActionPressed("menu_open"))
        {
            if (!_isOpen)
            {
                Open();
            }
            else if (_showingControls)
            {
                ShowMainPage(); // Escape backs out of the controls page first
            }
            else
            {
                Close();
            }
            GetViewport().SetInputAsHandled();
        }
    }

    // --- Open / close -----------------------------------------------------------------------------

    private void Open()
    {
        _isOpen = true;
        ShowMainPage();
        if (_menuRoot != null) _menuRoot.Visible = true;
        if (_hintLabel != null) _hintLabel.Visible = false;

        GetTree().Paused = true;
        Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;

        PushUiContext();
        Transition(GameState.Paused);
    }

    private void Close()
    {
        _isOpen = false;
        _showingControls = false;
        _rebindingAction = null;
        if (_menuRoot != null) _menuRoot.Visible = false;
        if (_hintLabel != null) _hintLabel.Visible = true;

        PopUiContext();
        GetTree().Paused = false;
        Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Captured;

        Transition(GameState.Playing);
    }

    private void ShowMainPage()
    {
        _showingControls = false;
        _rebindingAction = null;
        if (_controlsPage != null) _controlsPage.Visible = false;
        if (_mainPage != null) _mainPage.Visible = true;
    }

    private void ShowControlsPage()
    {
        _showingControls = true;
        RefreshAllKeyButtons();
        if (_mainPage != null) _mainPage.Visible = false;
        if (_controlsPage != null) _controlsPage.Visible = true;
    }

    // --- Button handlers --------------------------------------------------------------------------

    private void OnResume() => Close();

    private void OnControls() => ShowControlsPage();

    private void OnConsole()
    {
        Close();
        _console?.Open();
    }

    private void OnQuit() => GetTree().Quit();

    // --- Rebinding --------------------------------------------------------------------------------

    private void BeginRebind(string action)
    {
        _rebindingAction = action;
        if (_rebindButtons.TryGetValue(action, out var button))
        {
            button.Text = "Press a key…";
        }
    }

    private void CancelRebind()
    {
        string? action = _rebindingAction;
        _rebindingAction = null;
        if (action != null) RefreshKeyButton(action);
    }

    private void CompleteRebind()
    {
        string? action = _rebindingAction;
        _rebindingAction = null;
        if (action != null) RefreshKeyButton(action);
    }

    private void RefreshAllKeyButtons()
    {
        foreach (var (action, _) in InputActions.Rebindable)
        {
            RefreshKeyButton(action);
        }
    }

    private void RefreshKeyButton(string action)
    {
        if (_rebindButtons.TryGetValue(action, out var button))
        {
            button.Text = KeyText(action);
        }
    }

    private static string KeyText(string action)
    {
        Key key = InputRebindStore.GetBoundKey(action);
        return key == Key.None ? "—" : OS.GetKeycodeString(key);
    }

    // --- Service helpers --------------------------------------------------------------------------

    private static void PushUiContext()
    {
        if (Services.TryGet<IInputContextService>(out var service) && service != null)
        {
            service.PushContext(InputContextType.UI);
        }
    }

    private static void PopUiContext()
    {
        if (Services.TryGet<IInputContextService>(out var service) && service != null)
        {
            service.PopContext();
        }
    }

    private static void Transition(GameState state)
    {
        if (Services.TryGet<IGameDirector>(out var director) && director != null)
        {
            director.TransitionTo(state);
        }
    }

    // --- UI construction --------------------------------------------------------------------------

    private void BuildHint()
    {
        _hintLabel = new Label
        {
            Text = "[ESC] Menu     [`] Console",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _hintLabel.SetAnchorsPreset(LayoutPreset.BottomLeft);
        _hintLabel.OffsetLeft = 12;
        _hintLabel.OffsetTop = -30;
        _hintLabel.OffsetBottom = -8;
        AddChild(_hintLabel);
    }

    private void BuildMenu()
    {
        _menuRoot = new Control { Visible = false, MouseFilter = MouseFilterEnum.Stop };
        _menuRoot.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_menuRoot);

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        _menuRoot.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        _menuRoot.AddChild(center);

        _mainPage = BuildMainPage();
        center.AddChild(_mainPage);

        _controlsPage = BuildControlsPage();
        _controlsPage.Visible = false;
        center.AddChild(_controlsPage);
    }

    private Control BuildMainPage()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(360, 0) };
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        WrapWithMargin(panel, vbox, 24);

        vbox.AddChild(MakeTitle("Paused", 28));
        vbox.AddChild(MakeButton("Resume", OnResume));
        vbox.AddChild(MakeButton("Controls", OnControls));
        vbox.AddChild(MakeButton("Debug Console", OnConsole));
        vbox.AddChild(MakeButton("Quit to Desktop", OnQuit));
        return panel;
    }

    private Control BuildControlsPage()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(660, 0) };
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        WrapWithMargin(panel, vbox, 24);

        vbox.AddChild(MakeTitle("Controls", 24));

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 48);
        vbox.AddChild(columns);

        // Keyboard column (rebindable).
        var keyboard = new VBoxContainer();
        keyboard.AddThemeConstantOverride("separation", 6);
        columns.AddChild(keyboard);
        keyboard.AddChild(MakeTitle("Keyboard  (click to rebind)", 16));
        foreach (var (action, label) in InputActions.Rebindable)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(150, 0) });

            string capturedAction = action;
            var keyButton = new Button { Text = KeyText(action), CustomMinimumSize = new Vector2(130, 0) };
            keyButton.Pressed += () => BeginRebind(capturedAction);
            _rebindButtons[action] = keyButton;
            row.AddChild(keyButton);

            keyboard.AddChild(row);
        }

        // Controller column (fixed reference).
        var controller = new VBoxContainer();
        controller.AddThemeConstantOverride("separation", 6);
        columns.AddChild(controller);
        controller.AddChild(MakeTitle("Controller (Xbox)", 16));
        foreach (var (button, action) in InputActions.ControllerLayout)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new Label { Text = button, CustomMinimumSize = new Vector2(190, 0) });
            row.AddChild(new Label { Text = action });
            controller.AddChild(row);
        }

        vbox.AddChild(MakeButton("Back", ShowMainPage));
        return panel;
    }

    private static void WrapWithMargin(Control panel, Control content, int margin)
    {
        var container = new MarginContainer();
        container.AddThemeConstantOverride("margin_left", margin);
        container.AddThemeConstantOverride("margin_right", margin);
        container.AddThemeConstantOverride("margin_top", margin);
        container.AddThemeConstantOverride("margin_bottom", margin);
        panel.AddChild(container);
        container.AddChild(content);
    }

    private static Label MakeTitle(string text, int fontSize)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    private static Button MakeButton(string text, Action onPressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 36) };
        button.Pressed += onPressed;
        return button;
    }
}
