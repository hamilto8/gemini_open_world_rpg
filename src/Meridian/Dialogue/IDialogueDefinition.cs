using System.Collections.Generic;
using Meridian.Core.Logic;

namespace Meridian.Dialogue;

/// <summary>Engine-free authored conversation consumed by <see cref="DialogueService"/>.</summary>
public interface IDialogueDefinition
{
    string Id { get; }
    string StartNodeId { get; }
    IReadOnlyList<DialogueNodeDefinition> Nodes { get; }
}

public sealed record DialogueNodeDefinition(
    string NodeId,
    string Speaker,
    string Text,
    IReadOnlyList<DialogueChoiceDefinition> Choices);

public sealed record DialogueChoiceDefinition(
    string Text,
    string TargetNodeId,
    IReadOnlyList<ICondition> Conditions,
    IReadOnlyList<IGameAction> Actions);
