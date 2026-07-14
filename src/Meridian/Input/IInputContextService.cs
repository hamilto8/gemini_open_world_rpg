using System;

namespace Meridian.Input;

/// <summary>
/// Predefined input contexts in Project Meridian.
/// </summary>
public enum InputContextType
{
    OnFoot,
    Vehicle,
    UI,
    Dialogue,
    MapView,
    Console
}

/// <summary>
/// Service managing active input contexts via a stack.
/// Ensures clean input isolation (e.g. gameplay input is blocked while in UI or Dialogue).
/// </summary>
public interface IInputContextService
{
    /// <summary>
    /// Gets the currently active input context at the top of the stack.
    /// </summary>
    InputContextType CurrentContext { get; }

    /// <summary>
    /// Pushes a new input context onto the stack, making it the active context.
    /// </summary>
    void PushContext(InputContextType context);

    /// <summary>
    /// Pops the top input context from the stack.
    /// </summary>
    void PopContext();

    /// <summary>
    /// Pops only when <paramref name="expectedContext"/> is currently on top. Modal owners use this
    /// to avoid accidentally removing another system's context when close events arrive out of order.
    /// </summary>
    bool TryPopContext(InputContextType expectedContext);

    /// <summary>
    /// Checks if a named input action is permitted under the currently active context.
    /// </summary>
    bool IsActionAllowed(string action);

    /// <summary>
    /// Registers an action name with a specific context.
    /// </summary>
    void RegisterActionForContext(InputContextType context, string action);

    /// <summary>
    /// Resets the input context stack to default (OnFoot).
    /// </summary>
    void Reset();
}
