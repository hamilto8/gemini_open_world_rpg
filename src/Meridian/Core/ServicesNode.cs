using Godot;

namespace Meridian.Core;

/// <summary>
/// Autoload node that ensures application lifecycle management for the Services locator.
/// </summary>
public partial class ServicesNode : Node
{
    public override void _EnterTree()
    {
        // Ensure locator is clean on initialization
        Services.Reset();
    }

    public override void _ExitTree()
    {
        // Clean up locator on shutdown
        Services.Reset();
    }
}
