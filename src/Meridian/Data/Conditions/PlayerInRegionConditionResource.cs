using Godot;
using Meridian.Core.Logic;

namespace Meridian.Data;

[GlobalClass]
public partial class PlayerInRegionConditionResource : ConditionResource
{
    [Export] public string RegionId { get; set; } = "";
    public override ICondition ToCondition() => new PlayerInRegionCondition(RegionId);
}
