namespace Meridian.Core.Logic;

/// <summary>
/// A boolean predicate over an <see cref="IConditionContext"/> (§3.6 item 1). Concrete conditions are
/// plain, engine-free C# so they can be unit-tested headlessly; the matching
/// <c>Meridian.Data.ConditionResource</c> wrappers map editor exports onto these types.
/// </summary>
/// <remarks>
/// Contract: <see cref="Evaluate"/> must never throw. Conditions constructed with null/empty or
/// otherwise meaningless arguments evaluate to <c>false</c> rather than raising.
/// </remarks>
public interface ICondition
{
    /// <summary>Evaluates this condition against the supplied context.</summary>
    bool Evaluate(IConditionContext context);
}
