using Godot;
using Meridian.Core.Logic;

namespace Meridian.Data;

[GlobalClass]
public partial class ShowNotificationActionResource : GameActionResource
{
    [Export(PropertyHint.MultilineText)] public string Message { get; set; } = "";
    public override IGameAction ToAction() => new ShowNotificationAction(Message);
}
