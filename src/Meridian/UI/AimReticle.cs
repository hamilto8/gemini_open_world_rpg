using Godot;

namespace Meridian.UI;

/// <summary>
/// Lightweight vector reticle rendered by Godot, so it remains crisp at any resolution and does not
/// require a placeholder texture asset. Theme/content passes can replace it with an authored widget.
/// </summary>
public partial class AimReticle : Control
{
    [Export] public Color ReticleColor { get; set; } = Colors.White;
    [Export] public float Radius { get; set; } = 8f;
    [Export] public float ArmLength { get; set; } = 7f;
    [Export] public float LineWidth { get; set; } = 2f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorLeft = 0.5f;
        AnchorTop = 0.5f;
        AnchorRight = 0.5f;
        AnchorBottom = 0.5f;
        OffsetLeft = -24f;
        OffsetTop = -24f;
        OffsetRight = 24f;
        OffsetBottom = 24f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 center = Size * 0.5f;
        DrawCircle(center, LineWidth, ReticleColor);
        DrawLine(center + Vector2.Left * Radius, center + Vector2.Left * (Radius + ArmLength), ReticleColor, LineWidth, true);
        DrawLine(center + Vector2.Right * Radius, center + Vector2.Right * (Radius + ArmLength), ReticleColor, LineWidth, true);
        DrawLine(center + Vector2.Up * Radius, center + Vector2.Up * (Radius + ArmLength), ReticleColor, LineWidth, true);
        DrawLine(center + Vector2.Down * Radius, center + Vector2.Down * (Radius + ArmLength), ReticleColor, LineWidth, true);
    }
}
