using Godot;
using System;
using Meridian.Core;

namespace Meridian.UI;

/// <summary>
/// Minimal gameplay HUD displaying health, stamina, and crosshair reticle.
/// Enforces Section 14.3 requirements (event-driven widgets, no per-frame polling).
/// </summary>
public partial class MinimalHud : Control
{
    private ProgressBar? _healthBar;
    private ProgressBar? _staminaBar;
    private TextureRect? _reticle;
    private Label? _interactPrompt;

    private IDisposable? _focusSubscription;

    public override void _Ready()
    {
        _healthBar = GetNodeOrNull<ProgressBar>("HealthBar");
        _staminaBar = GetNodeOrNull<ProgressBar>("StaminaBar");
        _reticle = GetNodeOrNull<TextureRect>("Reticle");
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

            // Center crosshair reticle (aim)
            _reticle = new TextureRect
            {
                Name = "Reticle",
                CustomMinimumSize = new Vector2(16, 16),
                Visible = false // only visible during aim
            };
            AddChild(_reticle);
        }

        // Subscribe to EventBus interaction prompt updates (Section 14.3)
        var eventBus = Services.Get<IEventBus>();
        _focusSubscription = eventBus.Subscribe<Meridian.Player.InteractionFocusChangedEvent>(OnInteractionFocusChanged);
    }

    public override void _ExitTree()
    {
        _focusSubscription?.Dispose();
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
        if (_interactPrompt == null) return;

        if (ev.ActionPrompt != null)
        {
            _interactPrompt.Text = $"Press [E] to {ev.ActionPrompt} {ev.ObjectName}";
            _interactPrompt.Visible = true;
        }
        else
        {
            _interactPrompt.Visible = false;
        }
    }
}
