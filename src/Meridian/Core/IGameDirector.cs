namespace Meridian.Core;

/// <summary>
/// Predefined game states.
/// </summary>
public enum GameState
{
    Boot,
    MainMenu,
    Loading,
    Playing,
    Paused
}

/// <summary>
/// Interface for the global GameDirector.
/// Manages transitions between high-level states (Boot, MainMenu, Loading, Playing, Paused).
/// </summary>
public interface IGameDirector
{
    GameState CurrentState { get; }

    /// <summary>
    /// Transitions the game to a new state, optionally performing scene transitions.
    /// </summary>
    void TransitionTo(GameState newState, string? targetScenePath = null);
}
