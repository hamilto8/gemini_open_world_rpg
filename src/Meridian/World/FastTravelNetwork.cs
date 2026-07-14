using System;
using System.Collections.Generic;
using Godot;
using Meridian.Core;

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
public class FastTravelNetwork
{
    private readonly Dictionary<string, FastTravelNode> _nodes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, FastTravelNode> Nodes => _nodes;

    public void RegisterNode(string nodeId, string displayName, Vector3 position)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        _nodes[nodeId] = new FastTravelNode(nodeId, displayName, position);
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
}

public record struct FastTravelDiscoveredEvent(string NodeId, string DisplayName);
public record struct FastTravelExecutedEvent(string NodeId, Vector3 Position);
