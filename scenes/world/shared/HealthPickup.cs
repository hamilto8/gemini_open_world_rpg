using Godot;
using Meridian.Core;

namespace Meridian.Scenes.World;

/// <summary>World interactable that restores health to the possessed avatar's StatBlock on interact.</summary>
public partial class HealthPickup : StaticBody3D, IInteractable
{
    [Export] public float HealAmount { get; set; } = 40f;

    public string ObjectName => "Medkit";
    public string ActionPrompt => "Use";

    public bool CanInteract(Node3D interactor) => true;

    public void Interact(Node3D interactor)
    {
        // Consume the medkit only when it actually heals — freeing it with no possessed avatar, no
        // StatBlock, or at full health destroys it for nothing (same class as the pickup-loss finding).
        if (!Services.TryGet<IPlayerController>(out var pc) || pc?.PossessedEntity is not Node avatar) return;

        var stats = avatar.GetNodeOrNull<StatBlockNode>("StatBlock");
        if (stats == null) return;

        float health = stats.GetStat("health");
        float maxHealth = stats.GetStat("max_health");
        if (health >= maxHealth)
        {
            WeaponPickup.PublishNotice("Health already full.");
            return;
        }

        stats.SetBaseStat("health", Mathf.Min(maxHealth, health + HealAmount));
        GD.Print($"[Pickup] Healed +{HealAmount}.");
        QueueFree();
    }
}
