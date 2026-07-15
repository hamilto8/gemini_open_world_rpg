using System.Collections.Generic;
using Meridian.Core.Logic;

namespace Meridian.Environment;

/// <summary>Engine-free scheduled world event authored as conditions plus actions.</summary>
public interface IScheduledEventDefinition
{
    string Id { get; }
    int Hour { get; }
    int Minute { get; }
    bool IsRecurring { get; }
    IReadOnlyList<ICondition> Conditions { get; }
    IReadOnlyList<IGameAction> Actions { get; }
}
