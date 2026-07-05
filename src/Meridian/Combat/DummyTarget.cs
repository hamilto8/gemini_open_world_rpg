using Godot;
using System;
using Meridian.Core;

namespace Meridian.Combat;

/// <summary>
/// Combat dummy target node executing damage mitigation and broadcasting hit events.
/// Enforces Section 6.1 requirements.
/// </summary>
public partial class DummyTarget : StaticBody3D, IDamageable
{
    [Export] public string TargetName { get; set; } = "Training Dummy";
    [Export] public float BaseArmor { get; set; } = 5.0f;

    private StatBlock? _stats;

    public override void _Ready()
    {
        // Add a local StatBlock component dynamically if missing
        _stats = GetNodeOrNull<StatBlock>("StatBlock");
        if (_stats == null)
        {
            _stats = new StatBlock { Name = "StatBlock" };
            AddChild(_stats);
        }

        _stats.SetBaseStat("armor", BaseArmor);
        _stats.SetBaseStat("max_health", 200f);
        _stats.SetBaseStat("health", 200f);
    }

    public void ApplyDamage(DamageInfo info)
    {
        if (_stats == null) return;

        float health = _stats.GetStat("health");
        if (health <= 0f) return; // Already destroyed

        // 1. Resolve Hit Zone Multiplier (Section 6.1 Mitigation Pipeline)
        float zoneMultiplier = info.Zone switch
        {
            HitZone.Head => 2.0f,
            HitZone.Limbs => 0.5f,
            HitZone.Weakpoint => 3.0f,
            _ => 1.0f
        };

        float rawDamage = info.Amount * zoneMultiplier;

        // 2. Resolve Flat Armor Reduction
        float armor = _stats.GetStat("armor");
        float mitigatedDamage = Math.Max(1.0f, rawDamage - armor);

        // 3. Apply to Health
        float newHealth = Math.Max(0f, health - mitigatedDamage);
        _stats.SetBaseStat("health", newHealth);

        GD.Print($"[DummyTarget] '{TargetName}' hit in {info.Zone} for {mitigatedDamage} damage (Raw: {rawDamage}, Armor: {armor}). Health: {newHealth}");

        // 4. Publish Event to EventBus
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new DamageDealtEvent(
                TargetName: TargetName,
                MitigatedAmount: mitigatedDamage,
                IsCritical: info.Zone == HitZone.Head || info.Zone == HitZone.Weakpoint,
                NewHealth: newHealth,
                IsDead: newHealth <= 0f
            ));
        }

        // Visual feedback: simple scale pulse
        ScalePulse();
    }

    private void ScalePulse()
    {
        Scale = new Vector3(1.2f, 1.2f, 1.2f);
        var timer = GetTree().CreateTimer(0.1f);
        timer.Timeout += () => Scale = Vector3.One;
    }
}

/// <summary>
/// Event broadcasted to EventBus when damage is successfully applied to a target.
/// </summary>
public record struct DamageDealtEvent(string TargetName, float MitigatedAmount, bool IsCritical, float NewHealth, bool IsDead);
