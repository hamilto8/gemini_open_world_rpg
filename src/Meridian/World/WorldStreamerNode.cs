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
    private readonly Dictionary<Vector2I, List<Node3D>> _dynamicByCell = new();
    private readonly WorldStateStore _stateStore = new();

    // World-flags store: the game's consequence memory. Grouped with world state (§16.1), so the streamer
    // owns it, publishes it as a shared service, and enrolls it as a save participant alongside _stateStore.
    private readonly WorldFlagsService _worldFlags = new();

    private ICellLoader? _loader;
    private Node3D? _player;
    private RegionDefinition? _indexedRegion;
    private Vector2 _lastInterestSample;
    private bool _hasInterestSample;
    private int _instancesThisFrame;

    private StreamingRings? _rings;
    private Vector4 _lastRingParams = new(float.NaN, float.NaN, float.NaN, float.NaN);

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

        // Saves must see cells that are still loaded (deltas are otherwise flushed only on unload),
        // and a loaded save must rebuild instanced cells from the restored records.
        _stateStore.LiveStateProvider = SnapshotLiveCells;
        _stateStore.StateRestored += OnWorldStateRestored;

        // Publish the flag store so conditions, actions, dialogue, and quests share one namespace (§16.1).
        Services.Register<IWorldFlags>(_worldFlags);

        if (Services.TryGet<ISaveService>(out var saveService) && saveService != null)
        {
            saveService.RegisterParticipant(_stateStore);
            // Flags restore first (RestoreOrder 10) so later modules can read them during their own restore.
            saveService.RegisterParticipant(_worldFlags);
        }
    }

    public override void _ExitTree()
    {
        _stateStore.LiveStateProvider = null;
        _stateStore.StateRestored -= OnWorldStateRestored;

        if (Services.TryGet<ISaveService>(out var saveService) && saveService != null)
        {
            saveService.UnregisterParticipant(_stateStore);
            saveService.UnregisterParticipant(_worldFlags);
        }

        // Symmetric with the _Ready registration; guard against clobbering a different instance (mirrors
        // the IWorldStreamer unregister below).
        if (Services.TryGet<IWorldFlags>(out var flags) && ReferenceEquals(flags, _worldFlags))
        {
            Services.Unregister<IWorldFlags>();
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
        StreamingRings rings = GetRings();

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

    private StreamingRings GetRings()
    {
        // Rebuild only when the exported radii/margin change, instead of allocating every frame (V7).
        var current = new Vector4(ActiveRadius, SimulatedRadius, VisualRadius, HysteresisMargin);
        if (_rings == null || current != _lastRingParams)
        {
            _rings = new StreamingRings(ActiveRadius, SimulatedRadius, VisualRadius, HysteresisMargin);
            _lastRingParams = current;
        }
        return _rings;
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
        if (Services.TryGet<IPlayerController>(out var pc) && pc?.PossessedEntity is Node3D avatar)
        {
            if (!ReferenceEquals(_player, avatar))
            {
                // Possession can move from the on-foot avatar to a much faster vehicle. Reset the
                // velocity sample so the first vehicle frame does not produce a bogus lookahead spike.
                _hasInterestSample = false;
            }
            _player = avatar;
            return;
        }

        _player = null;
        _hasInterestSample = false;
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

        // Respawn runtime-spawned objects (dropped items, parked vehicles) recorded for this cell.
        SpawnDynamicObjects(gridPos, cellNode);
    }

    private void UnloadCell(Vector2I gridPos) => UnloadCell(gridPos, capture: true);

    private void UnloadCell(Vector2I gridPos, bool capture)
    {
        if (_instancedCells.TryGetValue(gridPos, out var cellNode))
        {
            if (capture)
            {
                // Capture authored deltas + dynamic-object records before freeing (Section 4.3).
                string cellKey = CellKey(gridPos);
                _stateStore.SaveCellState(cellKey, CaptureCellDeltas(cellNode));
                _stateStore.SaveCellDynamicObjects(cellKey, CaptureDynamicRecords(gridPos));
            }
            cellNode.QueueFree(); // dynamic objects are children of the cell root and free with it
            _instancedCells.Remove(gridPos);
            _dynamicByCell.Remove(gridPos);
        }
        _cellStates[gridPos] = CellState.Unloaded;
    }

    private void OnWorldStateRestored()
    {
        // A loaded save owns the truth now: drop live cells WITHOUT capturing (that would overwrite
        // the restored records with pre-load scene state) and let _Process stream them back in with
        // the restored deltas and dynamic objects applied. Also resets deltas the save never had.
        foreach (var gridPos in new List<Vector2I>(_instancedCells.Keys))
        {
            UnloadCell(gridPos, capture: false);
        }
    }

    /// <inheritdoc/>
    public void RegisterDynamicObject(Node3D node)
    {
        if (node is not IDynamicSceneObject)
        {
            GD.PushWarning($"[WorldStreamer] '{node?.Name}' does not implement IDynamicSceneObject — not registered.");
            return;
        }

        Vector2I gridPos = WorldToGrid(node.GlobalPosition);
        if (!_instancedCells.TryGetValue(gridPos, out var cellRoot))
        {
            GD.PushWarning($"[WorldStreamer] No instanced cell at {gridPos} for dynamic object '{node.Name}' — not registered.");
            return;
        }

        // Parent under the owning cell so the node's lifetime follows the cell's (frees on unload).
        if (node.GetParent() == null)
        {
            cellRoot.AddChild(node);
        }
        else if (node.GetParent() != cellRoot)
        {
            node.Reparent(cellRoot);
        }

        if (!_dynamicByCell.TryGetValue(gridPos, out var tracked))
        {
            tracked = new List<Node3D>();
            _dynamicByCell[gridPos] = tracked;
        }
        if (!tracked.Contains(node))
        {
            tracked.Add(node);
        }
    }

    /// <inheritdoc/>
    public bool UnregisterDynamicObject(Node3D node)
    {
        foreach (var tracked in _dynamicByCell.Values)
        {
            if (tracked.Remove(node))
            {
                return true;
            }
        }
        return false;
    }

    private Vector2I WorldToGrid(Vector3 position)
    {
        // Inverse of CellCenter: cell (x, y) spans [x·size, (x+1)·size) on each axis.
        float cellSize = ActiveRegion?.CellSize ?? 1f;
        return new Vector2I(
            (int)Mathf.Floor(position.X / cellSize),
            (int)Mathf.Floor(position.Z / cellSize));
    }

    private IEnumerable<WorldStateStore.LiveCellState> SnapshotLiveCells()
    {
        foreach (var (gridPos, cellNode) in _instancedCells)
        {
            yield return new WorldStateStore.LiveCellState(
                CellKey(gridPos),
                CaptureCellDeltas(cellNode),
                CaptureDynamicRecords(gridPos));
        }
    }

    private List<DynamicObjectRecordDto> CaptureDynamicRecords(Vector2I gridPos)
    {
        var records = new List<DynamicObjectRecordDto>();
        if (!_dynamicByCell.TryGetValue(gridPos, out var tracked)) return records;

        foreach (var node in tracked)
        {
            // Skip nodes freed outside our control (e.g. picked back up without unregistering).
            if (!IsInstanceValid(node) || node is not IDynamicSceneObject dynamic) continue;

            Vector3 position = node.GlobalPosition;
            records.Add(new DynamicObjectRecordDto(
                dynamic.PersistentId,
                dynamic.SceneFilePath,
                position.X, position.Y, position.Z,
                node.Rotation.Y,
                dynamic.CaptureState()));
        }
        return records;
    }

    private void SpawnDynamicObjects(Vector2I gridPos, Node cellNode)
    {
        foreach (var record in _stateStore.GetCellDynamicObjects(CellKey(gridPos)))
        {
            // Synchronous load is acceptable: records per cell are few and the scene is usually
            // already cached from the original spawn. Threaded prefetch is future work.
            var packed = ResourceLoader.Load<PackedScene>(record.ScenePath);
            if (packed == null)
            {
                GD.PushWarning($"[WorldStreamer] Dynamic object scene '{record.ScenePath}' missing — record '{record.PersistentId}' skipped.");
                continue;
            }

            var instantiated = packed.Instantiate();
            if (instantiated is not Node3D node || instantiated is not IDynamicSceneObject dynamic)
            {
                GD.PushWarning($"[WorldStreamer] '{record.ScenePath}' is not a Node3D IDynamicSceneObject — record '{record.PersistentId}' skipped.");
                instantiated?.QueueFree();
                continue;
            }

            cellNode.AddChild(node);
            node.GlobalPosition = new Vector3(record.PosX, record.PosY, record.PosZ);
            node.Rotation = new Vector3(0f, record.RotY, 0f);
            dynamic.RestoreState(record.State ?? new Dictionary<string, string>());

            if (!_dynamicByCell.TryGetValue(gridPos, out var tracked))
            {
                tracked = new List<Node3D>();
                _dynamicByCell[gridPos] = tracked;
            }
            tracked.Add(node);
        }
    }

    private static Dictionary<string, string> CaptureCellDeltas(Node cellNode)
    {
        // Walk all descendants (not just one level) for typed persistent objects, keyed by their
        // stable PersistentId rather than node name (M13). Flatten each object's state as "id.key".
        // Dynamic objects are excluded: they carry full respawn records (CaptureDynamicRecords).
        var deltas = new Dictionary<string, string>();
        foreach (var persistent in FindPersistentObjects(cellNode))
        {
            if (persistent is IDynamicSceneObject) continue;

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
