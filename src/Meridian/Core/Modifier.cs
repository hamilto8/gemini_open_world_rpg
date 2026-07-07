using System;

namespace Meridian.Core;

/// <summary>
/// Predefined mathematical operations for stat modification, applied by
/// <see cref="ModifierSystem.Calculate"/> in the order <c>Add -> PercentAdd -> Multiply -> Override</c>.
/// </summary>
public enum ModifierOp
{
    /// <summary>Flat addition to the base value.</summary>
    Add,

    /// <summary>Additive percentage as a fraction (0.15 = +15%); summed then applied once.</summary>
    PercentAdd,

    /// <summary>Multiplicative factor applied after percent adds.</summary>
    Multiply,

    /// <summary>Replaces the computed result entirely (last override wins).</summary>
    Override
}

/// <summary>
/// Stat modifier data structure.
/// Perks, gear, weather penalty, and status effects are all expressed through this structure.
/// </summary>
public class Modifier
{
    public string TargetStatId { get; }
    public ModifierOp Operation { get; }
    public float Value { get; }
    public string SourceTag { get; }
    public double? ExpiryTime { get; set; } // Absolute game clock time (TotalGameMinutes) or null for permanent

    public Modifier(string targetStatId, ModifierOp operation, float value, string sourceTag, double? expiryTime = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetStatId);
        ArgumentException.ThrowIfNullOrEmpty(sourceTag);

        TargetStatId = targetStatId;
        Operation = operation;
        Value = value;
        SourceTag = sourceTag;
        ExpiryTime = expiryTime;
    }
}
