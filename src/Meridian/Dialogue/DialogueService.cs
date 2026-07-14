using System;
using System.Collections.Generic;

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

    // TODO(vocabulary): Phase-6 scaffolding. Dialogue outcomes should route through the shared
    // GameActionResource vocabulary (doc §3.6/§13) rather than an arbitrary delegate, so writers can
    // trigger effects by name and conditions can gate them. GameActionResource/ConditionResource are
    // the two cross-cutting primitives the design leans on that remain unimplemented (L8).
    public Action? ActionEffect { get; }

    public DialogueChoice(string text, string targetNodeId, Action? actionEffect = null)
    {
        Text = text;
        TargetNodeId = targetNodeId;
        ActionEffect = actionEffect;
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
    private DialogueNode? _currentNode;

    public DialogueNode? CurrentNode => _currentNode;

    public void RegisterNode(DialogueNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _nodes[node.NodeId] = node;
    }

    public bool StartDialogue(string startNodeId)
    {
        if (!_nodes.TryGetValue(startNodeId, out var node))
        {
            return false;
        }

        _currentNode = node;
        return true;
    }

    public bool SelectChoice(int choiceIndex)
    {
        if (_currentNode == null || choiceIndex < 0 || choiceIndex >= _currentNode.Choices.Count)
        {
            return false;
        }

        var choice = _currentNode.Choices[choiceIndex];

        // Execute choice side effects (e.g. accepts a quest)
        choice.ActionEffect?.Invoke();

        if (choice.TargetNodeId.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            _currentNode = null;
            return true;
        }

        return StartDialogue(choice.TargetNodeId);
    }
}
