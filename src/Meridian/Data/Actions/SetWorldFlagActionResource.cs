using Godot;
using Meridian.Core.Logic;

namespace Meridian.Data;

[GlobalClass]
public partial class SetWorldFlagActionResource : GameActionResource
{
    [Export] public string FlagId { get; set; } = "";
    [Export] public bool Value { get; set; } = true;
    public override IGameAction ToAction() => new SetWorldFlagAction(FlagId, Value);
}
