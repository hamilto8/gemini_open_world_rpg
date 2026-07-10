namespace Meridian.Core.Logic;

/// <summary>
/// A "do something" effect applied through an <see cref="IActionContext"/> (§3.6 item 2). Concrete
/// actions are engine-free C# so they are unit-testable; the matching
/// <c>Meridian.Data.GameActionResource</c> wrappers map editor exports onto these types, and the
/// <see cref="ActionDispatcher"/> lets writers trigger them safely by name (§10.2, §10.4).
/// </summary>
public interface IGameAction
{
    /// <summary>Executes this action against the supplied context.</summary>
    void Execute(IActionContext context);
}
