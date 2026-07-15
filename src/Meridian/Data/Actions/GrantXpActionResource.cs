using Godot;
using Meridian.Core.Logic;

namespace Meridian.Data;

[GlobalClass]
public partial class GrantXpActionResource : GameActionResource
{
    [Export] public int Amount { get; set; } = 1;
    public override IGameAction ToAction() => new GrantXpAction(Amount);
}
