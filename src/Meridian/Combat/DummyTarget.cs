using Godot;
using System;
using Meridian.Core;

namespace Meridian.Combat;

/// <summary>
/// Combat dummy target node executing damage mitigation and broadcasting hit events.
/// Enforces Section 6.1 requirements.
/// </summary>
public partial class DummyTarget : StaticBody3D, IDamageable, IHitZoneResolver
{
    [Export] public string TargetName { get; set; } = "Training Dummy";
    [Export] public float BaseArmor { get; set; } = 5.0f;

    /// <summary>Hits at least this many metres above the target's origin count as a headshot.</summary>
    [Export] public float HeadHeightThreshold { get; set; } = 1.6f;

    private StatBlockNode? _stats;
    private Tween? _pulseTween;

    public override void _Ready()
    {
        // Add a local StatBlock component dynamically if missing
        _stats = GetNodeOrNull<StatBlockNode>("StatBlock");
        if (_stats == null)
        {
            _stats = new StatBlockNode { Name = "StatBlock" };
            AddChild(_stats);
        }

        _stats.SetBaseStat("armor", BaseArmor);
        _stats.SetBaseStat("max_health", 200f);
        _stats.SetBaseStat("health", 200f);
    }

    /// <summary>Derives the hit zone from this target's own height (doc §6.1), never the shooter's camera (H1).</summary>
    public HitZone ResolveHitZone(Vector3 worldHitPosition)
    {
        float relativeHeight = worldHitPosition.Y - GlobalPosition.Y;
        return relativeHeight >= HeadHeightThreshold ? HitZone.Head : HitZone.Body;
    }

    public void ApplyDamage(DamageInfo info)
    {
        if (_stats == null) return;

        float health = _stats.GetStat("health");
        if (health <= 0f) return; // Already destroyed

        // Ordered mitigation via the shared pure pipeline (Section 6.1).
        float armor = _stats.GetStat("armor");
        float mitigatedDamage = DamagePipeline.Mitigate(info.Amount, info.Zone, armor);

        // Apply to Health
        float newHealth = Math.Max(0f, health - mitigatedDamage);
        _stats.SetBaseStat("health", newHealth);

        GD.Print($"[DummyTarget] '{TargetName}' hit in {info.Zone} for {mitigatedDamage} damage (Armor: {armor}). Health: {newHealth}");

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
        // A node-owned Tween is bound to this node's lifetime, so it can't fire a callback into a
        // freed target the way a bare SceneTree timer lambda could (L5).
        _pulseTween?.Kill();
        Scale = new Vector3(1.2f, 1.2f, 1.2f);
        _pulseTween = CreateTween();
        _pulseTween.TweenProperty(this, "scale", Vector3.One, 0.1f);
    }
}

/// <summary>
/// Event broadcasted to EventBus when damage is successfully applied to a target.
/// </summary>
public record struct DamageDealtEvent(string TargetName, float MitigatedAmount, bool IsCritical, float NewHealth, bool IsDead);
