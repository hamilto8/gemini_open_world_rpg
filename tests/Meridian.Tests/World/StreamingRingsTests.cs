using Xunit;
using Meridian.World;

namespace Meridian.Tests.World;

/// <summary>
/// Pins the ring/hysteresis transitions extracted from the streamer (H7, T4): a cell must not
/// thrash load/unload while hovering on a ring boundary.
/// </summary>
public class StreamingRingsTests
{
    // enter radii 100 / 200 / 300, hysteresis margin 30 => exits 130 / 230 / 330.
    private static StreamingRings Rings() => new(100f, 200f, 300f, 30f);

    [Theory]
    [InlineData(50f, CellState.Active)]
    [InlineData(150f, CellState.Simulated)]
    [InlineData(250f, CellState.Visual)]
    [InlineData(400f, CellState.Unloaded)]
    public void EvaluateTarget_FromUnloaded_UsesEnterRadii(float distance, CellState expected)
    {
        Assert.Equal(expected, Rings().EvaluateTarget(CellState.Unloaded, distance));
    }

    [Fact]
    public void ActiveCell_StaysActive_WithinExitMargin()
    {
        // 120 is past the 100 enter radius but within the 130 exit radius: no downgrade (hysteresis).
        Assert.Equal(CellState.Active, Rings().EvaluateTarget(CellState.Active, 120f));
    }

    [Fact]
    public void ActiveCell_Downgrades_OncePastExitRadius()
    {
        // 135 is past the 130 exit radius, so it drops one ring to Simulated.
        Assert.Equal(CellState.Simulated, Rings().EvaluateTarget(CellState.Active, 135f));
    }

    [Fact]
    public void VisualCell_StaysVisual_WithinExitMargin_ButUnloadsBeyond()
    {
        var rings = Rings();
        Assert.Equal(CellState.Visual, rings.EvaluateTarget(CellState.Visual, 320f)); // within 330 exit
        Assert.Equal(CellState.Unloaded, rings.EvaluateTarget(CellState.Visual, 340f)); // beyond 330 exit
    }

    [Fact]
    public void NoThrash_AtEnterBoundary()
    {
        var rings = Rings();
        // Sitting exactly on the Active enter boundary (100), an already-Active cell holds Active
        // rather than oscillating with sub-metre jitter.
        Assert.Equal(CellState.Active, rings.EvaluateTarget(CellState.Active, 100.5f));
        Assert.Equal(CellState.Active, rings.EvaluateTarget(CellState.Active, 99.5f));
    }
}
