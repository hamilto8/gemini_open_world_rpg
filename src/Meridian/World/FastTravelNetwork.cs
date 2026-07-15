using System;
using System.Collections.Generic;
using Godot;
using Meridian.Core;
using Meridian.Core.Save;

namespace Meridian.World;

public class FastTravelNode
{
    public string NodeId { get; }
    public string DisplayName { get; }
    public Vector3 Position { get; }
    public bool IsDiscovered { get; set; }

    public FastTravelNode(string nodeId, string displayName, Vector3 position, bool isDiscovered = false)
    {
        NodeId = nodeId;
        DisplayName = displayName;
        Position = position;
        IsDiscovered = isDiscovered;
    }
}

/// <summary>
/// Domain service managing fast travel terminals discovery and travel validations.
/// Decoupled from Godot for unit testing.
/// Enforces Section 18.0 requirements.
/// </summary>
public class FastTravelNetwork : ISaveParticipant
{
    private readonly Dictionary<string, FastTravelNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingDiscoveries = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, FastTravelNode> Nodes => _nodes;

    public string ParticipantId => "WorldDiscoveries";
    public int RestoreOrder => SaveRestoreOrder.Narrative;
    public Type StateType => typeof(DiscoveriesStateDto);

    public void RegisterNode(string nodeId, string displayName, Vector3 position, bool discoveredByDefault = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        _nodes[nodeId] = new FastTravelNode(nodeId, displayName, position, discoveredByDefault);
        if (_pendingDiscoveries.Remove(nodeId))
        {
            _nodes[nodeId].IsDiscovered = true;
        }
    }

    public void RegisterNode(IFastTravelPointDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RegisterNode(
            definition.Id,
            definition.DisplayName,
            new Vector3(definition.X, definition.Y, definition.Z),
            definition.DiscoveredByDefault);
    }

    public bool DiscoverNode(string nodeId)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            if (!node.IsDiscovered)
            {
                node.IsDiscovered = true;

                // Publish discovery event
                if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
                {
                    eventBus.Publish(new FastTravelDiscoveredEvent(nodeId, node.DisplayName));
                }
                return true;
            }
        }
        return false;
    }

    public bool CanTravelTo(string nodeId)
    {
        return _nodes.TryGetValue(nodeId, out var node) && node.IsDiscovered;
    }

    public bool TravelTo(string destinationNodeId, Node3D entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (!CanTravelTo(destinationNodeId))
        {
            return false;
        }

        var node = _nodes[destinationNodeId];
        entity.GlobalPosition = node.Position;
        GD.Print($"[FastTravel] Teleported entity '{entity.Name}' to '{node.DisplayName}' at {node.Position}");

        if (Services.TryGet<IEventBus>(out var eventBus) && eventBus != null)
        {
            eventBus.Publish(new FastTravelExecutedEvent(destinationNodeId, node.Position));
        }

        return true;
    }

    public object CaptureState()
    {
        var discovered = new List<string>(_pendingDiscoveries);
        foreach (var (id, node) in _nodes)
        {
            if (node.IsDiscovered)
            {
                discovered.Add(id);
            }
        }
        discovered.Sort(StringComparer.OrdinalIgnoreCase);
        return new DiscoveriesStateDto(discovered);
    }

    public void RestoreState(object stateDto)
    {
        if (stateDto is not DiscoveriesStateDto dto)
        {
            throw new ArgumentException("Expected discovery state.", nameof(stateDto));
        }

        foreach (var node in _nodes.Values)
        {
            node.IsDiscovered = false;
        }
        _pendingDiscoveries.Clear();
        foreach (string id in dto.DiscoveredIds ?? new List<string>())
        {
            if (_nodes.TryGetValue(id, out var node))
            {
                node.IsDiscovered = true;
            }
            else if (!string.IsNullOrWhiteSpace(id))
            {
                // A discovery can belong to a region or DLC not registered yet. Retain it until the
                // corresponding terminal is authored/streamed back in rather than losing progress.
                _pendingDiscoveries.Add(id);
            }
        }
    }
}

public record struct FastTravelDiscoveredEvent(string NodeId, string DisplayName);
public record struct FastTravelExecutedEvent(string NodeId, Vector3 Position);
