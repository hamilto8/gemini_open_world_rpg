using Godot;
using Meridian.Core;

namespace Meridian.Scenes.World;

/// <summary>
/// Area3D trigger volume that registers player gateway boundary entries.
/// Enforces Section 4.2 and 4.6 requirements.
/// </summary>
public partial class GatewayVolume : Area3D
{
    [Export] public string TargetRegionId { get; set; } = "wilderness";

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node body)
    {
        // Check if the body entering the portal is the player avatar (possessable)
        if (body is CharacterBody3D && Services.TryGet<IPlayerController>(out var pc) && pc?.PossessedEntity == body)
        {
            GD.Print($"[GatewayVolume] Player entered boundary gateway for region: {TargetRegionId}");
            if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
            {
                eventBus.Publish(new RegionEnteredEvent(TargetRegionId));
            }
        }
    }
}

/// <summary>
/// Event published when the player crosses a gateway portal into a new region.
/// </summary>
public record struct RegionEnteredEvent(string RegionId);
