using Godot;
using System;
using System.Collections.Generic;
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
public partial class WorldStreamerNode : Node, IWorldStreamer
{
    [Export] public RegionDefinition? ActiveRegion { get; set; }

    // Ring enter radii (a cell upgrades to this ring at/inside the radius).
    [Export] public float ActiveRadius { get; set; } = 120.0f;
    [Export] public float SimulatedRadius { get; set; } = 250.0f;
    [Export] public float VisualRadius { get; set; } = 500.0f;

    /// <summary>Extra distance a cell must travel past its enter radius before it downgrades (anti-thrash).</summary>
    [Export] public float HysteresisMargin { get; set; } = 30.0f;

    /// <summary>Interest point leads the player by velocity × this many seconds so cells stream in ahead (§4.4).</summary>
    [Export] public float VelocityLookaheadSeconds { get; set; } = 0.5f;

    /// <summary>Max cell instantiations per frame — time-slices arrival bursts so they never spike a frame (§4.3).</summary>
    [Export] public int MaxCellInstancesPerFrame { get; set; } = 1;

    private readonly Dictionary<Vector2I, CellState> _cellStates = new();
    private readonly Dictionary<Vector2I, Node> _instancedCells = new();
    private readonly Dictionary<Vector2I, CellDefinition> _cellsByGrid = new();
    private readonly WorldStateStore _stateStore = new();

    private ICellLoader? _loader;
    private Node3D? _player;
    private RegionDefinition? _indexedRegion;
    private Vector2 _lastInterestSample;
    private bool _hasInterestSample;
    private int _instancesThisFrame;

    public WorldStateStore StateStore => _stateStore;
    public IReadOnlyDictionary<Vector2I, CellState> CellStates => _cellStates;
    public string? CurrentRegionId => ActiveRegion?.Id;

    public override void _EnterTree()
    {
        Services.Register<IWorldStreamer>(this);
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

        if (Services.TryGet<IWorldStreamer>(out var current) && ReferenceEquals(current, this))
        {
            Services.Unregister<IWorldStreamer>();
        }
    }

    public void SetLoader(ICellLoader loader)
    {
        _loader = loader;
    }

    public override void _Process(double delta)
    {
        if (ActiveRegion == null || _loader == null) return;

        EnsureCellIndex();
        ResolvePlayer();

        Vector2 interest = ComputeInterestPoint((float)delta);
        var rings = new StreamingRings(ActiveRadius, SimulatedRadius, VisualRadius, HysteresisMargin);

        _instancesThisFrame = 0;

        // Only cells that actually have a definition are considered (dictionary index, not a full-grid scan).
        foreach (var (gridPos, cellDef) in _cellsByGrid)
        {
            Vector2 cellCenter = CellCenter(gridPos);
            float distance = interest.DistanceTo(cellCenter);
            CellState current = _cellStates.TryGetValue(gridPos, out var s) ? s : CellState.Unloaded;
            CellState target = rings.EvaluateTarget(current, distance);

            StepCell(gridPos, cellDef, current, target);
        }
    }

    private void EnsureCellIndex()
    {
        if (ReferenceEquals(_indexedRegion, ActiveRegion)) return;

        _cellsByGrid.Clear();
        if (ActiveRegion != null)
        {
            foreach (var cell in ActiveRegion.Cells)
            {
                if (cell != null)
                {
                    _cellsByGrid[cell.GridPosition] = cell;
                }
            }
        }
        _indexedRegion = ActiveRegion;
    }

    private void ResolvePlayer()
    {
        if (_player != null) return;
        if (Services.TryGet<IPlayerController>(out var pc) && pc?.PossessedEntity is Node3D avatar)
        {
            _player = avatar;
        }
    }

    private Vector2 ComputeInterestPoint(float delta)
    {
        Vector3 playerPos = _player?.GlobalPosition ?? Vector3.Zero;
        Vector2 pos2D = new Vector2(playerPos.X, playerPos.Z);

        Vector2 velocity = Vector2.Zero;
        if (_hasInterestSample && delta > 0f)
        {
            velocity = (pos2D - _lastInterestSample) / delta;
        }
        _lastInterestSample = pos2D;
        _hasInterestSample = true;

        // Lead the interest point by projected velocity so cells ahead stream in before arrival (§4.4).
        return pos2D + velocity * VelocityLookaheadSeconds;
    }

    private Vector2 CellCenter(Vector2I gridPos)
    {
        float cellSize = ActiveRegion?.CellSize ?? 1f;
        return new Vector2((gridPos.X + 0.5f) * cellSize, (gridPos.Y + 0.5f) * cellSize);
    }

    private string CellKey(Vector2I gridPos) => $"{ActiveRegion?.Id}_{gridPos.X}_{gridPos.Y}";

    private void StepCell(Vector2I gridPos, CellDefinition cellDef, CellState current, CellState target)
    {
        switch (current)
        {
            case CellState.Unloaded:
                if (target != CellState.Unloaded)
                {
                    _cellStates[gridPos] = CellState.Loading;
                    if (!string.IsNullOrEmpty(cellDef.ScenePath))
                    {
                        _loader?.RequestLoad(cellDef.ScenePath);
                    }
                }
                break;

            case CellState.Loading:
                if (target == CellState.Unloaded)
                {
                    // Interest point left before instantiation — abandon the load rather than
                    // instantiate a cell nobody wants (H7 load-cancel).
                    _cellStates[gridPos] = CellState.Unloaded;
                }
                else if (_instancesThisFrame < MaxCellInstancesPerFrame
                         && !string.IsNullOrEmpty(cellDef.ScenePath)
                         && _loader != null
                         && _loader.IsLoadComplete(cellDef.ScenePath))
                {
                    InstantiateCell(gridPos, cellDef);
                }
                break;

            default: // Visual / Simulated / Active — already instanced
                if (target == CellState.Unloaded)
                {
                    UnloadCell(gridPos);
                }
                else if (target != current)
                {
                    _cellStates[gridPos] = target;
                    if (_instancedCells.TryGetValue(gridPos, out var cellNode))
                    {
                        // Visual is render-only (processing disabled); Simulated/Active process (§4.3).
                        bool process = target is CellState.Active or CellState.Simulated;
                        cellNode.ProcessMode = process ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
                    }
                }
                break;
        }
    }

    private void InstantiateCell(Vector2I gridPos, CellDefinition cellDef)
    {
        var cellNode = _loader!.InstantiateCell(cellDef.ScenePath);
        if (cellNode == null) return;

        _instancesThisFrame++;
        AddChild(cellNode);
        _instancedCells[gridPos] = cellNode;

        // Freshly instanced cells enter Visual: render-only, processing disabled per doc §4.3.
        cellNode.ProcessMode = ProcessModeEnum.Disabled;
        _cellStates[gridPos] = CellState.Visual;

        // Rehydrate persisted modifications from the state store (Section 4.3).
        if (_stateStore.TryGetCellState(CellKey(gridPos), out var deltas) && deltas != null)
        {
            ApplyCellDeltas(cellNode, deltas);
        }
    }

    private void UnloadCell(Vector2I gridPos)
    {
        if (_instancedCells.TryGetValue(gridPos, out var cellNode))
        {
            // Capture dynamic-object state before freeing (Section 4.3).
            _stateStore.SaveCellState(CellKey(gridPos), CaptureCellDeltas(cellNode));
            cellNode.QueueFree();
            _instancedCells.Remove(gridPos);
        }
        _cellStates[gridPos] = CellState.Unloaded;
    }

    private static Dictionary<string, string> CaptureCellDeltas(Node cellNode)
    {
        // Walk all descendants (not just one level) for typed persistent objects, keyed by their
        // stable PersistentId rather than node name (M13). Flatten each object's state as "id.key".
        var deltas = new Dictionary<string, string>();
        foreach (var persistent in FindPersistentObjects(cellNode))
        {
            foreach (var pair in persistent.CaptureState())
            {
                deltas[$"{persistent.PersistentId}.{pair.Key}"] = pair.Value;
            }
        }
        return deltas;
    }

    private static void ApplyCellDeltas(Node cellNode, Dictionary<string, string> deltas)
    {
        foreach (var persistent in FindPersistentObjects(cellNode))
        {
            string prefix = persistent.PersistentId + ".";
            var state = new Dictionary<string, string>();
            foreach (var pair in deltas)
            {
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    state[pair.Key.Substring(prefix.Length)] = pair.Value;
                }
            }

            if (state.Count > 0)
            {
                persistent.RestoreState(state);
            }
        }
    }

    private static IEnumerable<IPersistentSceneObject> FindPersistentObjects(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is IPersistentSceneObject persistent)
            {
                yield return persistent;
            }
            foreach (var nested in FindPersistentObjects(child))
            {
                yield return nested;
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
