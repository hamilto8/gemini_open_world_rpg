using Godot;
using Meridian.Core;

namespace Meridian.Input;

/// <summary>
/// Autoload node wrapper for InputContextService. Registers IInputContextService with the Services
/// locator at boot, seeds the default InputMap bindings, and tracks the active input device so the UI
/// can swap control schemes on the fly (<see cref="IInputDeviceTracker"/>).
/// </summary>
public partial class InputContextServiceNode : Node, IInputContextService, IInputDeviceTracker
{
    private const float StickActivityThreshold = 0.5f;
    private const float MouseActivityThresholdSq = 1.0f; // ignore sub-pixel jitter

    private readonly InputContextService _service = new();
    private InputDeviceType _activeDevice = InputDeviceType.KeyboardMouse;

    public InputDeviceType ActiveDevice => _activeDevice;

    public override void _EnterTree()
    {
        Services.Register<IInputContextService>(this);
        Services.Register<IInputDeviceTracker>(this);

        // Ensure the gameplay actions the controller polls actually exist in the InputMap (V1).
        InputMapBootstrap.EnsureDefaultBindings();
    }

    public override void _ExitTree()
    {
        _service.Reset();

        if (Services.TryGet<IInputDeviceTracker>(out var tracker) && ReferenceEquals(tracker, this))
        {
            Services.Unregister<IInputDeviceTracker>();
        }
    }

    public override void _Input(InputEvent @event)
    {
        InputDeviceType? detected = @event switch
        {
            InputEventKey => InputDeviceType.KeyboardMouse,
            InputEventMouseButton => InputDeviceType.KeyboardMouse,
            InputEventMouseMotion motion when motion.Relative.LengthSquared() > MouseActivityThresholdSq
                => InputDeviceType.KeyboardMouse,
            InputEventJoypadButton { Pressed: true } => InputDeviceType.Gamepad,
            InputEventJoypadMotion motion when Mathf.Abs(motion.AxisValue) > StickActivityThreshold
                => InputDeviceType.Gamepad,
            _ => null,
        };

        if (detected is InputDeviceType device && device != _activeDevice)
        {
            _activeDevice = device;
            if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
            {
                eventBus.Publish(new InputDeviceChangedEvent(device));
            }
        }
    }

    public InputContextType CurrentContext => _service.CurrentContext;

    public void PushContext(InputContextType context) => _service.PushContext(context);

    public void PopContext() => _service.PopContext();

    public bool IsActionAllowed(string action) => _service.IsActionAllowed(action);

    public void RegisterActionForContext(InputContextType context, string action) =>
        _service.RegisterActionForContext(context, action);

    public void Reset() => _service.Reset();
}
