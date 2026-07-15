using Godot;
using Meridian.Core.Logic;

namespace Meridian.Data;

[GlobalClass]
public partial class FactionReputationConditionResource : ConditionResource
{
    [Export] public string FactionId { get; set; } = "";
    [Export] public int Minimum { get; set; }
    public override ICondition ToCondition() => new FactionReputationCondition(FactionId, Minimum);
}
