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
            new Modifier("health", ModifierOp.PercentAdd, 0.10f, "buff"),  // 120 + 10% = 132 (0.10 = 10%)
            new Modifier("health", ModifierOp.Multiply, 1.5f, "potion"),   // 132 * 1.5 = 198
        };

        float result = ModifierSystem.Calculate(baseVal, mods);
        Assert.Equal(198f, result);
    }

    [Fact]
    public void PercentAdd_ValuesAreFractions_NotWholePercents()
    {
        // Project-wide convention (H8): 0.15 means +15%, -0.15 means -15%.
        Assert.Equal(11.5f, ModifierSystem.Calculate(10f, new[] { new Modifier("s", ModifierOp.PercentAdd, 0.15f, "x") }), 3);
        Assert.Equal(8.5f, ModifierSystem.Calculate(10f, new[] { new Modifier("s", ModifierOp.PercentAdd, -0.15f, "x") }), 3);
    }

    [Fact]
    public void PercentAdd_ShouldStackAdditivelyBeforeMultiply()
    {
        var mods = new[]
        {
            new Modifier("s", ModifierOp.PercentAdd, 0.10f, "a"),
            new Modifier("s", ModifierOp.PercentAdd, 0.20f, "b"),
        };
        // 100 * (1 + 0.30) = 130
        Assert.Equal(130f, ModifierSystem.Calculate(100f, mods), 3);
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
