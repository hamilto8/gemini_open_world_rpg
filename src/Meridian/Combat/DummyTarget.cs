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
    [Export] public float MaxHealth { get; set; } = 100.0f;

    /// <summary>Seconds before a destroyed target respawns (0 = stays destroyed).</summary>
    [Export] public float RespawnSeconds { get; set; } = 5.0f;

    /// <summary>Hits at least this many metres above the target's origin count as a headshot.</summary>
    [Export] public float HeadHeightThreshold { get; set; } = 1.6f;

    private StatBlockNode? _stats;
    private Tween? _pulseTween;
    private uint _collisionLayer;

    public override void _Ready()
    {
        _collisionLayer = CollisionLayer;

        // Add a local StatBlock component dynamically if missing
        _stats = GetNodeOrNull<StatBlockNode>("StatBlock");
        if (_stats == null)
        {
            _stats = new StatBlockNode { Name = "StatBlock" };
            AddChild(_stats);
        }

        _stats.SetBaseStat("armor", BaseArmor);
        _stats.SetBaseStat("max_health", MaxHealth);
        _stats.SetBaseStat("health", MaxHealth);
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

        DamageApplicationResult result = DamagePipeline.Apply(_stats.Stats, info);
        if (!result.WasApplied) return;

        GD.Print($"[DummyTarget] '{TargetName}' hit in {info.Zone} for {result.AppliedDamage} damage. Health: {result.NewHealth}");

        // 4. Publish Event to EventBus
        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new DamageDealtEvent(
                TargetName: TargetName,
                MitigatedAmount: result.AppliedDamage,
                IsCritical: info.Zone == HitZone.Head || info.Zone == HitZone.Weakpoint,
                NewHealth: result.NewHealth,
                IsDead: result.IsDead
            ));
        }

        if (result.IsDead)
        {
            Die();
        }
        else
        {
            // Visual feedback: simple scale pulse
            ScalePulse();
        }
    }

    private void Die()
    {
        GD.Print($"[DummyTarget] '{TargetName}' destroyed.");
        Visible = false;
        CollisionLayer = 0; // stop registering hits while destroyed

        if (RespawnSeconds > 0f)
        {
            var respawn = CreateTween();
            respawn.TweenInterval(RespawnSeconds);
            respawn.TweenCallback(Callable.From(Respawn));
        }
    }

    private void Respawn()
    {
        _stats?.SetBaseStat("health", MaxHealth);
        Scale = Vector3.One;
        Visible = true;
        CollisionLayer = _collisionLayer;
        GD.Print($"[DummyTarget] '{TargetName}' respawned.");
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
