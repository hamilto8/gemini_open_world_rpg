namespace Meridian.Core;

/// <summary>
/// Interface representing progression profiles required by the ProgressionManager.
/// Allows unit tests to mock progression curves without instantiating Godot Resource classes.
/// </summary>
public interface IProgressionProfile
{
    int BaseXpRequired { get; }
    float XpExponent { get; }
    int MaxLevel { get; }
}

/// <summary>
/// Basic mock implementation of IProgressionProfile for unit testing.
/// </summary>
public class BasicProgressionProfile : IProgressionProfile
{
    public int BaseXpRequired { get; set; } = 100;
    public float XpExponent { get; set; } = 1.5f;
    public int MaxLevel { get; set; } = 50;
}
