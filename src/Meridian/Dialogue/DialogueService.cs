using System;
using System.Collections.Generic;
using System.Linq;
using Meridian.Core.Logic;

namespace Meridian.Dialogue;

public class DialogueNode
{
    public string NodeId { get; }
    public string Speaker { get; }
    public string Text { get; }
    public List<DialogueChoice> Choices { get; } = new();

    public DialogueNode(string nodeId, string speaker, string text)
    {
        NodeId = nodeId;
        Speaker = speaker;
        Text = text;
    }
}

public class DialogueChoice
{
    public string Text { get; }
    public string TargetNodeId { get; }

    // Compatibility delegate for programmatic dialogue. Authored content uses Conditions/Actions below,
    // routed through the shared condition/action vocabulary.
    public Action? ActionEffect { get; }
    public IReadOnlyList<ICondition> Conditions { get; }
    public IReadOnlyList<IGameAction> Actions { get; }

    public DialogueChoice(string text, string targetNodeId, Action? actionEffect = null)
    {
        Text = text;
        TargetNodeId = targetNodeId;
        ActionEffect = actionEffect;
        Conditions = Array.Empty<ICondition>();
        Actions = Array.Empty<IGameAction>();
    }

    public DialogueChoice(
        string text,
        string targetNodeId,
        IReadOnlyList<ICondition> conditions,
        IReadOnlyList<IGameAction> actions)
    {
        Text = text;
        TargetNodeId = targetNodeId;
        Conditions = conditions ?? Array.Empty<ICondition>();
        Actions = actions ?? Array.Empty<IGameAction>();
    }
}

/// <summary>
/// Domain model managing conversations, choice evaluations, and conditional text branching.
/// Decoupled from Godot for unit testing.
/// Enforces Section 13.1 and 13.3 requirements.
/// </summary>
public class DialogueService
{
    private readonly Dictionary<string, DialogueNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IDialogueDefinition> _dialogues = new(StringComparer.OrdinalIgnoreCase);
    private readonly IConditionContext? _conditionContext;
    private readonly IActionContext? _actionContext;
    private DialogueNode? _currentNode;

    public DialogueNode? CurrentNode => _currentNode;
    public IReadOnlyList<DialogueChoice> AvailableChoices => _currentNode is null
        ? Array.Empty<DialogueChoice>()
        : _currentNode.Choices.Where(IsAvailable).ToList();

    public DialogueService(IConditionContext? conditionContext = null, IActionContext? actionContext = null)
    {
        _conditionContext = conditionContext;
        _actionContext = actionContext;
    }

    public void RegisterDialogue(IDialogueDefinition dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        if (string.IsNullOrEmpty(dialogue.Id))
        {
            throw new ArgumentException("Dialogue id cannot be empty.", nameof(dialogue));
        }

        _dialogues[dialogue.Id] = dialogue;
    }

    public void RegisterNode(DialogueNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _nodes[node.NodeId] = node;
    }

    public bool StartDialogue(string startNodeId)
    {
        if (_dialogues.TryGetValue(startNodeId, out var dialogue))
        {
            _nodes.Clear();
            foreach (var authoredNode in dialogue.Nodes)
            {
                var mappedNode = new DialogueNode(authoredNode.NodeId, authoredNode.Speaker, authoredNode.Text);
                foreach (var choice in authoredNode.Choices)
                {
                    mappedNode.Choices.Add(new DialogueChoice(choice.Text, choice.TargetNodeId, choice.Conditions, choice.Actions));
                }

                _nodes[mappedNode.NodeId] = mappedNode;
            }

            startNodeId = dialogue.StartNodeId;
        }

        if (!_nodes.TryGetValue(startNodeId, out var node))
        {
            return false;
        }

        _currentNode = node;
        return true;
    }

    public bool SelectChoice(int choiceIndex)
    {
        var available = AvailableChoices;
        if (_currentNode == null || choiceIndex < 0 || choiceIndex >= available.Count)
        {
            return false;
        }

        var choice = available[choiceIndex];

        // Execute choice side effects (e.g. accepts a quest)
        choice.ActionEffect?.Invoke();
        if (_actionContext is not null)
        {
            foreach (var action in choice.Actions)
            {
                action?.Execute(_actionContext);
            }
        }

        if (choice.TargetNodeId.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            _currentNode = null;
            return true;
        }

        return StartDialogue(choice.TargetNodeId);
    }

    private bool IsAvailable(DialogueChoice choice)
    {
        if (choice.Conditions.Count == 0)
        {
            return true;
        }

        if (_conditionContext is null)
        {
            return false;
        }

        foreach (var condition in choice.Conditions)
        {
            if (condition is null || !condition.Evaluate(_conditionContext))
            {
                return false;
            }
        }

        return true;
    }
}
