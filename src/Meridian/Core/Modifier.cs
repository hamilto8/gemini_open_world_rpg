using System;

namespace Meridian.Core;

/// <summary>
/// Predefined mathematical operations for stat modification.
/// Order of operations: Override > Multiply > PercentAdd > Add.
/// </summary>
public enum ModifierOp
{
    Add,
    PercentAdd,
    Multiply,
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
