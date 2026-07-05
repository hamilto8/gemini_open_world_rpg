using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Meridian.Core;
using Meridian.Core.Save;
using Meridian.Data;

namespace Meridian.World;

/// <summary>
/// Predefined cell lifecycle states.
/// Enforces Section 4.3 requirements.
/// </summary>
public enum CellState
{
    Unloaded,
    Loading,
    Visual,
    Simulated,
    Active
}

/// <summary>
/// Autoload Node implementing World Streaming and Cell lifecycle management.
/// time-slices instantiation on the main thread to prevent frame hitching.
/// Enforces Section 4.3 and 4.4 requirements.
/// </summary>
public partial class WorldStreamerNode : Node
{
    [Export] public RegionDefinition? ActiveRegion { get; set; }
    [Export] public float ActiveRadius { get; set; } = 120.0f;
    [Export] public float SimulatedRadius { get; set; } = 250.0f;
    [Export] public float VisualRadius { get; set; } = 500.0f;

    private readonly Dictionary<Vector2I, CellState> _cellStates = new();
    private readonly Dictionary<Vector2I, Node> _instancedCells = new();
    private readonly WorldStateStore _stateStore = new();
    
    private ICellLoader? _loader;
    private Node3D? _player;

    public WorldStateStore StateStore => _stateStore;
    public IReadOnlyDictionary<Vector2I, CellState> CellStates => _cellStates;

    public override void _EnterTree()
    {
        Services.Register<WorldStreamerNode>(this);
        Services.Register<WorldStateStore>(_stateStore);
    }

    public override void _Ready()
    {
        // Default Godot runtime loader
        _loader = new GodotCellLoader();

        if (Services.TryGet<ISaveService>(out var saveService) && saveService != null)
        {
            saveService.RegisterParticipant(_stateStore);
        }
    }

    public override void _ExitTree()
    {
        if (Services.TryGet<ISaveService>(out var saveService) && saveService != null)
        {
            saveService.UnregisterParticipant(_stateStore);
        }
    }

    public void SetLoader(ICellLoader loader)
    {
        _loader = loader;
    }

    public override void _Process(double delta)
    {
        if (ActiveRegion == null || _loader == null) return;

        // Lazy load player reference
        if (_player == null)
        {
            if (Services.TryGet<IPlayerController>(out var pc) && pc?.PossessedEntity is Node3D avatar)
            {
                _player = avatar;
            }
        }

        Vector3 playerPos = _player?.GlobalPosition ?? Vector3.Zero;
        Vector2 playerPos2D = new Vector2(playerPos.X, playerPos.Z);

        // Update lifecycle state of all cells in the active region bounds
        for (int x = ActiveRegion.Bounds.Position.X; x < ActiveRegion.Bounds.End.X; x++)
        {
            for (int y = ActiveRegion.Bounds.Position.Y; y < ActiveRegion.Bounds.End.Y; y++)
            {
                Vector2I gridPos = new Vector2I(x, y);
                Vector2 cellCenter = new Vector2(
                    (x + 0.5f) * ActiveRegion.CellSize,
                    (y + 0.5f) * ActiveRegion.CellSize
                );

                float distance = playerPos2D.DistanceTo(cellCenter);
                CellState targetState = EvaluateTargetState(distance);

                UpdateCellLifecycle(gridPos, targetState);
            }
        }
    }

    private CellState EvaluateTargetState(float distance)
    {
        if (distance <= ActiveRadius) return CellState.Active;
        if (distance <= SimulatedRadius) return CellState.Simulated;
        if (distance <= VisualRadius) return CellState.Visual;
        return CellState.Unloaded;
    }

    private void UpdateCellLifecycle(Vector2I gridPos, CellState targetState)
    {
        if (!_cellStates.TryGetValue(gridPos, out var currentState))
        {
            currentState = CellState.Unloaded;
        }

        if (currentState == targetState) return;

        // Process Load/Unload sequence
        if (targetState != CellState.Unloaded && currentState == CellState.Unloaded)
        {
            // Transition to loading
            _cellStates[gridPos] = CellState.Loading;
            var cellDef = ActiveRegion?.Cells.FirstOrDefault(c => c.GridPosition == gridPos);
            if (cellDef != null && !string.IsNullOrEmpty(cellDef.ScenePath))
            {
                _loader?.RequestLoad(cellDef.ScenePath);
            }
        }
        else if (currentState == CellState.Loading && _loader != null)
        {
            var cellDef = ActiveRegion?.Cells.FirstOrDefault(c => c.GridPosition == gridPos);
            if (cellDef != null && _loader.IsLoadComplete(cellDef.ScenePath))
            {
                var cellNode = _loader.InstantiateCell(cellDef.ScenePath);
                if (cellNode != null)
                {
                    AddChild(cellNode);
                    _instancedCells[gridPos] = cellNode;
                    _cellStates[gridPos] = CellState.Visual; // Render-only state initially

                    // Rehydrate modifications from state store (Section 4.3)
                    string cellKey = $"{ActiveRegion?.Id}_{gridPos.X}_{gridPos.Y}";
                    if (_stateStore.TryGetCellState(cellKey, out var deltas) && deltas != null)
                    {
                        ApplyCellDeltas(cellNode, deltas);
                    }
                }
            }
        }
        else if (targetState == CellState.Unloaded && currentState != CellState.Unloaded)
        {
            // Unload cell, capture state modifications first
            if (_instancedCells.TryGetValue(gridPos, out var cellNode))
            {
                string cellKey = $"{ActiveRegion?.Id}_{gridPos.X}_{gridPos.Y}";
                var deltas = CaptureCellDeltas(cellNode);
                _stateStore.SaveCellState(cellKey, deltas);

                cellNode.QueueFree();
                _instancedCells.Remove(gridPos);
            }
            _cellStates[gridPos] = CellState.Unloaded;
        }
        else
        {
            // Update mid-ring states directly (Visual ↔ Simulated ↔ Active)
            _cellStates[gridPos] = targetState;
            if (_instancedCells.TryGetValue(gridPos, out var cellNode))
            {
                // Toggle physics and processing flags (Section 4.3 rings)
                bool process = targetState == CellState.Active || targetState == CellState.Simulated;
                cellNode.ProcessMode = process ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
            }
        }
    }

    private Dictionary<string, string> CaptureCellDeltas(Node cellNode)
    {
        var deltas = new Dictionary<string, string>();
        // Walk child nodes and find looted chests/containers
        // e.g. if child is a Chest and has property IsOpen, capture it
        foreach (var child in cellNode.GetChildren())
        {
            if (child.Get("IsOpen").VariantType != Variant.Type.Nil)
            {
                bool isOpen = (bool)child.Get("IsOpen");
                deltas[child.Name] = isOpen.ToString();
            }
        }
        return deltas;
    }

    private void ApplyCellDeltas(Node cellNode, Dictionary<string, string> deltas)
    {
        foreach (var child in cellNode.GetChildren())
        {
            if (deltas.TryGetValue(child.Name, out var valStr) && bool.TryParse(valStr, out bool isOpen))
            {
                child.Set("IsOpen", isOpen);
            }
        }
    }

    private class GodotCellLoader : ICellLoader
    {
        public void RequestLoad(string scenePath)
        {
            ResourceLoader.LoadThreadedRequest(scenePath);
        }

        public bool IsLoadComplete(string scenePath)
        {
            return ResourceLoader.LoadThreadedGetStatus(scenePath) == ResourceLoader.ThreadLoadStatus.Loaded;
        }

        public Node? InstantiateCell(string scenePath)
        {
            var res = (PackedScene)ResourceLoader.LoadThreadedGet(scenePath);
            return res?.Instantiate();
        }
    }
}
