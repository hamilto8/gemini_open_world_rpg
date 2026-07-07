using Godot;
using System.Collections.Generic;
using Meridian.Core;
using Meridian.Items;
using Meridian.World;

namespace Meridian.Scenes.World;

/// <summary>
/// A lootable container: grants materials once, then reports as looted. Implements
/// <see cref="IPersistentSceneObject"/> so its open state survives streaming/save (M13, doc §4.3/§16).
/// </summary>
public partial class LootContainer : StaticBody3D, IInteractable, IPersistentSceneObject
{
    [Export] public string PersistentIdValue { get; set; } = "container_start";
    [Export] public string LootItemId { get; set; } = "metal_scrap";
    [Export] public int LootAmount { get; set; } = 5;
    [Export] public MeshInstance3D? Lid { get; set; }

    private bool _isOpen;

    public string ObjectName => "Supply Crate";
    public string ActionPrompt => _isOpen ? "Empty" : "Open";

    public bool CanInteract(Node3D interactor) => !_isOpen;

    public override void _Ready() => ApplyOpenVisual();

    public void Interact(Node3D interactor)
    {
        if (_isOpen) return;
        _isOpen = true;
        ApplyOpenVisual();

        if (Services.TryGet<IInventoryProvider>(out var provider) && provider != null)
        {
            provider.Inventory.RegisterDefinition(new Meridian.Data.ItemResource
            {
                Id = LootItemId,
                DisplayName = LootItemId,
                MaxStack = 999,
                Weight = 0.1f,
            });
            provider.Inventory.AddItem(new ItemInstance(LootItemId, LootAmount));
        }

        GD.Print($"[Container] Looted {LootAmount}x {LootItemId}.");
    }

    private void ApplyOpenVisual()
    {
        // Tip the lid open when looted for a simple visual cue.
        if (Lid != null)
        {
            Lid.RotationDegrees = new Vector3(_isOpen ? -100f : 0f, 0f, 0f);
        }
    }

    // IPersistentSceneObject
    public string PersistentId => PersistentIdValue;

    public Dictionary<string, string> CaptureState() => new() { ["open"] = _isOpen.ToString() };

    public void RestoreState(Dictionary<string, string> state)
    {
        if (state.TryGetValue("open", out var value) && bool.TryParse(value, out var open))
        {
            _isOpen = open;
            ApplyOpenVisual();
        }
    }
}
