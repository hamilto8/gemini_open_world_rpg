using Godot;
using Meridian.Core;

namespace Meridian.Data;

/// <summary>
/// Data-driven definition Resource for character progression curves. Implements IProgressionProfile.
/// Enforces Section 17.1 requirements.
/// </summary>
[GlobalClass]
public partial class ProgressionProfile : Resource, IProgressionProfile
{
    [Export] public string Id { get; set; } = "";
    [Export] public int BaseXpRequired { get; set; } = 100;
    [Export] public float XpExponent { get; set; } = 1.5f;
    [Export] public int MaxLevel { get; set; } = 50;
    [Export] public int SkillPointsPerLevel { get; set; } = 2;
}
