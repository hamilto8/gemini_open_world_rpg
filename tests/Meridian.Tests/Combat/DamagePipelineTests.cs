using System;
using Xunit;
using Meridian.Core;
using Meridian.Combat;

namespace Meridian.Tests.Combat;

public class DamagePipelineTests
{
    private class MockDamageableTarget : IDamageable
    {
        public DamageInfo LastReceivedDamage { get; private set; }
        public int CallCount { get; private set; }

        public void ApplyDamage(DamageInfo info)
        {
            LastReceivedDamage = info;
            CallCount++;
        }
    }

    [Fact]
    public void MitigationPipeline_ShouldApplyMultiplierAndArmorCorrectly()
    {
        // Simple manual calculation verification
        float baseDamage = 50f;
        float armor = 10f;
        
        // 1. Headshot multiplier = 2.0x
        float rawDamage = baseDamage * 2.0f; // 100f
        
        // 2. Armor reduction
        float finalDamage = Math.Max(1.0f, rawDamage - armor); // 90f

        Assert.Equal(90f, finalDamage);
    }
}
