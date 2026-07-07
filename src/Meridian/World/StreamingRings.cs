using System;

namespace Meridian.World;

/// <summary>
/// Engine-free ring evaluation with hysteresis (Section 4.3): a cell upgrades to a nearer ring at the
/// enter radius, but only downgrades once it passes the larger exit radius (enter + margin). This
/// prevents the load/unload thrashing a cell would otherwise suffer sitting exactly on a boundary.
/// Kept out of the streamer Node so the transition logic is directly unit-testable (H7, T4).
/// </summary>
public sealed class StreamingRings
{
    public float ActiveEnter { get; }
    public float SimulatedEnter { get; }
    public float VisualEnter { get; }
    public float Margin { get; }

    public float ActiveExit => ActiveEnter + Margin;
    public float SimulatedExit => SimulatedEnter + Margin;
    public float VisualExit => VisualEnter + Margin;

    public StreamingRings(float activeEnter, float simulatedEnter, float visualEnter, float hysteresisMargin)
    {
        ActiveEnter = activeEnter;
        SimulatedEnter = simulatedEnter;
        VisualEnter = visualEnter;
        Margin = Math.Max(0f, hysteresisMargin);
    }

    /// <summary>
    /// Returns the ring state a cell should target given its <paramref name="current"/> state and the
    /// <paramref name="distance"/> from the interest point. Never returns the transient
    /// <see cref="CellState.Loading"/> — only Unloaded/Visual/Simulated/Active rings.
    /// </summary>
    public CellState EvaluateTarget(CellState current, float distance)
    {
        int cur = (int)current;

        // Upgrade uses the enter radius; staying in a ring uses the larger exit radius (hysteresis).
        if (distance <= ActiveEnter) return CellState.Active;
        if (cur >= (int)CellState.Active && distance <= ActiveExit) return CellState.Active;

        if (distance <= SimulatedEnter) return CellState.Simulated;
        if (cur >= (int)CellState.Simulated && distance <= SimulatedExit) return CellState.Simulated;

        if (distance <= VisualEnter) return CellState.Visual;
        if (cur >= (int)CellState.Visual && distance <= VisualExit) return CellState.Visual;

        return CellState.Unloaded;
    }
}
