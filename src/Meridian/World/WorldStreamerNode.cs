using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Meridian.Core;
using Meridian.Core.Registry;
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
public partial class WorldStreamerNode : Node, IWorldStreamer, IPlayerRestoreCoordinator
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

    /// <summary>Maximum main-thread time spent applying streaming transitions in one frame.</summary>
    [Export(PropertyHint.Range, "0.1,8.0,0.1")] public double WorkBudgetMilliseconds { get; set; } = 1.5;

    /// <summary>Expands visual prefetch at driving speed so fast vehicles cannot overrun world data.</summary>
    [Export(PropertyHint.Range, "1.0,4.0,0.05")] public float DrivingPrefetchMultiplier { get; set; } = 1.75f;

    [Export(PropertyHint.Range, "1.0,100.0,0.5")] public float DrivingSpeedThreshold { get; set; } = 8f;
    [Export(PropertyHint.Range, "0,10,1")] public int MaxLoadRetries { get; set; } = 3;
    [Export(PropertyHint.Range, "0.1,30.0,0.1")] public double RetryBaseDelaySeconds { get; set; } = 1.0;

    private readonly Dictionary<Vector2I, CellState> _cellStates = new();
    private readonly Dictionary<Vector2I, Node> _instancedCells = new();
    private readonly Dictionary<Vector2I, Node> _collisionProxies = new();
    private readonly Dictionary<Vector2I, CellDefinition> _cellsByGrid = new();
    private readonly List<KeyValuePair<Vector2I, CellDefinition>> _alwaysLoadedCells = new();
    private readonly Dictionary<Vector2I, List<Node3D>> _dynamicByCell = new();
    private readonly Dictionary<Vector2I, LoadRetry> _loadRetries = new();
    private readonly HashSet<Vector2I> _mainSceneRequested = new();
    private readonly HashSet<Vector2I> _collisionSceneRequested = new();
    private readonly HashSet<Vector2I> _candidateGrids = new();
    private readonly List<StreamingWorkItem> _workItems = new();
    private readonly WorldStateStore _stateStore = new();

    // World-flags store: the game's consequence memory. Grouped with world state (§16.1), so the streamer
    // owns it, publishes it as a shared service, and enrolls it as a save participant alongside _stateStore.
    private readonly WorldFlagsService _worldFlags = new();
    private VehiclePersistenceService? _vehiclePersistence;

    private ICellLoader? _loader;
    private Node3D? _player;
    private RegionDefinition? _indexedRegion;
    private Vector2 _lastInterestSample;
    private bool _hasInterestSample;
    private int _instancesThisFrame;
    private float _interestSpeed;
    private double _streamTimeSeconds;

    private StreamingRings? _rings;
    private Vector4 _lastRingParams = new(float.NaN, float.NaN, float.NaN, float.NaN);

    private readonly record struct LoadRetry(int Attempts, double RetryAtSeconds);

    private struct StreamingWorkItem
    {
        public Vector2I Grid;
        public CellDefinition Definition;
        public CellState Current;
        public CellState Target;
        public float Distance;

        public StreamingWorkItem(
            Vector2I grid,
            CellDefinition definition,
            CellState current,
            CellState target,
            float distance)
        {
            Grid = grid;
            Definition = definition;
            Current = current;
            Target = target;
            Distance = distance;
        }
    }

    private sealed class StreamingWorkComparer : IComparer<StreamingWorkItem>
    {
        public static readonly StreamingWorkComparer Instance = new();

        public int Compare(StreamingWorkItem x, StreamingWorkItem y)
        {
            int urgency = Urgency(y).CompareTo(Urgency(x));
            if (urgency != 0) return urgency;

            int authoredPriority = y.Definition.StreamPriority.CompareTo(x.Definition.StreamPriority);
            if (authoredPriority != 0) return authoredPriority;

            int distance = x.Distance.CompareTo(y.Distance);
            if (distance != 0) return distance;

            int gridX = x.Grid.X.CompareTo(y.Grid.X);
            return gridX != 0 ? gridX : x.Grid.Y.CompareTo(y.Grid.Y);
        }

        private static int Urgency(StreamingWorkItem item)
        {
            // Cancelling obsolete in-flight work is cheapest and most urgent. Upgrades then proceed
            // nearest-ring first; ordinary unloads trail so arrival never waits behind cleanup.
            if (item.Current == CellState.Loading && item.Target == CellState.Unloaded) return 500;
            return item.Target switch
            {
                CellState.Active => 400,
                CellState.Simulated => 300,
                CellState.Visual => 200,
                _ => 100,
            };
        }
    }

    public WorldStateStore StateStore => _stateStore;
    public IReadOnlyDictionary<Vector2I, CellState> CellStates => _cellStates;
    public string? CurrentRegionId => ActiveRegion?.Id;

    public override void _EnterTree()
    {
        Services.Register<IWorldStreamer>(this);
        Services.Register<IPlayerRestoreCoordinator>(this);
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

            _vehiclePersistence = new VehiclePersistenceService(
                currentRegionId: () => CurrentRegionId ?? "unknown",
                possessedVehicleId: ResolvePossessedVehicleId,
                logger: message => GD.PushWarning(message));
            Services.Register<VehiclePersistenceService>(_vehiclePersistence);
            saveService.RegisterParticipant(_vehiclePersistence);
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
            if (_vehiclePersistence != null)
            {
                saveService.UnregisterParticipant(_vehiclePersistence);
            }
        }

        if (_vehiclePersistence != null
            && Services.TryGet<VehiclePersistenceService>(out var registeredVehicles)
            && ReferenceEquals(registeredVehicles, _vehiclePersistence))
        {
            Services.Unregister<VehiclePersistenceService>();
        }
        _vehiclePersistence = null;

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

        if (Services.TryGet<IPlayerRestoreCoordinator>(out var coordinator) && ReferenceEquals(coordinator, this))
        {
            Services.Unregister<IPlayerRestoreCoordinator>();
        }
    }

    private static string? ResolvePossessedVehicleId()
    {
        return Services.TryGet<IPlayerController>(out var controller)
            && controller?.PossessedEntity is IPersistentVehicle vehicle
                ? vehicle.PersistentVehicleId
                : null;
    }

    public void PrepareRegion(string regionId, Vector3 worldPosition)
    {
        if (!string.IsNullOrWhiteSpace(regionId)
            && Services.TryGet<IContentDatabase>(out var content)
            && content != null
            && content.Regions.TryGet(regionId, out var definition)
            && definition is RegionDefinition region
            && !ReferenceEquals(region, ActiveRegion))
        {
            ActiveRegion = region;
        }

        // Prime the interest sample at the restored location. The restored possessable is moved there
        // immediately afterward by PlayerControllerNode, so the next frame spatially warms this region
        // without a bogus cross-map velocity projection.
        _lastInterestSample = new Vector2(worldPosition.X, worldPosition.Z);
        _hasInterestSample = false;
    }

    public string GetPersistentId(IPossessable possessable)
    {
        if (possessable is IPersistentVehicle vehicle)
        {
            return vehicle.PersistentVehicleId;
        }
        if (possessable is Node node)
        {
            if (node.HasMeta("persistent_id"))
            {
                return node.GetMeta("persistent_id").AsString();
            }
            return node.GetPath().ToString();
        }
        return possessable.GetType().FullName ?? possessable.GetType().Name;
    }

    public IPossessable? ResolvePossessable(string persistentId)
    {
        if (string.IsNullOrWhiteSpace(persistentId) || GetTree().CurrentScene == null) return null;

        foreach (Node node in EnumerateTree(GetTree().CurrentScene))
        {
            if (node is IPossessable possessable
                && persistentId.Equals(GetPersistentId(possessable), StringComparison.OrdinalIgnoreCase))
            {
                return possessable;
            }
        }
        return null;
    }

    private static IEnumerable<Node> EnumerateTree(Node root)
    {
        yield return root;
        foreach (Node child in root.GetChildren())
        {
            foreach (Node descendant in EnumerateTree(child))
            {
                yield return descendant;
            }
        }
    }

    public void SetLoader(ICellLoader loader)
    {
        _loader = loader;
    }

    public override void _Process(double delta)
    {
        if (ActiveRegion == null || _loader == null) return;

        _streamTimeSeconds += Math.Max(0d, delta);

        EnsureCellIndex();
        ResolvePlayer();

        Vector2 interest = ComputeInterestPoint((float)delta);
        StreamingRings rings = GetRings();
        _instancesThisFrame = 0;

        BuildPrioritizedWork(interest, rings);

        long startedAt = Stopwatch.GetTimestamp();
        foreach (var item in _workItems)
        {
            if (Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds >= WorkBudgetMilliseconds)
            {
                break;
            }

            StepCell(item.Grid, item.Definition, item.Current, item.Target);
        }
    }

    private void BuildPrioritizedWork(Vector2 interest, StreamingRings rings)
    {
        _candidateGrids.Clear();
        _workItems.Clear();

        float cellSize = Math.Max(1f, ActiveRegion!.CellSize);
        float prefetchRadius = VisualRadius + HysteresisMargin;
        if (_interestSpeed >= DrivingSpeedThreshold)
        {
            prefetchRadius *= DrivingPrefetchMultiplier;
        }

        int minX = Mathf.FloorToInt((interest.X - prefetchRadius) / cellSize) - 1;
        int maxX = Mathf.FloorToInt((interest.X + prefetchRadius) / cellSize) + 1;
        int minY = Mathf.FloorToInt((interest.Y - prefetchRadius) / cellSize) - 1;
        int maxY = Mathf.FloorToInt((interest.Y + prefetchRadius) / cellSize) + 1;

        // Query only the grid neighborhood around the interest point. Region size can grow without
        // turning every frame into a linear scan across every authored cell.
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                var grid = new Vector2I(x, y);
                if (_cellsByGrid.TryGetValue(grid, out var definition))
                {
                    AddWorkItem(grid, definition, interest, rings);
                }
            }
        }

        foreach (var pair in _alwaysLoadedCells)
        {
            AddWorkItem(pair.Key, pair.Value, interest, rings);
        }

        // Loaded/loading cells outside the neighborhood still need a downgrade/cancellation pass.
        foreach (var pair in _cellStates)
        {
            if (pair.Value != CellState.Unloaded
                && !_candidateGrids.Contains(pair.Key)
                && _cellsByGrid.TryGetValue(pair.Key, out var definition))
            {
                AddWorkItem(pair.Key, definition, interest, rings);
            }
        }

        _workItems.Sort(StreamingWorkComparer.Instance);
        ApplyRegionBudgets();
    }

    private void AddWorkItem(
        Vector2I grid,
        CellDefinition definition,
        Vector2 interest,
        StreamingRings rings)
    {
        if (!_candidateGrids.Add(grid)) return;

        float distance = interest.DistanceTo(CellCenter(grid));
        CellState current = _cellStates.TryGetValue(grid, out var state) ? state : CellState.Unloaded;
        CellState target = rings.EvaluateTarget(current, distance);
        if (definition.AlwaysLoaded && target == CellState.Unloaded)
        {
            target = CellState.Visual;
        }

        _workItems.Add(new StreamingWorkItem(grid, definition, current, target, distance));
    }

    private void ApplyRegionBudgets()
    {
        int residentCells = 0;
        int simulationCost = 0;
        int maxResident = Math.Max(1, ActiveRegion!.MaxResidentCells);
        int maxSimulation = Math.Max(1, ActiveRegion.MaxSimulationCost);

        for (int index = 0; index < _workItems.Count; index++)
        {
            StreamingWorkItem item = _workItems[index];
            if (item.Target == CellState.Unloaded) continue;

            if (!item.Definition.AlwaysLoaded && residentCells >= maxResident)
            {
                item.Target = CellState.Unloaded;
                _workItems[index] = item;
                continue;
            }

            residentCells++;
            if (item.Target is CellState.Active or CellState.Simulated)
            {
                int cost = Math.Max(1, item.Definition.SimulationCost);
                if (simulationCost + cost > maxSimulation)
                {
                    item.Target = CellState.Visual;
                    _workItems[index] = item;
                }
                else
                {
                    simulationCost += cost;
                }
            }
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

        if (_indexedRegion != null)
        {
            foreach (Vector2I grid in new List<Vector2I>(_instancedCells.Keys))
            {
                UnloadCell(grid, capture: true);
            }
            foreach (var pair in new List<KeyValuePair<Vector2I, CellState>>(_cellStates))
            {
                if (pair.Value == CellState.Loading && _cellsByGrid.TryGetValue(pair.Key, out var oldCell))
                {
                    CancelCellLoad(pair.Key, oldCell);
                }
            }
            _cellStates.Clear();
            _loadRetries.Clear();
        }

        _cellsByGrid.Clear();
        _alwaysLoadedCells.Clear();
        if (ActiveRegion != null)
        {
            foreach (var cell in ActiveRegion.Cells)
            {
                if (cell != null)
                {
                    _cellsByGrid[cell.GridPosition] = cell;
                    if (cell.AlwaysLoaded)
                    {
                        _alwaysLoadedCells.Add(new KeyValuePair<Vector2I, CellDefinition>(cell.GridPosition, cell));
                    }
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
        _interestSpeed = velocity.Length();
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

    private string CellKey(Vector2I gridPos) => $"{_indexedRegion?.Id ?? ActiveRegion?.Id}_{gridPos.X}_{gridPos.Y}";

    private void StepCell(Vector2I gridPos, CellDefinition cellDef, CellState current, CellState target)
    {
        switch (current)
        {
            case CellState.Unloaded:
                if (target != CellState.Unloaded)
                {
                    StartCellLoad(gridPos, cellDef);
                }
                break;

            case CellState.Loading:
                if (target == CellState.Unloaded)
                {
                    CancelCellLoad(gridPos, cellDef);
                }
                else if (_instancesThisFrame < MaxCellInstancesPerFrame)
                {
                    AdvanceCellLoad(gridPos, cellDef);
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

    private void StartCellLoad(Vector2I gridPos, CellDefinition cellDef)
    {
        if (string.IsNullOrWhiteSpace(cellDef.ScenePath))
        {
            RecordLoadFailure(gridPos, cellDef, "cell has no scene path");
            return;
        }

        if (_loadRetries.TryGetValue(gridPos, out var retry))
        {
            if (retry.Attempts > MaxLoadRetries || _streamTimeSeconds < retry.RetryAtSeconds)
            {
                return;
            }
        }

        _cellStates[gridPos] = CellState.Loading;
        if (!string.IsNullOrWhiteSpace(cellDef.CollisionScenePath))
        {
            _loader!.RequestLoad(cellDef.CollisionScenePath);
            _collisionSceneRequested.Add(gridPos);
        }
        else
        {
            RequestMainScene(gridPos, cellDef);
        }
    }

    private void AdvanceCellLoad(Vector2I gridPos, CellDefinition cellDef)
    {
        if (!string.IsNullOrWhiteSpace(cellDef.CollisionScenePath)
            && !_collisionProxies.ContainsKey(gridPos))
        {
            CellLoadStatus collisionStatus = _loader!.GetLoadStatus(cellDef.CollisionScenePath);
            if (collisionStatus == CellLoadStatus.Failed)
            {
                RecordLoadFailure(gridPos, cellDef, $"collision proxy '{cellDef.CollisionScenePath}' failed");
                return;
            }
            if (collisionStatus != CellLoadStatus.Loaded) return;

            Node? proxy = _loader.InstantiateCell(cellDef.CollisionScenePath);
            if (proxy == null)
            {
                RecordLoadFailure(gridPos, cellDef, $"collision proxy '{cellDef.CollisionScenePath}' could not instantiate");
                return;
            }

            proxy.Name = $"CollisionProxy_{gridPos.X}_{gridPos.Y}";
            AddChild(proxy);
            _collisionProxies[gridPos] = proxy;
            _instancesThisFrame++;
            RequestMainScene(gridPos, cellDef);

            if (_instancesThisFrame >= MaxCellInstancesPerFrame) return;
        }

        if (!_mainSceneRequested.Contains(gridPos))
        {
            RequestMainScene(gridPos, cellDef);
            return;
        }

        CellLoadStatus status = _loader!.GetLoadStatus(cellDef.ScenePath);
        if (status == CellLoadStatus.Failed)
        {
            RecordLoadFailure(gridPos, cellDef, $"scene '{cellDef.ScenePath}' failed");
        }
        else if (status == CellLoadStatus.Loaded)
        {
            InstantiateCell(gridPos, cellDef);
        }
    }

    private void RequestMainScene(Vector2I gridPos, CellDefinition cellDef)
    {
        if (_mainSceneRequested.Add(gridPos))
        {
            _loader!.RequestLoad(cellDef.ScenePath);
        }
    }

    private void CancelCellLoad(Vector2I gridPos, CellDefinition cellDef)
    {
        if (_mainSceneRequested.Remove(gridPos))
        {
            _loader!.CancelLoad(cellDef.ScenePath);
        }
        if (_collisionSceneRequested.Remove(gridPos) && !string.IsNullOrWhiteSpace(cellDef.CollisionScenePath))
        {
            _loader!.CancelLoad(cellDef.CollisionScenePath);
        }

        RemoveCollisionProxy(gridPos);
        _cellStates[gridPos] = CellState.Unloaded;
    }

    private void RecordLoadFailure(Vector2I gridPos, CellDefinition cellDef, string reason)
    {
        int attempts = _loadRetries.TryGetValue(gridPos, out var prior) ? prior.Attempts + 1 : 1;
        double delay = RetryBaseDelaySeconds * Math.Pow(2d, Math.Min(attempts - 1, 5));
        _loadRetries[gridPos] = new LoadRetry(attempts, _streamTimeSeconds + delay);

        _mainSceneRequested.Remove(gridPos);
        _collisionSceneRequested.Remove(gridPos);
        RemoveCollisionProxy(gridPos);
        _cellStates[gridPos] = CellState.Unloaded;

        string retryMessage = attempts <= MaxLoadRetries
            ? $"retry {attempts}/{MaxLoadRetries} in {delay:0.0}s"
            : "retry budget exhausted";
        GD.PushWarning($"[WorldStreamer] Cell {gridPos} ({cellDef.ScenePath}) load failed: {reason}; {retryMessage}.");
    }

    private void InstantiateCell(Vector2I gridPos, CellDefinition cellDef)
    {
        var cellNode = _loader!.InstantiateCell(cellDef.ScenePath);
        if (cellNode == null)
        {
            RecordLoadFailure(gridPos, cellDef, "loaded scene could not instantiate");
            return;
        }

        _instancesThisFrame++;
        AddChild(cellNode);
        _instancedCells[gridPos] = cellNode;
        _mainSceneRequested.Remove(gridPos);
        _collisionSceneRequested.Remove(gridPos);
        _loadRetries.Remove(gridPos);

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
        RemoveCollisionProxy(gridPos);
        _mainSceneRequested.Remove(gridPos);
        _collisionSceneRequested.Remove(gridPos);
        _cellStates[gridPos] = CellState.Unloaded;
    }

    private void RemoveCollisionProxy(Vector2I gridPos)
    {
        if (_collisionProxies.Remove(gridPos, out var proxy) && IsInstanceValid(proxy))
        {
            proxy.QueueFree();
        }
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
        private readonly HashSet<string> _failedRequests = new(StringComparer.Ordinal);

        public void RequestLoad(string scenePath)
        {
            Error result = ResourceLoader.LoadThreadedRequest(scenePath);
            if (result == Error.Ok)
            {
                _failedRequests.Remove(scenePath);
            }
            else
            {
                _failedRequests.Add(scenePath);
            }
        }

        public bool IsLoadComplete(string scenePath)
        {
            return ResourceLoader.LoadThreadedGetStatus(scenePath) == ResourceLoader.ThreadLoadStatus.Loaded;
        }

        public CellLoadStatus GetLoadStatus(string scenePath)
        {
            if (_failedRequests.Contains(scenePath)) return CellLoadStatus.Failed;

            return ResourceLoader.LoadThreadedGetStatus(scenePath) switch
            {
                ResourceLoader.ThreadLoadStatus.Loaded => CellLoadStatus.Loaded,
                ResourceLoader.ThreadLoadStatus.InProgress => CellLoadStatus.Loading,
                ResourceLoader.ThreadLoadStatus.Failed => CellLoadStatus.Failed,
                _ => CellLoadStatus.NotRequested,
            };
        }

        public Node? InstantiateCell(string scenePath)
        {
            var res = ResourceLoader.LoadThreadedGet(scenePath) as PackedScene;
            return res?.Instantiate();
        }
    }
}
