using System;
using System.Collections.Generic;
using Godot;
using Meridian.Core.Logic;
using Meridian.Dialogue;

namespace Meridian.Data;

[GlobalClass]
public partial class DialogueDefinition : Resource, IDialogueDefinition
{
    [Export] public string Id { get; set; } = "";
    [Export] public string StartNodeId { get; set; } = "";
    [Export] public Godot.Collections.Array<DialogueNodeResource> Nodes { get; set; } = new();

    IReadOnlyList<DialogueNodeDefinition> IDialogueDefinition.Nodes
    {
        get
        {
            var nodes = new List<DialogueNodeDefinition>();
            foreach (var node in Nodes)
            {
                if (node is null)
                {
                    continue;
                }

                var choices = new List<DialogueChoiceDefinition>();
                foreach (var choice in node.Choices)
                {
                    if (choice is null)
                    {
                        continue;
                    }

                    choices.Add(new DialogueChoiceDefinition(
                        choice.Text,
                        choice.TargetNodeId,
                        MapConditions(choice.Conditions),
                        MapActions(choice.Actions)));
                }

                nodes.Add(new DialogueNodeDefinition(node.NodeId, node.Speaker, node.Text, choices));
            }

            return nodes;
        }
    }

    private static IReadOnlyList<ICondition> MapConditions(Godot.Collections.Array<ConditionResource> resources)
    {
        var result = new List<ICondition>();
        foreach (var resource in resources)
        {
            if (resource is not null)
            {
                result.Add(resource.ToCondition());
            }
        }

        return result;
    }

    private static IReadOnlyList<IGameAction> MapActions(Godot.Collections.Array<GameActionResource> resources)
    {
        var result = new List<IGameAction>();
        foreach (var resource in resources)
        {
            if (resource is not null)
            {
                result.Add(resource.ToAction());
            }
        }

        return result;
    }
}
