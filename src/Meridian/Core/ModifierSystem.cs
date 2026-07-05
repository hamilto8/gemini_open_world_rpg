using System;
using System.Collections.Generic;
using System.Linq;

namespace Meridian.Core;

/// <summary>
/// Centralized stat math system.
/// Calculates final stat values given a base value and active modifiers.
/// </summary>
public static class ModifierSystem
{
    /// <summary>
    /// Calculates the modified value of a stat based on a list of active modifiers.
    /// Order of operations: Base -> Add -> PercentAdd -> Multiply -> Override.
    /// </summary>
    public static float Calculate(float baseValue, IEnumerable<Modifier> modifiers)
    {
        float adds = 0.0f;
        float percentAdds = 0.0f;
        float multiplies = 1.0f;
        float? overrideValue = null;

        foreach (var mod in modifiers)
        {
            switch (mod.Operation)
            {
                case ModifierOp.Add:
                    adds += mod.Value;
                    break;
                case ModifierOp.PercentAdd:
                    percentAdds += mod.Value;
                    break;
                case ModifierOp.Multiply:
                    multiplies *= mod.Value;
                    break;
                case ModifierOp.Override:
                    // If multiple overrides exist, take the last one applied
                    overrideValue = mod.Value;
                    break;
            }
        }

        if (overrideValue.HasValue)
        {
            return overrideValue.Value;
        }

        float result = baseValue + adds;
        result *= (1.0f + (percentAdds / 100.0f));
        result *= multiplies;

        return result;
    }
}
