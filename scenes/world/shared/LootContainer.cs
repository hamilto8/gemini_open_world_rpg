using Godot;
using System.Collections.Generic;
using Meridian.Core;
using Meridian.Core.Registry;
using Meridian.Data;
using Meridian.Items;
using Meridian.World;

namespace Meridian.Scenes.World;

/// <summary>
/// A lootable container: rolls a data-driven <see cref="LootTableResource"/> once, then reports as looted.
/// Implements <see cref="IPersistentSceneObject"/> so its open state survives streaming/save (M13, doc
/// §4.3/§16). The loot table and its item drops are authored data resolved from <see cref="IContentDatabase"/>
/// by id (§7.4, §19.1); the exported id is the only scene-tunable surface.
/// </summary>
public partial class LootContainer : StaticBody3D, IInteractable, IPersistentSceneObject
{
    [Export] public string PersistentIdValue { get; set; } = "container_start";
    [Export] public string LootTableId { get; set; } = "supply_crate";
    [Export] public MeshInstance3D? Lid { get; set; }

    private bool _isOpen;

    public string ObjectName => "Supply Crate";
    public string ActionPrompt => _isOpen ? "Empty" : "Open";

    public bool CanInteract(Node3D interactor) => !_isOpen;

    public override void _Ready() => ApplyOpenVisual();

    public void Interact(Node3D interactor)
    {
        if (_isOpen) return;

        if (!Services.TryGet<IInventoryProvider>(out var provider) || provider == null) return;

        // Resolve the loot table from the registry rather than constructing drops inline. Missing database
        // or unknown id is a data error: warn loudly and stay closed (the ContentValidator catches it at
        // authoring time, §19.1).
        if (!Services.TryGet<IContentDatabase>(out var content) || content == null)
        {
            GD.PushWarning("[LootContainer] No IContentDatabase registered; cannot resolve loot table.");
            return;
        }

        if (!content.LootTables.TryGet(LootTableId, out var table) || table is not LootTableResource lootTable)
        {
            GD.PushWarning($"[LootContainer] Unknown loot table id '{LootTableId}'; not in the LootTables registry.");
            return;
        }

        // Roll one weighted drop. Random.Shared keeps this allocation-free (M7); a single-entry test table
        // is effectively deterministic (§7.4).
        var (itemId, quantity) = lootTable.RollDrop();
        if (string.IsNullOrEmpty(itemId) || quantity <= 0)
        {
            GD.PushWarning($"[LootContainer] Loot table '{LootTableId}' produced no drop; nothing granted.");
            return;
        }

        if (!content.Items.TryGet(itemId, out var definition) || definition == null)
        {
            GD.PushWarning($"[LootContainer] Loot table '{LootTableId}' rolled unknown item '{itemId}'.");
            return;
        }

        // Latch open only after the loot actually lands — a full pack (or missing inventory) must not
        // mark the crate looted and destroy its contents (finding: pickups discarded items on failure).
        provider.Inventory.RegisterDefinition(definition);
        if (!provider.Inventory.AddItem(new ItemInstance(itemId, quantity)))
        {
            WeaponPickup.PublishNotice($"Pack is full — can't carry {quantity}x {itemId}.");
            return;
        }

        _isOpen = true;
        ApplyOpenVisual();
        GD.Print($"[Container] Looted {quantity}x {itemId}.");
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
