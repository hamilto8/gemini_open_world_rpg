using Godot;
using Meridian.Core.Logic;

namespace Meridian.Data;

[GlobalClass]
public partial class QuestStateConditionResource : ConditionResource
{
    [Export] public string QuestId { get; set; } = "";
    [Export] public string State { get; set; } = "Active";
    public override ICondition ToCondition() => new QuestStateCondition(QuestId, State);
}
