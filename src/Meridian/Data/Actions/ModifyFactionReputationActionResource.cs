using Godot;
using Meridian.Core.Logic;

namespace Meridian.Data;

[GlobalClass]
public partial class ModifyFactionReputationActionResource : GameActionResource
{
    [Export] public string FactionId { get; set; } = "";
    [Export] public int Amount { get; set; }
    public override IGameAction ToAction() => new ModifyFactionReputationAction(FactionId, Amount);
}
