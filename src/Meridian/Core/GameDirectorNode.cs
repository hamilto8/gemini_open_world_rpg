using Godot;

namespace Meridian.Core;

/// <summary>
/// Autoload Node implementing IGameDirector.
/// Manages high-level state transitions and scene loading.
/// </summary>
public partial class GameDirectorNode : Node, IGameDirector
{
    public GameState CurrentState { get; private set; } = GameState.Boot;

    public override void _EnterTree()
    {
        Services.Register<IGameDirector>(this);
    }

    public override void _Ready()
    {
        GD.Print("[GameDirector] Booting...");
        // Auto transition from Boot to MainMenu in Phase 0
        CallDeferred(nameof(TransitionTo), (int)GameState.MainMenu, "res://scenes/game/Game.tscn");
    }

    public void TransitionTo(GameState newState, string? targetScenePath = null)
    {
        GD.Print($"[GameDirector] Transitioning state: {CurrentState} -> {newState}");
        CurrentState = newState;

        // Publish event to EventBus so HUD/UI can react
        var eventBus = Services.Get<IEventBus>();
        eventBus.Publish(new GameStateChangedEvent(CurrentState));

        if (!string.IsNullOrEmpty(targetScenePath) && CurrentState != GameState.Boot)
        {
            // Perform Godot scene switch
            GetTree().ChangeSceneToFile(targetScenePath);
        }
    }

    // Overload for CallDeferred which doesn't support enum casting directly
    private void TransitionTo(int stateInt, string? targetScenePath)
    {
        TransitionTo((GameState)stateInt, targetScenePath);
    }
}

/// <summary>
/// Event payload published via the EventBus when game state transitions occur.
/// </summary>
public record struct GameStateChangedEvent(GameState NewState);
