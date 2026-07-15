using Godot;
using Meridian.Factions;

namespace Meridian.Data;

[GlobalClass]
public partial class FactionDefinition : Resource, IFactionDefinition
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public int MinimumReputation { get; set; } = -100;
    [Export] public int MaximumReputation { get; set; } = 100;
    [Export] public int StartingReputation { get; set; }
}
