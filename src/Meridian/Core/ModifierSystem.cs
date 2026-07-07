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
    /// Calculates the modified value of a stat from a base value and its active modifiers.
    /// Application order: <c>Base -> Add -> PercentAdd -> Multiply -> Override</c>.
    /// <para>
    /// <see cref="ModifierOp.PercentAdd"/> values are <b>fractions</b>: <c>0.15</c> means +15%,
    /// <c>-0.15</c> means −15%. This is the single project-wide convention — every call site
    /// (perks, gear, weather) authors percentages as fractions.
    /// </para>
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
        result *= 1.0f + percentAdds; // percentAdds is a summed fraction (0.15 = +15%)
        result *= multiplies;

        return result;
    }
}
