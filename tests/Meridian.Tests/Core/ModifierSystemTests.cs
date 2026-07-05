using System;
using Xunit;
using Meridian.Core;

namespace Meridian.Tests.Core;

public class ModifierSystemTests
{
    [Fact]
    public void Calculate_ShouldApplyMathInCorrectOrder()
    {
        // Order: Base -> Add -> PercentAdd -> Multiply -> Override
        float baseVal = 100f;

        var mods = new[]
        {
            new Modifier("health", ModifierOp.Add, 20f, "ring"),           // 100 + 20 = 120
            new Modifier("health", ModifierOp.PercentAdd, 10f, "buff"),    // 120 + 10% = 132
            new Modifier("health", ModifierOp.Multiply, 1.5f, "potion"),   // 132 * 1.5 = 198
        };

        float result = ModifierSystem.Calculate(baseVal, mods);
        Assert.Equal(198f, result);
    }

    [Fact]
    public void Calculate_ShouldRespectOverrideModifier()
    {
        float baseVal = 100f;

        var mods = new[]
        {
            new Modifier("health", ModifierOp.Add, 50f, "shield"),
            new Modifier("health", ModifierOp.Override, 5f, "poison_debuff"), // should override final result completely
        };

        float result = ModifierSystem.Calculate(baseVal, mods);
        Assert.Equal(5f, result);
    }
}
