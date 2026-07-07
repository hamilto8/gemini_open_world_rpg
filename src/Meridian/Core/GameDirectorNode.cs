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
        // Game.tscn is already the running main scene, so just advance the state machine to MainMenu
        // — no redundant scene reload (H6). A typed deferred Callable avoids fragile Variant/overload
        // marshalling through CallDeferred(string, ...).
        Callable.From(() => TransitionTo(GameState.MainMenu)).CallDeferred();
    }

    public void TransitionTo(GameState newState, string? targetScenePath = null)
    {
        GD.Print($"[GameDirector] Transitioning state: {CurrentState} -> {newState}");
        CurrentState = newState;

        // Publish event to EventBus so HUD/UI can react
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new GameStateChangedEvent(CurrentState));
        }

        // Only switch scenes when a target is given and it isn't already the running scene.
        if (!string.IsNullOrEmpty(targetScenePath) && newState != GameState.Boot)
        {
            string? currentScenePath = GetTree().CurrentScene?.SceneFilePath;
            if (currentScenePath != targetScenePath)
            {
                GetTree().ChangeSceneToFile(targetScenePath);
            }
        }
    }
}

/// <summary>
/// Event payload published via the EventBus when game state transitions occur.
/// </summary>
public record struct GameStateChangedEvent(GameState NewState);
