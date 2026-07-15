using System.Collections.Generic;
using Godot;
using Meridian.Core.Logic;
using Meridian.Environment;

namespace Meridian.Data;

[GlobalClass]
public partial class ScheduledEventDefinition : Resource, IScheduledEventDefinition
{
    [Export] public string Id { get; set; } = "";
    [Export(PropertyHint.Range, "0,23,1")] public int Hour { get; set; }
    [Export(PropertyHint.Range, "0,59,1")] public int Minute { get; set; }
    [Export] public bool IsRecurring { get; set; } = true;
    [Export] public Godot.Collections.Array<ConditionResource> ConditionResources { get; set; } = new();
    [Export] public Godot.Collections.Array<GameActionResource> ActionResources { get; set; } = new();

    public IReadOnlyList<ICondition> Conditions => MapConditions();
    public IReadOnlyList<IGameAction> Actions => MapActions();

    private IReadOnlyList<ICondition> MapConditions()
    {
        var result = new List<ICondition>();
        foreach (var resource in ConditionResources)
        {
            if (resource is not null)
            {
                result.Add(resource.ToCondition());
            }
        }

        return result;
    }

    private IReadOnlyList<IGameAction> MapActions()
    {
        var result = new List<IGameAction>();
        foreach (var resource in ActionResources)
        {
            if (resource is not null)
            {
                result.Add(resource.ToAction());
            }
        }

        return result;
    }
}
