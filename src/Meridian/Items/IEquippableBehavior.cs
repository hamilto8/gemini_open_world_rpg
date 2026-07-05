namespace Meridian.Items;

/// <summary>
/// Interface representing equippable item behavior properties required by the equipment slot simulation.
/// Allows unit tests to mock equippable behaviors without instantiating Godot Resource classes.
/// </summary>
public interface IEquippableBehavior
{
    string SlotId { get; }
    string TargetStatId { get; }
    float ModifierValue { get; }
}
