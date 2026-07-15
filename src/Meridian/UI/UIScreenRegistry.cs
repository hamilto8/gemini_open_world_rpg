using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

namespace Meridian.UI;

/// <summary>Single authoring index for all production UI surfaces.</summary>
[GlobalClass]
public partial class UIScreenRegistry : Resource
{
    [Export] public Array<UIScreenDefinition> Screens { get; set; } = new();

    public bool TryGet(UIScreenId id, out UIScreenDefinition? definition)
    {
        foreach (var candidate in Screens)
        {
            if (candidate != null && candidate.Id == id)
            {
                definition = candidate;
                return true;
            }
        }
        definition = null;
        return false;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var seen = new HashSet<UIScreenId>();
        foreach (var definition in Screens)
        {
            if (definition == null)
            {
                errors.Add("Screen registry contains an empty entry.");
                continue;
            }
            if (!seen.Add(definition.Id)) errors.Add($"Duplicate UI screen id: {definition.Id}.");
            if (definition.Scene == null) errors.Add($"UI screen {definition.Id} has no PackedScene.");
        }
        foreach (UIScreenId id in Enum.GetValues<UIScreenId>())
        {
            if (!seen.Contains(id)) errors.Add($"Missing UI screen registration: {id}.");
        }
        return errors;
    }
}
