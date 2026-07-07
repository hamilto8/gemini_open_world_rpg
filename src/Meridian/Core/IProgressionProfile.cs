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

    /// <summary>Skill points granted per level-up (data, not a hardcoded constant — M3).</summary>
    int SkillPointsPerLevel { get; }
}
