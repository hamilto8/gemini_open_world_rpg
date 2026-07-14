using Godot;
using System;
using Meridian.Core;
using Meridian.Input;
using Meridian.Vehicles;

namespace Meridian.UI;

/// <summary>
/// Minimal gameplay HUD displaying health, stamina, and crosshair reticle.
/// Enforces Section 14.3 requirements (event-driven widgets, no per-frame polling).
/// </summary>
public partial class MinimalHud : Control
{
    /// <summary>How long a transient notice ("Pack is full") stays on screen.</summary>
    private const float NoticeSeconds = 2.5f;

    private ProgressBar? _healthBar;
    private ProgressBar? _staminaBar;
    private Control? _reticle;
    private Label? _interactPrompt;
    private Label? _ammoLabel;
    private Label? _noticeLabel;
    private Label? _vehicleLabel;

    private IDisposable? _focusSubscription;
    private IDisposable? _deviceSubscription;
    private IDisposable? _ammoSubscription;
    private IDisposable? _equipSubscription;
    private IDisposable? _noticeSubscription;
    private IDisposable? _possessionSubscription;
    private IDisposable? _vehicleSpeedSubscription;
    private IDisposable? _vehicleFuelSubscription;
    private Meridian.Player.InteractionFocusChangedEvent _currentFocus;
    private float _vehicleSpeed;
    private float _vehicleFuel;
    private bool _showVehicleHud;

    // Monotonic id so an older notice's hide-timer can't blank a newer notice.
    private int _noticeSequence;

    public override void _Ready()
    {
        // A full-screen HUD must not swallow mouse input, or camera mouse-look (read in
        // PlayerControllerNode._UnhandledInput) never receives motion events.
        MouseFilter = MouseFilterEnum.Ignore;

        _healthBar = GetNodeOrNull<ProgressBar>("HealthBar");
        _staminaBar = GetNodeOrNull<ProgressBar>("StaminaBar");
        _reticle = GetNodeOrNull<Control>("Reticle");
        _interactPrompt = GetNodeOrNull<Label>("InteractPrompt");

        if (_healthBar == null)
        {
            // Programmatic setup for Phase 1 HUD
            var container = new VBoxContainer { Size = new Vector2(300, 80), Position = new Vector2(10, 500) };
            AddChild(container);

            _healthBar = new ProgressBar { Name = "HealthBar", MinValue = 0, MaxValue = 100, Value = 100 };
            _staminaBar = new ProgressBar { Name = "StaminaBar", MinValue = 0, MaxValue = 100, Value = 100 };
            _interactPrompt = new Label { Name = "InteractPrompt", Text = "" };

            container.AddChild(new Label { Text = "HP" });
            container.AddChild(_healthBar);
            container.AddChild(new Label { Text = "Stamina" });
            container.AddChild(_staminaBar);
            container.AddChild(_interactPrompt);

            _ammoLabel = new Label { Name = "AmmoLabel", Text = "", Visible = false };
            container.AddChild(_ammoLabel);

            _noticeLabel = new Label { Name = "NoticeLabel", Text = "", Visible = false };
            container.AddChild(_noticeLabel);

            _vehicleLabel = new Label { Name = "VehicleLabel", Text = "", Visible = false };
            container.AddChild(_vehicleLabel);

            // Centered, resolution-independent reticle with actual drawn geometry. The earlier empty
            // TextureRect had no texture and therefore rendered nothing while aiming.
            _reticle = new AimReticle
            {
                Name = "Reticle",
                Visible = false // only visible during aim
            };
            AddChild(_reticle);
        }

        // Display-only widgets — none should capture the mouse (the centered reticle in particular
        // would otherwise block look while aiming).
        SetChildrenMouseIgnore(this);

        // Subscribe to EventBus interaction prompt updates (Section 14.3) and input-device swaps so the
        // prompt shows the right glyph for keyboard vs gamepad.
        var eventBus = Services.Get<IEventBus>();
        _focusSubscription = eventBus.Subscribe<Meridian.Player.InteractionFocusChangedEvent>(OnInteractionFocusChanged);
        _deviceSubscription = eventBus.Subscribe<InputDeviceChangedEvent>(_ => RenderInteractPrompt());
        _ammoSubscription = eventBus.Subscribe<Meridian.Combat.WeaponAmmoChangedEvent>(OnAmmoChanged);
        _equipSubscription = eventBus.Subscribe<Meridian.Combat.WeaponEquippedEvent>(OnWeaponEquipped);
        _noticeSubscription = eventBus.Subscribe<HudNoticeEvent>(OnHudNotice);
        _possessionSubscription = eventBus.Subscribe<PossessionChangedEvent>(OnPossessionChanged);
        _vehicleSpeedSubscription = eventBus.Subscribe<VehicleSpeedChangedEvent>(OnVehicleSpeedChanged);
        _vehicleFuelSubscription = eventBus.Subscribe<VehicleFuelChangedEvent>(OnVehicleFuelChanged);
    }

    public override void _ExitTree()
    {
        _focusSubscription?.Dispose();
        _deviceSubscription?.Dispose();
        _ammoSubscription?.Dispose();
        _equipSubscription?.Dispose();
        _noticeSubscription?.Dispose();
        _possessionSubscription?.Dispose();
        _vehicleSpeedSubscription?.Dispose();
        _vehicleFuelSubscription?.Dispose();
    }

    private void OnHudNotice(HudNoticeEvent ev)
    {
        if (_noticeLabel == null) return;

        _noticeLabel.Text = ev.Message;
        _noticeLabel.Visible = true;

        int sequence = ++_noticeSequence;
        GetTree().CreateTimer(NoticeSeconds).Timeout += () =>
        {
            // Only the newest notice may clear the label; IsInstanceValid guards the freed-node case
            // (SceneTreeTimer callbacks outlive _ExitTree — the L5 timer-lifetime lesson).
            if (IsInstanceValid(this) && sequence == _noticeSequence && _noticeLabel != null)
            {
                _noticeLabel.Visible = false;
            }
        };
    }

    private void OnWeaponEquipped(Meridian.Combat.WeaponEquippedEvent ev)
    {
        if (_ammoLabel == null) return;
        _ammoLabel.Visible = true;
        _ammoLabel.Text = $"Ammo: {ev.CurrentAmmo} / {ev.MagazineSize}";
    }

    private void OnAmmoChanged(Meridian.Combat.WeaponAmmoChangedEvent ev)
    {
        if (_ammoLabel == null) return;
        _ammoLabel.Visible = true;
        _ammoLabel.Text = $"Ammo: {ev.CurrentAmmo} / {ev.MagazineSize}  (reserve {ev.Reserve})";
    }

    private static void SetChildrenMouseIgnore(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Control control)
            {
                control.MouseFilter = Control.MouseFilterEnum.Ignore;
            }
            SetChildrenMouseIgnore(child);
        }
    }

    public void UpdatePlayerStats(float health, float maxHealth, float stamina, float maxStamina)
    {
        if (_healthBar != null)
        {
            _healthBar.MaxValue = maxHealth;
            _healthBar.Value = health;
        }

        if (_staminaBar != null)
        {
            _staminaBar.MaxValue = maxStamina;
            _staminaBar.Value = stamina;
        }
    }

    public void SetAiming(bool isAiming)
    {
        if (_reticle != null)
        {
            _reticle.Visible = isAiming;
        }
    }

    private void OnInteractionFocusChanged(Meridian.Player.InteractionFocusChangedEvent ev)
    {
        _currentFocus = ev;
        RenderInteractPrompt();
    }

    private void RenderInteractPrompt()
    {
        if (_interactPrompt == null) return;

        if (_currentFocus.ActionPrompt != null)
        {
            _interactPrompt.Text = $"Press {InteractGlyph()} to {_currentFocus.ActionPrompt} {_currentFocus.ObjectName}";
            _interactPrompt.Visible = true;
        }
        else
        {
            _interactPrompt.Visible = false;
        }
    }

    private static string InteractGlyph()
    {
        // interact is bound to E (keyboard) and the X button (gamepad) — show whichever matches the
        // player's current device so the scheme swaps seamlessly.
        if (Services.TryGet<IInputDeviceTracker>(out var tracker) && tracker?.ActiveDevice == InputDeviceType.Gamepad)
        {
            return "(X)";
        }
        Key key = InputRebindStore.GetBoundKey("interact");
        string label = key == Key.None ? "Unbound" : OS.GetKeycodeString(key);
        return $"[{label}]";
    }

    private void OnPossessionChanged(PossessionChangedEvent ev)
    {
        _showVehicleHud = ev.NewEntity is VehicleAvatar;
        if (!_showVehicleHud)
        {
            _vehicleSpeed = 0f;
            _vehicleFuel = 0f;
        }
        RenderVehicleHud();
    }

    private void OnVehicleSpeedChanged(VehicleSpeedChangedEvent ev)
    {
        _vehicleSpeed = ev.Speed;
        RenderVehicleHud();
    }

    private void OnVehicleFuelChanged(VehicleFuelChangedEvent ev)
    {
        _vehicleFuel = ev.Fuel;
        RenderVehicleHud();
    }

    private void RenderVehicleHud()
    {
        if (_vehicleLabel == null)
        {
            return;
        }

        _vehicleLabel.Visible = _showVehicleHud;
        if (_showVehicleHud)
        {
            float speedKph = Mathf.Abs(_vehicleSpeed) * 3.6f;
            _vehicleLabel.Text = $"Vehicle  {speedKph:0} km/h  |  Fuel {_vehicleFuel:0}%  |  Hold exit control to leave";
        }
    }
}
