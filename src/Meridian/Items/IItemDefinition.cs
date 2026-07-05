namespace Meridian.Items;

/// <summary>
/// Interface representing item definition properties required by the domain inventory simulation.
/// Allows unit tests to mock item definitions without instantiating Godot Resource classes.
/// </summary>
public interface IItemDefinition
{
    string Id { get; }
    int MaxStack { get; }
    float Weight { get; }
    System.Collections.Generic.IReadOnlyList<object> Behaviors { get; }
}

/// <summary>
/// Basic pure C# implementation of IItemDefinition for fallback and unit testing.
/// </summary>
public class BasicItemDefinition : IItemDefinition
{
    public string Id { get; }
    public int MaxStack { get; }
    public float Weight { get; }
    public System.Collections.Generic.IReadOnlyList<object> Behaviors { get; set; }

    public BasicItemDefinition(string id, int maxStack = 99, float weight = 0.1f)
    {
        Id = id;
        MaxStack = maxStack;
        Weight = weight;
        Behaviors = new System.Collections.Generic.List<object>();
    }
}
