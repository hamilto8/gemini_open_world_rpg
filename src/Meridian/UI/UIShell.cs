using System;
using Godot;
using Meridian.Core;
using Meridian.Input;
using Godot.Collections;

namespace Meridian.UI;

/// <summary>Scene-driven application UI root and the only owner of modal navigation/pause state.</summary>
public partial class UIShell : Control
{
    [Export] public UIScreenRegistry? Registry { get; set; }
    [Export] public Array<Translation> Translations { get; set; } = new();
    [Export] public NodePath ScreenHostPath { get; set; } = "SafeArea/ScreenHost";

    private UIScreenStack? _stack;
    private IDisposable? _screenRequestSubscription;
    private bool _ownsPause;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
        foreach (Translation translation in Translations) TranslationServer.AddTranslation(translation);
        if (Registry == null)
        {
            GD.PushError("[UIShell] No UIScreenRegistry assigned.");
            return;
        }

        foreach (string error in Registry.Validate()) GD.PushError($"[UIShell] {error}");
        _stack = new UIScreenStack(GetNode<Control>(ScreenHostPath), Registry);
        _stack.Changed += SynchronizeModalState;
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            _screenRequestSubscription = eventBus.Subscribe<UIScreenRequestedEvent>(OnScreenRequested);
        }
    }

    public override void _ExitTree()
    {
        _screenRequestSubscription?.Dispose();
        if (_ownsPause && GetTree() != null) GetTree().Paused = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("menu_open"))
        {
            if (_stack?.Current == UIScreenId.Pause) _stack.Pop();
            else if (_stack?.Count > 0) _stack.Clear();
            else _stack?.Push(UIScreenId.Pause);
            GetViewport().SetInputAsHandled();
        }
        else if (_stack?.Count > 0 && @event.IsActionPressed("ui_cancel"))
        {
            _stack.Pop();
            GetViewport().SetInputAsHandled();
        }
    }

    public void ShowMainMenu() => _stack?.Replace(UIScreenId.MainMenu);
    public void ShowLoading() => _stack?.Push(UIScreenId.Loading);
    public void HideLoading()
    {
        if (_stack?.Current == UIScreenId.Loading) _stack.Pop();
    }

    private void OnScreenRequested(UIScreenRequestedEvent request)
    {
        if (request.ReplaceTop) _stack?.Replace(request.Screen);
        else _stack?.Push(request.Screen);
    }

    private void SynchronizeModalState()
    {
        bool shouldPause = false;
        if (_stack?.Current is UIScreenId screen && Registry?.TryGet(screen, out var definition) == true)
        {
            shouldPause = definition?.PausesGame == true;
        }

        if (shouldPause == _ownsPause) return;
        _ownsPause = shouldPause;
        GetTree().Paused = shouldPause;
        MouseFilter = shouldPause ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        Godot.Input.MouseMode = shouldPause ? Godot.Input.MouseModeEnum.Visible : Godot.Input.MouseModeEnum.Captured;

        if (Services.TryGet<IInputContextService>(out var input) && input != null)
        {
            if (shouldPause) input.PushContext(InputContextType.UI);
            else input.TryPopContext(InputContextType.UI);
        }
        if (Services.TryGet<IGameDirector>(out var director) && director != null)
        {
            director.TransitionTo(shouldPause ? GameState.Paused : GameState.Playing);
        }
    }
}
