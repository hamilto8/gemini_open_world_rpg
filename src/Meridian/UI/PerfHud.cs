using Godot;
using System;

namespace Meridian.UI;

/// <summary>
/// Always-on Debug Performance HUD. Displays frame time, memory, draw calls, and other telemetry.
/// Enforces Section 18.1 requirements.
/// </summary>
public partial class PerfHud : Control
{
    private Label? _fpsLabel;
    private Label? _memoryLabel;
    private Label? _telemetryLabel;

    public override void _Ready()
    {
        // Programmatically build UI elements if they are not bound via TSCN
        _fpsLabel = GetNodeOrNull<Label>("FpsLabel");
        _memoryLabel = GetNodeOrNull<Label>("MemoryLabel");
        _telemetryLabel = GetNodeOrNull<Label>("TelemetryLabel");

        if (_fpsLabel == null)
        {
            // Fallback: build layout dynamically if scene is empty
            var container = new VBoxContainer();
            AddChild(container);

            _fpsLabel = new Label { Name = "FpsLabel" };
            _memoryLabel = new Label { Name = "MemoryLabel" };
            _telemetryLabel = new Label { Name = "TelemetryLabel" };

            container.AddChild(_fpsLabel);
            container.AddChild(_memoryLabel);
            container.AddChild(_telemetryLabel);
        }
    }

    public override void _Process(double delta)
    {
        double fps = Engine.GetFramesPerSecond();
        double ms = delta * 1000.0;

        if (_fpsLabel != null)
        {
            _fpsLabel.Text = $"Frame Time: {ms:F2} ms ({fps:F0} FPS)";
            // Color code based on budget (Section 18.1: 60fps = 16.6ms)
            if (ms > 16.6)
            {
                _fpsLabel.Modulate = new Color(0.9f, 0.3f, 0.3f); // Red (over budget)
            }
            else
            {
                _fpsLabel.Modulate = new Color(0.3f, 0.9f, 0.3f); // Green (within budget)
            }
        }

        if (_memoryLabel != null)
        {
            ulong staticMem = OS.GetStaticMemoryUsage();
            double staticMemMb = staticMem / (1024.0 * 1024.0);
            _memoryLabel.Text = $"Static Memory: {staticMemMb:F2} MB";
        }

        if (_telemetryLabel != null)
        {
            // Basic performance counters
            int drawCalls = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
            int objects = (int)Performance.GetMonitor(Performance.Monitor.ObjectCount);
            _telemetryLabel.Text = $"Draw Calls: {drawCalls} | Objects: {objects}";
        }
    }
}
