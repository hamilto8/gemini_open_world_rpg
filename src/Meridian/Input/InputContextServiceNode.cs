using Godot;
using Meridian.Core;

namespace Meridian.Input;

/// <summary>
/// Autoload node wrapper for InputContextService. Registers IInputContextService with Services locator at boot.
/// </summary>
public partial class InputContextServiceNode : Node, IInputContextService
{
    private readonly InputContextService _service = new();

    public override void _EnterTree()
    {
        Services.Register<IInputContextService>(this);

        // Ensure the gameplay actions the controller polls actually exist in the InputMap (V1).
        InputMapBootstrap.EnsureDefaultBindings();
    }

    public override void _ExitTree()
    {
        _service.Reset();
    }

    public InputContextType CurrentContext => _service.CurrentContext;

    public void PushContext(InputContextType context) => _service.PushContext(context);

    public void PopContext() => _service.PopContext();

    public bool IsActionAllowed(string action) => _service.IsActionAllowed(action);

    public void RegisterActionForContext(InputContextType context, string action) =>
        _service.RegisterActionForContext(context, action);

    public void Reset() => _service.Reset();
}
