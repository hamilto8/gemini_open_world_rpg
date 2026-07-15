using Godot;
using Meridian.Core.Logic;

namespace Meridian.Data;

[GlobalClass]
public partial class StartQuestActionResource : GameActionResource
{
    [Export] public string QuestId { get; set; } = "";
    public override IGameAction ToAction() => new StartQuestAction(QuestId);
}
