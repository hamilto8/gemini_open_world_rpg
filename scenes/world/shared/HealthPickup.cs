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
        if (Services.TryGet<IPlayerController>(out var pc) && pc?.PossessedEntity is Node avatar)
        {
            var stats = avatar.GetNodeOrNull<StatBlockNode>("StatBlock");
            if (stats != null)
            {
                float health = stats.GetStat("health");
                float maxHealth = stats.GetStat("max_health");
                stats.SetBaseStat("health", Mathf.Min(maxHealth, health + HealAmount));
                GD.Print($"[Pickup] Healed +{HealAmount}.");
            }
        }
        QueueFree();
    }
}
