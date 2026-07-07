# Code Review Findings — Project Meridian

**Date:** 2026-07-06
**Reviewer:** Claude (Claude Code) — review only, no fixes applied
**Baseline:** commit `6d2c3f1` plus uncommitted working tree
**Reference:** *Project Meridian — Game Architecture & Systems Design Document v1.0* (Gemini_open-world-arpg-architecture.md.pdf) and `docs/ARCHITECTURE_REVIEW.md`

This document lists issues found reviewing the C# codebase against the architecture document and Godot 4.6 C# best practices. **No code was changed.** Each finding is written to be independently actionable by a follow-up AI/developer. Build status at review time: `dotnet build` clean, all 41 unit tests pass — note that most Critical findings below are runtime/boot issues that compile fine and are not covered by the current tests.

Severity legend: **Critical** = broken at runtime or violates a load-bearing architectural rule; **High** = incorrect behavior likely; **Medium** = fragile/deviates from doc; **Low** = polish, style, minor perf.

---

## Critical

### C1. `ServicesNode` wipes the EventBus registration at boot
- **Files:** [ServicesNode.cs:13](src/Meridian/Core/ServicesNode.cs), [EventBusNode.cs:15](src/Meridian/Core/EventBusNode.cs), `project.godot` `[autoload]` order
- Autoload order is `EventBus` → `ServicesNode` → `GameDirector` → …. `EventBusNode._EnterTree()` registers `IEventBus`, then `ServicesNode._EnterTree()` calls `Services.Reset()`, **erasing that registration**. Any later `Services.Get<IEventBus>()` (e.g. `GameDirectorNode.TransitionTo`, deferred from `_Ready`) throws `InvalidOperationException` at boot.
- **Fix direction:** remove the `Reset()` from `ServicesNode._EnterTree()` (or make ServicesNode the *first* autoload; better yet, drop ServicesNode entirely and reset only in test teardown). Also decide whether the shutdown-time `Reset()` in `_ExitTree` is safe given other autoloads' `_ExitTree` still call `Services.TryGet`.

### C2. `IWeatherSystem` is never implemented or registered — weather save/restore and console command are dead
- **Files:** [WeatherSystemNode.cs](src/Meridian/Environment/WeatherSystemNode.cs), [IWeatherSystem.cs](src/Meridian/Environment/IWeatherSystem.cs), [WorldClockNode.cs:823-852](src/Meridian/Environment/WorldClockNode.cs), [DebugConsole.cs:525](src/Meridian/UI/DebugConsole.cs)
- `WeatherSystemNode` does **not** implement `IWeatherSystem` (it has `TransitionTo(WeatherProfile)` instead of `ChangeWeather`/`ForceWeather`/`CurrentWeatherId`) and never registers with `Services`. Consequences: `WorldClockNode.CaptureState()` always saves placeholder weather (`"clear"`), `RestoreState` never restores weather, and the debug console `set-weather` command always fails.
- **Fix direction:** implement `IWeatherSystem` on `WeatherSystemNode` (mapping weather ids to `WeatherProfile`s), register it in `_EnterTree`, and unify `TransitionTo` with the interface methods.

### C3. Vehicles cannot be driven: input context blocks all movement actions while in a vehicle
- **Files:** [PlayerControllerNode.cs](src/Meridian/Core/PlayerControllerNode.cs) (`CompileInputFrame`), [InputContextService.cs](src/Meridian/Input/InputContextService.cs), [VehicleAvatar.cs:117](src/Meridian/Vehicles/VehicleAvatar.cs)
- `CompileInputFrame` only reads on-foot actions (`move_forward`, `move_left`, …). When `VehicleAvatar.Interact` pushes the `Vehicle` context, those actions are all disallowed (`Vehicle` context registers only `vehicle_throttle` etc., which nothing reads), so `MoveX`/`MoveY` are always 0 and the vehicle never receives throttle or steering. Only `interact` (exit) works.
- **Fix direction:** either compile vehicle actions into `InputFrame` when `CurrentContext == Vehicle`, or allow the movement actions in the Vehicle context. Decide one input mapping and delete the dead `vehicle_*` registrations if unused.

### C4. Vehicle throttle sign is inverted relative to the controller's input mapping
- **File:** [VehicleAvatar.cs:117](src/Meridian/Vehicles/VehicleAvatar.cs)
- `PlayerControllerNode` maps `move_forward` to `MoveY += 1`. `VehicleAvatar` computes `throttleInput = -_lastInput.MoveY`, so even once C3 is fixed, pressing forward gives negative throttle (no acceleration) and pressing backward drives the vehicle forward.
- **Fix direction:** use `+MoveY` as throttle (matching `MovementMotor`, which maps forward via `new Vector3(MoveX, 0, -MoveY)`), and add a shared convention comment/test so the two consumers can't drift again.

### C5. EventBus subscriptions without stored/disposed tokens (violates the doc's "non-negotiable" rule)
- **Files:** [NpcLifeController.cs:31](src/Meridian/NPC/NpcLifeController.cs), [WeatherSystemNode.cs:28](src/Meridian/Environment/WeatherSystemNode.cs)
- The architecture doc (§3.3) and `docs/ARCHITECTURE_REVIEW.md` §2.2 mandate: every Node that subscribes stores its `IDisposable` tokens and disposes them in `_ExitTree()`. `NpcLifeController` (a scene node that **will** be freed by streaming) discards its token — after the NPC is freed, `HourChangedEvent` dispatch will invoke a handler on a disposed node (`ObjectDisposedException` / dangling-handler crash, the exact failure mode the rule exists to prevent). `WeatherSystemNode` (autoload) also discards its token; lower blast radius but same rule violation, and its `OnMinuteTick` is an empty stub.
- **Fix direction:** store tokens and dispose in `_ExitTree()` in both; `MinimalHud.cs` already demonstrates the correct pattern. Consider a project-wide grep/analyzer check for `Subscribe<` calls whose result is discarded.

---

## High

### H1. Headshot detection compares hit height against the *camera's* position
- **File:** [WeaponController.cs:94](src/Meridian/Combat/WeaponController.cs)
- `if (hitPosition.Y - collider.GetViewport().GetCamera3D().GlobalPosition.Y > 0.5f) zone = HitZone.Head;` — the hit zone is derived from the shooter's camera height, not the target's anatomy. A shot at a tall target's knees from a crouched camera counts as a headshot; also `GetCamera3D()` can be null (NRE). Doc §6.1 specifies hit-zone tags on the target.
- **Fix direction:** derive the zone from the target (e.g. hit position relative to the collider's origin/height, or dedicated hit-zone child Areas), and null-guard.

### H2. Weapon raycast can hit the shooter
- **File:** [WeaponController.cs:79](src/Meridian/Combat/WeaponController.cs)
- `PhysicsRayQueryParameters3D.Create(globalPosition, endPosition)` has no `Exclude` list, so the ray can collide with the firing avatar's own `CharacterBody3D` (especially firing from a camera-anchored muzzle behind the shoulder).
- **Fix direction:** pass the shooter's RID(s) in `query.Exclude`.

### H3. Perk/gear modifiers on unregistered stats silently do nothing
- **Files:** [StatBlock.cs](src/Meridian/Core/StatBlock.cs) (`GetStat` early-returns 0 when `_baseValues` lacks the id), [ProgressionManager.cs:88](src/Meridian/Core/ProgressionManager.cs)
- `StatBlock.GetStat` returns `0f` for any stat without a base value, *without* consulting modifiers. The `fast_reload` perk adds a modifier for `reload_speed`, which is never registered as a base stat — the perk consumes a skill point and has zero effect, silently. Same trap for any equipment modifier targeting a non-default stat.
- **Fix direction:** either treat missing base as 0 and still run the modifier pipeline, or fail loudly (assert/log) when a modifier targets an unknown stat; register the full derived-stat catalogue from data (doc §8).

### H4. Save-state participant dispatch by substring of `ParticipantId`
- **File:** [SaveService.cs:192-194](src/Meridian/Core/Save/SaveService.cs)
- `DeserializeState` picks the DTO type via `id.Contains("Player")` / `"Flag"` / `"Time"` / `"Weather"`. Any participant whose id matches none of these serializes fine but **silently restores nothing** (`null` → skipped). Concretely: `WorldStateStore` has `ParticipantId = "WorldStateStore"`, which matches none of the substrings, so **cell deltas (looted containers, etc.) are captured into save files but never restored**. This is an active data-loss bug today, not just a latent one.
- **Fix direction:** have `ISaveParticipant` declare its DTO type (e.g. `Type StateType { get; }` or generic participant base) or register id→type mapping explicitly; remove the substring convention. Also log (don't swallow) unknown ids.

### H5. `SaveService.LoadGame` swallows all exceptions and does not fall back to `.bak` on corruption
- **File:** [SaveService.cs](src/Meridian/Core/Save/SaveService.cs) (`LoadGame`)
- The comment says "Fall back to backup if original is missing/corrupt" but the `.bak` path is only tried when the primary file is *missing*; a corrupt primary returns `false` without ever reading the backup. The blanket `catch (Exception) { return false; }` also hides participant `RestoreState` bugs with no logging.
- **Fix direction:** try primary, on parse failure try `.bak`; log the exception before returning false. Consider letting participant restore failures surface per-participant instead of aborting silently mid-restore (partial restores currently leave mixed state).

### H6. `GameDirectorNode` boot flow reloads the main scene and relies on fragile deferred overload dispatch
- **File:** [GameDirectorNode.cs:22](src/Meridian/Core/GameDirectorNode.cs)
- `_Ready` defers `TransitionTo(MainMenu, "res://scenes/game/Game.tscn")`, calling `ChangeSceneToFile` on the scene that is already the running main scene — a redundant full scene reload at every boot (and the state is labeled `MainMenu` while loading the gameplay scene). Two same-arity `TransitionTo` overloads exist solely to appease `CallDeferred`'s Variant marshalling; overload dispatch through Godot's `CallDeferred(string, ...)` is fragile.
- **Fix direction:** use `Callable.From(() => TransitionTo(GameState.MainMenu, ...)).CallDeferred()` and delete the int overload; skip `ChangeSceneToFile` when the target scene is already current; revisit Boot→MainMenu→Playing mapping vs doc §3.4.

### H7. `WorldStreamerNode` per-frame full-grid scan with LINQ, no hysteresis, no time-slicing, no prefetch ring
- **File:** [WorldStreamerNode.cs](src/Meridian/World/WorldStreamerNode.cs) (`_Process`, `UpdateCellLifecycle`)
- Doc §4.3 requires hysteresis (exit radius > enter radius), a distance/heading-weighted priority queue, per-frame millisecond instantiation budget, and a prefetch ring. The implementation rescans every cell in region bounds every frame, does `Cells.FirstOrDefault(...)` (linear search + closure allocation) per transitioning cell, has zero hysteresis (cells at a ring boundary will thrash load/unload), and instantiates every completed cell in the same frame (arrival bursts = frame spike). Also: a freshly instanced cell enters `Visual` state with `ProcessMode.Inherit` (processing enabled) — `Visual` should be render-only per the doc; and a cell whose target becomes `Unloaded` while still `Loading` is marked `Unloaded` while the threaded load continues (leaked request, then instantiated-never state confusion).
- **Fix direction:** pre-index cells by `GridPosition` in a dictionary; add enter/exit radii pairs; queue instantiations against a per-frame budget; set `ProcessMode.Disabled` on instantiation into `Visual`; handle load-cancel; track the interest point with velocity lookahead (§4.4).

### H8. Modifier semantics mismatch: `PercentAdd` divides by 100 but callers pass fractions
- **Files:** [ModifierSystem.cs](src/Meridian/Core/ModifierSystem.cs), [ProgressionManager.cs:88](src/Meridian/Core/ProgressionManager.cs), [WeatherProfile.cs](src/Meridian/Data/WeatherProfile.cs)
- `ModifierSystem.Calculate` treats `PercentAdd` values as whole percents (`/100`). But `fast_reload` "+15%" is authored as `Add 0.15`, and `WeatherProfile.MoveSpeedModifier` documents `-0.15f for 15% slow` applied as a flat `Add` to `move_speed` (5.0 base) — i.e. a 3% slow. Percent-style intents are being applied as flat adds throughout.
- **Fix direction:** pick one convention (recommend: fraction-based `PercentAdd`, values like 0.15), update `Calculate`, and re-author the call sites; add unit tests pinning the convention. Also note the enum XML doc says order is "Override > Multiply > PercentAdd > Add" while `Calculate` applies Add → PercentAdd → Multiply → Override — align the docs with the code.

---

## Medium

### M1. `Meridian.csproj` missing mandated compiler settings
- **File:** [Meridian.csproj](Meridian.csproj)
- Doc §2.2 and `ARCHITECTURE_REVIEW.md` §2.1 require `<Nullable>enable</Nullable>`, `LangVersion` latest, and warnings-as-errors (via `Directory.Build.props`). None are present in `Meridian.csproj` (the *test* project has Nullable but the game project doesn't). The code uses `?`/`!` annotations that currently compile in a nullable-oblivious context, so the annotations are not being checked at all. Also `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` is enabled with no unsafe code — remove it.
- **Fix direction:** add a `Directory.Build.props` with `Nullable`, `LangVersion=latest`, `TreatWarningsAsErrors` (scoped to own code), drop `AllowUnsafeBlocks`; then fix the nullable warnings that surface.

### M2. Autoload set diverges from the doc's "deliberately few" list
- **File:** [project.godot](project.godot) `[autoload]`
- Doc §3.4 explicitly lists streaming and music as **not** autoloads ("Streaming, UI root, spawners, dialogue bridge — Systems node children"). `WorldStreamer` and `MusicManager` are registered as autoloads. There is also no persistent `Game.tscn` "Systems" node implementing that pattern.
- **Fix direction:** move `WorldStreamerNode` and `MusicManagerNode` under a Systems node in the persistent game scene, or record the deviation in a new ADR.

### M3. Hardcoded gameplay data in code (violates "data over code")
- **Files:** [ProgressionManager.cs](src/Meridian/Core/ProgressionManager.cs) (`ApplyPerkModifier` hardcodes `fast_reload`/`thick_skin`; `+2` skill points per level), [MovementMotor.cs](src/Meridian/Player/MovementMotor.cs) (jump cost 15, sprint drain 25/s, regen 15/s, aim speed ×0.6, move_speed baseline `/5.0`), [StatBlock.cs](src/Meridian/Core/StatBlock.cs) (default stat set in constructor), [NpcScheduler.cs](src/Meridian/NPC/NpcScheduler.cs) (fixed 8/17/22 schedule), [DummyTarget.cs](src/Meridian/Combat/DummyTarget.cs) (zone multipliers)
- Doc §1.5 principle 1 and §5.5/§8: stamina costs, perk effects, schedules, and hit-zone multipliers should be Resource-driven. These are acceptable as phase scaffolding but each is a content-blocking hardcode; the perk list in particular means new perks require editing core C#.
- **Fix direction:** introduce `PerkResource`/`ScheduleResource`/stat-definition resources incrementally; at minimum move magic numbers into exported fields on existing profiles.

### M4. `PlayerControllerNode.CaptureState` saves placeholder data
- **File:** [PlayerControllerNode.cs:140-147](src/Meridian/Core/PlayerControllerNode.cs)
- Region id is hardcoded `"harbor_town"`, health/stamina hardcoded `100f`, `PossessedGuid` is `"avatar_player"` or `""`. Round-tripping a save loses actual health/stamina and region. `RestoreState` also ignores `Health`/`Stamina`.
- **Fix direction:** capture from the avatar's `StatBlock` and the streamer's current region; restore symmetrically.

### M5. Quest system gaps: rewards never granted; enumeration-mutation hazard
- **File:** [QuestManager.cs](src/Meridian/Quests/QuestManager.cs)
- `QuestDefinition.RewardItemIds/RewardItemCounts` are never consumed — completing a quest grants nothing. `IncrementObjective` mutates `_questStates[questId]` (via `CheckQuestCompletion`) while iterating `_questStates.Where(...)`; updating an existing key doesn't invalidate Dictionary enumerators on current .NET, but it's fragile — a future `Remove`/`Add` inside the loop throws. QuestManager is also not an `ISaveParticipant` (quest progress is lost on save/load).
- **Fix direction:** snapshot active quest ids before the loop (`.ToList()`), dispatch rewards through the inventory on completion, add a save participant.

### M6. Parallel-array data contracts in Resources
- **Files:** [QuestDefinition.cs](src/Meridian/Data/QuestDefinition.cs), [LootTableResource.cs](src/Meridian/Data/LootTableResource.cs)
- Objectives and loot entries are modeled as 4 parallel arrays (`ObjectiveIds`/`Types`/`Targets`/`RequiredCounts`; `ItemIds`/`Weights`/`MinQuantities`/`MaxQuantities`). Content authors *will* desync lengths; `QuestManager.IncrementObjective` indexes all four arrays and will throw `ArgumentOutOfRangeException` on a desynced quest. `LootTableResource.RollDrop` half-guards (checks Weights only).
- **Fix direction:** define per-entry nested Resources (`QuestObjectiveResource`, `LootEntryResource` with `[GlobalClass]`) and arrays thereof; have `ContentValidator` verify them.

### M7. `LootTableResource` — `new Random()` per call, and rolling logic lives in the data layer
- **File:** [LootTableResource.cs:36,46](src/Meridian/Data/LootTableResource.cs)
- Two fresh time-seeded `Random` instances per roll: bursts of drops produce identical results, and it allocates. Doc layering also puts roll *logic* in the domain, not on the definition Resource.
- **Fix direction:** use `Random.Shared` (or an injected seeded RNG for determinism/testing), and consider moving `RollDrop` into a domain `LootService`.

### M8. `MovementMotor` gravity via `PhysicsServer3D.AreaGetParam(space, …)`
- **File:** [MovementMotor.cs:43](src/Meridian/Player/MovementMotor.cs)
- `AreaGetParam` expects an *area* RID; passing the `World3D.Space` RID is incorrect API usage. Godot 4.6 provides `CharacterBody3D.GetGravity()` (velocity vector, since 4.3), or read `ProjectSettings` `physics/3d/default_gravity`.
- **Fix direction:** replace with `_body.GetGravity()` and integrate its vector (also gives correct behavior inside gravity-override Areas).

### M9. Vehicle physics: no gravity, brake uses a "just pressed" flag as if held, per-frame event spam
- **File:** [VehicleAvatar.cs](src/Meridian/Vehicles/VehicleAvatar.cs)
- `Velocity = forward * _currentSpeed` with no Y component: the vehicle ignores gravity and will hover off ramps/slopes (doc §11: vehicles are physics bodies). Braking checks `JumpPressed` (compiled from `IsActionJustPressed`) so brake force applies for a single physics frame per press despite the "held" comment. `VehicleSpeedChangedEvent` + `VehicleFuelChangedEvent` publish every physics frame while possessed — 120 events/sec through the bus for HUD data that changes slowly; publish on meaningful delta instead. `Unboard` casts `(IPossessable)_boardedAvatar` unchecked.
- **Fix direction:** integrate gravity when `!IsOnFloor()`; add a `BrakeHeld` field to `InputFrame`; threshold event publication; safe-cast with a guard.

### M10. `IPossessable` couples to concrete `PlayerControllerNode`
- **File:** [IPossessable.cs](src/Meridian/Core/IPossessable.cs)
- `OnPossessed(PlayerControllerNode controller)` forces every possessable to depend on the concrete Node class, defeating the interface-decoupling goal (§3.5) and blocking headless tests of possessables.
- **Fix direction:** change the parameter to `IPlayerController`.

### M11. `CameraRig` reads raw input directly and hardcodes sensitivity
- **File:** [CameraRig.cs](src/Meridian/Player/CameraRig.cs)
- `UpdateCamera` polls `Input.IsActionPressed("move_forward"/…)` directly, bypassing `InputContextService` (movement keys will rotate the character even when a UI/dialogue context should block them) and duplicating the controller's input mapping. Mouse sensitivity `0.003f` is hardcoded (doc: camera params live on `CameraModeResource`; sensitivity belongs in settings). `_Ready` also force-captures the mouse — a side effect that will fight any menu/UI code.
- **Fix direction:** drive rotation decisions from the `InputFrame` passed down by the avatar; move sensitivity to data/settings; let a UI/GameDirector layer own `Input.MouseMode`.

### M12. `ContentValidator` doesn't validate content
- **File:** [ContentValidator.cs](src/Meridian/Core/Validation/ContentValidator.cs)
- Doc §19.10: the validator should scan `data/` for broken references, duplicate ids, and unregistered content. The implementation only checks that folders exist and that `.tres` files contain the substring `[gd_resource`. It will pass a completely broken database. (Several required directories it checks for — `addons/`, `shaders/`, `localization/` — don't exist in the repo, so it also fails vacuously today.)
- **Fix direction:** parse `.tres` ids per category folder, check duplicates and dangling `ExtResource` paths, and cross-check against index resources once those exist (`data/indexes/` is referenced but the folder is absent).

### M13. `WorldStreamer` cell delta capture via stringly `Get("IsOpen")`
- **File:** [WorldStreamerNode.cs](src/Meridian/World/WorldStreamerNode.cs) (`CaptureCellDeltas`/`ApplyCellDeltas`)
- Persistence contract is "any direct child with an `IsOpen` Variant property", keyed by node name — only scans one level deep, silently skips nested interactables, breaks on node rename, and supports only booleans. Doc §4.3/§16 call for GUID-keyed dynamic-object records.
- **Fix direction:** define an `IPersistentSceneObject` interface (`string PersistentId; Dictionary Capture(); Restore(...)`) and walk descendants.

### M14. Test/mocking types shipped in production namespaces + `CS8785` suppressed
- **Files:** [IVehicleHandlingProfile.cs](src/Meridian/Data/IVehicleHandlingProfile.cs) (`BasicVehicleHandlingProfile`), [IWeaponDefinition.cs](src/Meridian/Combat/IWeaponDefinition.cs) (`BasicWeaponDefinition`), [IItemDefinition.cs](src/Meridian/Items/IItemDefinition.cs) (`BasicItemDefinition`), [Meridian.Tests.csproj](tests/Meridian.Tests/Meridian.Tests.csproj)
- "Basic mock implementation … for unit testing" classes live in shipping assemblies. The test project references the *whole* Godot game assembly and silences `CS8785` (source-generator failure) to make that work — this hides real generator errors. Doc §2.2 anticipated extracting engine-free code into `Meridian.Core` for headless testing.
- **Fix direction:** move mocks into the test project; longer term split domain code into an engine-free project referenced by both, and remove the `NoWarn`.

### M15. `EventBus` half-hearted thread-safety
- **File:** [EventBus.cs](src/Meridian/Core/EventBus.cs)
- Handler lists are locked, but the `_handlers` dictionary itself is read/written without synchronization (`TryGetValue` + indexer set in `Subscribe`, unlocked `TryGetValue` in `Publish`, `lock (_handlers)` only in `Clear`). Either it's single-threaded (then drop all locks and the per-publish `ToArray()` snapshot allocation — doc wants near-zero-allocation dispatch) or it must be actually thread-safe (`ConcurrentDictionary` + immutable handler arrays). Current state is misleading. `Publish` also allocates an array snapshot per publish call.
- **Fix direction:** document main-thread-only and simplify, or make it genuinely concurrent. Cache the snapshot/use a versioned list to avoid per-publish allocation.

### M16. `UpgradeBench` publishes success without doing anything
- **File:** [UpgradeBench.cs](scenes/world/shared/UpgradeBench.cs)
- `Interact` never checks or deducts `RequiredMaterialId`, never touches a `WeaponInstance`, yet publishes `UpgradeAttemptedEvent(..., success: true)`. Doc §6.4 requires an atomic inventory transaction (`InventoryTransaction` exists and is tested — it's just not used here). `CanInteract` also ignores its own "must possess a weapon" comment and returns true whenever a player exists.
- **Fix direction:** wire the bench through `InventoryTransaction` + the player's inventory/equipped `WeaponInstance`; only report `success` truthfully. (Also rename the event parameter `success` → `Success` — record positional params are PascalCase everywhere else.)

### M17. `InventoryTransaction.DeductStep.Rollback` loses instance data
- **File:** [InventoryTransaction.cs](src/Meridian/Items/InventoryTransaction.cs)
- Rollback recreates a plain `ItemInstance(definitionId, count)` — any payload/`WeaponInstance` state (ammo, mods, upgrade level) removed by the deduct is not restored. Fine for stackable materials, wrong for unique items.
- **Fix direction:** capture the actual removed instances in `Apply()` and re-add those objects on rollback (requires `RemoveItem` to return the removed instances).

---

## Low

### L1. Registrations of concrete types in the service locator
- `Services.Register<WorldStreamerNode>(this)` and `Services.Register<MusicManagerNode>(this)` ([WorldStreamerNode.cs](src/Meridian/World/WorldStreamerNode.cs), [MusicManagerNode.cs](src/Meridian/Audio/MusicManagerNode.cs)) register concrete Node types; doc §3.5 says consumers depend on interfaces. Add `IWorldStreamer` / `IMusicManager` (or reuse `IAudioDirector`).

### L2. Missing `Services` unregistration symmetry
- Nodes register in `_EnterTree` but almost none unregister in `_ExitTree` (e.g. `PlayerControllerNode`, `EventBusNode`, `GameDirectorNode`). Harmless for app-lifetime autoloads, but `PlayerControllerNode` is described as a Systems-node child; a stale locator entry after scene teardown yields use-after-free. Add `Services.Unregister<T>()` and call it symmetrically.

### L3. `StatBlock` per-query LINQ and event churn
- `GetStat` allocates a `Where` iterator per dirty recompute; `SetBaseStat` is called every physics frame for stamina drain/regen, firing `StatChanged` (and a Godot signal emission via `StatBlockNode`) 60+ times/sec. Consider a per-stat modifier index and a dedicated resource-pool (current/max) model for health/stamina rather than base-stat mutation.

### L4. `WorldClock` dead `_timeScale`
- [WorldClock.cs](src/Meridian/Environment/WorldClock.cs) stores `_timeScale` but never uses it (`WorldClockNode` applies its own copy). Remove one or the other; as-is `SetTimeScale` on the pure class is a no-op trap for tests.

### L5. `DummyTarget.ScalePulse` timer callback can outlive the node
- [DummyTarget.cs](src/Meridian/Combat/DummyTarget.cs): `GetTree().CreateTimer(0.1f).Timeout += () => Scale = ...` — if the node is freed within 0.1s, the lambda touches a disposed object. Use a `Tween` owned by the node or check `IsInstanceValid(this)`.

### L6. `InputFrame` XML doc claims immutability
- [IPossessable.cs](src/Meridian/Core/IPossessable.cs): `InputFrame` is documented "Immutable" but is a mutable struct with public fields. Either make it `readonly record struct` built via a constructor, or fix the comment. Also `LookX/LookY` are never populated by `CompileInputFrame` (dead fields — camera look bypasses the frame entirely, see M11).

### L7. `InventoryModel` auto-registers stub definitions
- [InventoryModel.cs](src/Meridian/Items/InventoryModel.cs) `AddItem` fabricates a `BasicItemDefinition` (MaxStack 99, weight 0.1) for unknown ids "to ease testing" — in production this masks missing content instead of failing validation. Gate behind a flag or remove; let the ContentValidator catch unknown ids.

### L8. Dialogue choices execute raw `Action` side effects
- [DialogueService.cs](src/Meridian/Dialogue/DialogueService.cs): `DialogueChoice.ActionEffect` is an arbitrary delegate; doc §3.6/§13 route dialogue outcomes through the shared `GameActionResource` vocabulary (which doesn't exist yet — neither does `ConditionResource`). Fine as Phase-6 scaffolding, but flag: the two cross-cutting primitives the whole doc leans on are still unimplemented.

### L9. Audio/footstep systems are print-only stubs
- `AudioDirectorNode.PlaySoundCue`, `MusicManagerNode.SetTension` (bus updates commented out), and `FootstepMaterialDetectorNode.PlayFootstep` (never called by anything; material detection hardcodes group names `metal`/`dirt`) log instead of playing audio. Track as TODOs so they aren't mistaken for working systems.

### L10. `PerfHud` runs always-on `_Process` UI updates
- [PerfHud.cs](src/Meridian/UI/PerfHud.cs) rewrites three label strings every frame (allocation churn the doc's frame-budget section warns about). Throttle to ~4 Hz.

### L11. `PlayerAvatar` polls the HUD every physics frame
- [PlayerAvatar.cs](scenes/player/PlayerAvatar.cs) calls `_hud?.UpdatePlayerStats(...)` per physics tick while `MinimalHud`'s header claims "event-driven widgets, no per-frame polling". Drive it from `StatBlockNode.StatChanged` / bus events instead.

### L12. Duplicate namespace import & misc style
- [ProgressionManager.cs](src/Meridian/Core/ProgressionManager.cs) has `using Meridian.Core;` inside `namespace Meridian.Core` and an unused `stats` parameter on `AddXp`. `Game.cs` stores `_hud`/`_console` fields it never uses. `ItemResource.IItemDefinition.Behaviors` re-allocates a `List<object>` on every access (make it lazy/cached). `AudioCueProfile`/several resources use raw string paths where typed `AudioStream`/`Texture2D` exports would be idiomatic.

### L13. `.uid` files should be committed
- The untracked `*.cs.uid` files generated by Godot 4.4+ are part of the project contract (they keep script references stable) and should be committed alongside their scripts — include them in this commit rather than ignoring them.

---

## Positive observations (keep these patterns)

- Domain logic (StatBlock, InventoryModel, QuestManager, WorldClock, LocomotionStateMachine) is genuinely engine-free and unit-tested — the doc's layering is respected in structure.
- `MinimalHud` shows the correct EventBus token store-and-dispose pattern.
- `SaveService` atomic write (tmp → bak rotate → rename) matches the doc's §16 requirements.
- `InventoryTransaction` implements validate-all/apply-all/rollback exactly as §7.5 specifies — it just needs call sites (see M16).
- Definition-vs-instance split (`ItemResource`/`ItemInstance`, `WeaponResource`/`WeaponInstance`, id-based references) follows §3.7 correctly.

## Suggested fix order for the follow-up AI

1. C1 (boot), C2 (weather service), C5 (subscription tokens) — unblock correct runtime behavior.
2. C3/C4 + M9 (vehicle input/physics) as one unit, with a regression test on input mapping.
3. H3/H8 (modifier semantics) before authoring more content on top of wrong math.
4. H4/H5 + M4 (save pipeline) as one unit, with round-trip tests.
5. H1/H2 (combat raycast), H6, H7.
6. M-tier structural items, then L-tier polish.

---

# Test Suite Review (addendum, 2026-07-06)

Second pass focused on `tests/Meridian.Tests/` (17 test classes, 41 tests, all green): do the tests actually exercise the production code the design document says must be testable? The doc (§3.2) names the "most bug-prone systems — inventory transactions, quest branching, modifier math, save/restore" as the reason domain logic is plain C#. Verdict: the domain tests that exist are mostly real, but a meaningful subset are tautologies, several *pin* known production bugs as correct behavior, and the systems that shipped broken (vehicles, save restore dispatch, weather) are exactly the ones with no genuine coverage. The xUnit-instead-of-GoDotTest choice is properly recorded in ADR-0003 and is not a finding.

## High

### T1. Tautological tests that never invoke production code (false coverage)
- **Files:** [DamagePipelineTests.cs](tests/Meridian.Tests/Combat/DamagePipelineTests.cs), [WeaponCombatTests.cs](tests/Meridian.Tests/Combat/WeaponCombatTests.cs), [VehicleTests.cs](tests/Meridian.Tests/Vehicles/VehicleTests.cs)
- `MitigationPipeline_ShouldApplyMultiplierAndArmorCorrectly` computes `50 * 2 - 10` inline in the test body and asserts `90` — it never calls `DummyTarget.ApplyDamage` or any pipeline code (its own `MockDamageableTarget` is declared and never used). `WeaponController_Firing_ShouldConsumeAmmo` executes `weaponInstance.CurrentAmmo--` in the test and asserts 9 — it tests the `--` operator. `WeaponController_Reloading_...` re-implements `CompleteReload`'s algorithm inline instead of calling it. Both `VehicleTests` re-implement throttle/fuel math locally and never touch `VehicleAvatar`. These tests pass no matter what the production code does — which is precisely why C3/C4 (undrivable vehicle) and the H1 hit-zone bug shipped green.
- **Root cause:** the logic under test lives inside Godot `Node` classes (`WeaponController`, `VehicleAvatar`, `DummyTarget`) that can't be instantiated headlessly (ADR-0003's own constraint).
- **Fix direction:** extract the fire/reload cycle, damage mitigation, and vehicle throttle/fuel/brake integration into plain C# domain classes (e.g. `WeaponRuntime`, `DamagePipeline`, `VehicleMotorModel`) consumed by the thin Nodes, then rewrite these tests against the real code. Delete the inline-math tests rather than keeping them as decoys.

### T2. Tests that pin or mask known production bugs
- **Files/cases:**
  - [SystemsDepthTests.cs](tests/Meridian.Tests/Progression/SystemsDepthTests.cs) `ProgressionManager_ShouldHandleLevelUps...` calls `stats.SetBaseStat("reload_speed", 1.0f)` *in the test* before unlocking `fast_reload`. Production never registers `reload_speed`, so the perk is a silent no-op (finding H3) — the test green-lights the bug by performing the setup the game is missing.
  - [WeatherTests.cs](tests/Meridian.Tests/Environment/WeatherTests.cs) `Weather_StateModifiers_...` documents "-15% = -1.5" as a flat `Add` against a hand-picked base of 10 — encoding the percent-vs-flat confusion of H8. Meanwhile [ModifierSystemTests.cs](tests/Meridian.Tests/Core/ModifierSystemTests.cs) pins `PercentAdd` value `10f` = 10% (whole-percent). The suite thus asserts *both* conventions; whichever way H8 is resolved, one of these tests must consciously change.
  - [SaveTests.cs](tests/Meridian.Tests/Core/SaveTests.cs) uses only participant ids (`"PlayerState"`, `"WorldFlags"`) that happen to satisfy `SaveService`'s substring dispatch. No test registers a participant with a non-matching id (e.g. `"WorldStateStore"`), so the H4 restore data-loss is invisible. [StreamerTests.cs](tests/Meridian.Tests/World/StreamerTests.cs) `CaptureAndRestoreState_ShouldRoundTripSerialization` hands the DTO object directly from `CaptureState()` to `RestoreState()`, bypassing SaveService JSON dispatch entirely — the only broken link in that chain is the one skipped.
  - [InputContextServiceTests.cs](tests/Meridian.Tests/Input/InputContextServiceTests.cs) `IsActionAllowed_ShouldRespectActiveContext` asserts `vehicle_throttle` allowed / `move_forward` blocked in the Vehicle context — pinning the exact contract that makes vehicles undrivable (C3), because no test covers the `PlayerControllerNode`→`InputContextService` integration where the mismatch lives.
- **Fix direction:** when fixing H3/H4/H8/C3, update these tests *deliberately* (they will fail or, worse, keep passing); add the missing adversarial cases listed in T4. A save round-trip test through actual JSON with a `"WorldStateStore"`-style id should be the first new test written.

## Medium

### T3. Shared static `Services` locator + default xUnit parallelism is a race hazard
- **Files:** [ServicesTests.cs](tests/Meridian.Tests/Core/ServicesTests.cs), [InventoryTests.cs](tests/Meridian.Tests/Items/InventoryTests.cs), [EnvironmentTests.cs](tests/Meridian.Tests/Environment/EnvironmentTests.cs); no `xunit.runner.json` / `[CollectionDefinition]` in the project
- xUnit runs test classes in parallel by default. Three test classes call `Services.Reset()` in ctor/`Dispose` against the process-global, **non-thread-safe** `Dictionary` inside `Services`, while domain classes under test in *other* classes (`InventoryModel.TriggerChanged`, `QuestManager`, `FastTravelNetwork`, `ProgressionManager.AddXp`) concurrently call `Services.TryGet<IEventBus>`. Today the collisions are mostly benign; as the suite grows this becomes intermittent corruption/flakes that are miserable to diagnose.
- **Fix direction:** either serialize the affected classes into one xUnit collection (or disable parallelization project-wide), or — better, and more aligned with doc §3.5 — inject `IEventBus` into domain classes instead of having *domain-layer* code reach into the static locator at all (the doc reserves the locator for scene/service layers; domain classes silently depending on global state is itself a layering deviation worth fixing).

### T4. Coverage gaps on the doc's named bug-prone systems
No tests exist for any of the following (all headless-testable today, no extraction needed):
- **StatBlock directly:** `TickModifiers` expiry, `RemoveModifierBySource`, dirty-cache recompute after base change, and — the H3 detector — a modifier targeting an unregistered stat.
- **SaveService failure paths:** corrupt primary file with valid `.bak` (H5 claims fallback; a test proves it doesn't), `DeleteSave`, temp-file cleanup after a failed write, unknown participant id in the file, `LoadGame` on missing slot returning false (only the happy path is covered).
- **ProgressionManager:** multi-level-up from one large `AddXp`, `MaxLevel` clamp, XP cost recomputation ordering across the level boundary, `UnlockPerk` with zero points, duplicate perk unlock.
- **QuestManager:** multi-objective quests, mixed objective types, a second active quest completing mid-`IncrementObjective` (M5 enumeration hazard), desynced parallel arrays (M6 — currently throws `ArgumentOutOfRangeException`; a test should decide the intended behavior).
- **InventoryModel:** stack splitting across `MaxStack` boundaries, exact-weight-limit boundary, `RemoveItem` spanning multiple stacks, the auto-stub-definition behavior (L7 — deliberate or not, it's load-bearing and untested).
- **InventoryTransaction:** rollback of instance-carrying items losing payload (M17), multi-step failure after partial apply.
- **LocomotionStateMachine:** sprint denied at zero stamina (the `currentStamina > 0` branch), the `Aiming` overlay flag, crouch-vs-jump precedence (crouch currently wins — intended?), `StateChanged` event emission, the walk/run threshold boundary (`2.51f`).
- **WorldClock:** midnight rollover incrementing `DayCounter`, phase boundaries at hours 5/8/17/20, `SetTime` backwards within a day, and `SetTimeScale` — a test would immediately expose that the pure class's `_timeScale` is dead (L4).
- **EventBus:** multiple handlers for one event, unsubscribe-during-publish reentrancy (the snapshot makes it safe — pin that), double `Dispose` idempotency.
- **World streaming:** `ICellLoader` exists explicitly (per its own XML doc) to "permit unit tests to mock and simulate cell instancing," yet no test uses it — because the ring/lifecycle logic is embedded in `WorldStreamerNode._Process` (H7). Extract the lifecycle state machine into a pure class and test enter/exit/hysteresis transitions with a fake loader.
- **EquipmentModel:** equipping an item whose behavior targets a different slot, replacing an occupied slot, items with no equippable behavior.

## Low

### T5. Test hygiene
- [EnvironmentTests.cs](tests/Meridian.Tests/Environment/EnvironmentTests.cs) registers a real `SaveService(Path.GetTempPath())` in the locator that nothing under test uses (dead arrangement), and points it at the shared temp root rather than a per-test directory (the `SaveService` ctor creates directories as a side effect).
- `DamagePipelineTests.MockDamageableTarget` is dead code (see T1).
- `SystemsDepthTests`/`WeaponCombatTests`/`VehicleTests` depend on the `Basic*` mock classes shipped in the production assembly (already M14) — moving the mocks to the test project is a prerequisite for cleaning this up.
- Float assertions use exact equality (`Assert.Equal(1.15f, ...)`); fine for today's exact arithmetic, but prefer the `Assert.Equal(expected, actual, precision)` overloads so intentional math changes don't cascade into brittle failures.
- One-scenario-per-system smoke tests (`ContentPolishTests`, `QuestDialogueTests.NpcScheduler...`) assert only interior values, never boundary hours/edges — cheap to extend while writing T4 cases.

## Test-suite fix order

1. T2's save round-trip test through real JSON (exposes H4) and the modifier-convention decision test (forces H8 resolution) — write these *before* fixing the production bugs so the fixes have failing tests to turn green.
2. T1: extract Node-bound logic to domain classes, replace tautology tests (pairs with C3/C4/H1 fixes).
3. T3: serialize or de-static the locator usage before the suite grows.
4. T4 backlog, highest-value first: StatBlock unregistered-stat, SaveService `.bak` fallback, QuestManager desynced arrays, streamer lifecycle extraction.

---

# Resolution Log (addendum, 2026-07-07)

Fixes for the findings above were applied. Build is clean (`dotnet build` — 0 warnings/0 errors) and the suite grew from 41 to **98 passing tests**. Disposition per finding:

**Critical:** C1 fixed — `ServicesNode` deleted (removed from autoloads); the static locator needs no boot reset, so it no longer wipes the `EventBus` registration. C2 fixed — `WeatherSystemNode` now implements/registers `IWeatherSystem` (id→profile map, `ChangeWeather`/`ForceWeather`). C3 fixed — Vehicle input context allows the shared movement actions + `brake`; dead `vehicle_*` registrations removed. C4 fixed — throttle uses `+MoveY`, extracted into a tested `VehicleMotorModel`. C5 fixed — `NpcLifeController` stores/disposes its token; `WeatherSystemNode`'s dead subscription removed.

**High:** H1 — hit zone derived from the target via `IHitZoneResolver`, camera null-risk gone. H2 — shooter RID excluded from the ray. H3 — `StatBlock.GetStat` runs the modifier pipeline for missing bases; `reload_speed` registered. H4 — `ISaveParticipant.StateType` replaces substring dispatch; unknown ids logged. H5 — `.bak` fallback on corrupt primary, per-participant restore, logging. H6 — `Callable.From` deferred, int overload deleted, redundant scene reload skipped. H7 — dictionary cell index, `StreamingRings` hysteresis, per-frame instantiation budget, `ProcessMode.Disabled` on Visual, load-cancel, velocity lookahead. H8 — `PercentAdd` is now a fraction convention project-wide; docs aligned.

**Medium:** M1 (dropped `AllowUnsafeBlocks`; `Directory.Build.props` already present), M2 (ADR‑0010 records the autoload deviation + migration plan), M3 (movement/skill-point magic numbers → profile data), M4, M5 (rewards granted, save participant, enumeration snapshot), M6 (nested `QuestObjective`/`QuestReward`/`LootEntry` resources), M7 (`Random.Shared`/injectable), M8 (`GetGravity()`), M9, M10, M11 (`CameraRig` reads `InputFrame`, data sensitivity, controller owns `MouseMode`), M12 (validator parses `.tres` for dangling refs + duplicate ids), M13 (`IPersistentSceneObject` + descendant walk), M14 (mocks moved to test project; `NoWarn` removed by supplying `GodotProjectDir`), M15 (documented main-thread, zero-alloc, reentrancy-safe bus), M16 (`UpgradeBench` transacts truthfully via `IInventoryProvider`), M17 (rollback restores real instances).

**Low:** L1 (`IWorldStreamer`/`IMusicManager`), L2 (unregister symmetry on Systems-scoped nodes), L4 (`WorldClock` owns time scale), L5 (`DummyTarget` uses an owned `Tween`), L6 (`InputFrame` doc corrected; `LookX/Y` populated), L7 (auto-stub gated behind `AllowUnregisteredItems`), L8/L9 (dialogue-action + audio stubs marked TODO), L10 (`PerfHud` throttled to ~4 Hz), L11 (HUD driven by `StatChanged`), L12 (unused usings/fields/params removed; `ItemResource.Behaviors` cached), L13 (`.uid` files confirmed committable, not git-ignored). L3 left as-is — an explicit "consider" micro-optimization, not a defect.

**Tests:** T1 (domain extraction: `VehicleMotorModel`, `DamagePipeline`, `WeaponRuntime`, `StreamingRings`; tautology tests rewritten), T2 (bug-pinning tests updated deliberately; JSON round-trip with a non-substring id added), T3 (parallelization disabled — shared static locator race), T4 (coverage added across StatBlock, EventBus reentrancy, WorldClock, QuestManager, SaveService `.bak`, streaming rings, locomotion boundaries, inventory stack split), T5 (dead arrangements removed, precision overloads).

---

# Verification Review (addendum, 2026-07-07)

Independent re-review of the codebase after the Resolution Log fixes landed (`0bdf692`). Method: rebuilt (`dotnet build` — 0 warnings/errors), reran the suite (98/98 passing), and read every changed production and test file, verifying each claimed fix against the code rather than the log.

## Verdict on the fix pass

**All Resolution Log claims check out.** Spot-verified in code, not just by diff: C1 (ServicesNode deleted from autoloads and repo), C2 (`WeatherSystemNode : IWeatherSystem`, registered/unregistered symmetrically, id→profile index), C3/C4 (Vehicle context allows movement actions; `VehicleMotorModel` uses forward-positive throttle, and `VehicleTests.NegativeThrottle_ShouldNotDriveForward` pins the old bug), C5 (a project-wide grep shows every `Subscribe<` call now stores its token; `NpcLifeController` disposes in `_ExitTree`), H1/H2 (`IHitZoneResolver` on the target + shooter RID excluded), H3 (`StatBlock.GetStat` runs the pipeline for unregistered bases; pinned by `StatBlockTests.Modifier_OnUnregisteredStat_StillApplies`), H4/H5 (`ISaveParticipant.StateType` drives source-generated (de)serialization; corrupt-primary → `.bak` fallback; per-participant restore with logging; unknown ids surfaced), H6 (typed `Callable.From` deferred boot, no redundant scene reload), H7 (`StreamingRings` hysteresis with real boundary tests, dictionary cell index, per-frame instantiation cap, `ProcessMode.Disabled` on Visual, load-abandon), H8 (fraction convention consistent across `ModifierSystem`, perks, weather, and tests — the previously contradictory tests now agree), and the M/L/T tiers as described. The test-suite rewrite is genuine: the former tautologies now exercise `WeaponRuntime`/`VehicleMotorModel`/`DamagePipeline`/`StreamingRings`, quest state round-trips through the real JSON context, and parallelization is disabled with a rationale comment.

One correction to the original review: **M1 was partially wrong** — `Directory.Build.props` (with `Nullable`, `LangVersion latest`, targeted `WarningsAsErrors`) has existed since the first commit; only the `AllowUnsafeBlocks` removal was a real fix. Noted for the record.

## New findings

The fixes repaired the systems' internals; what remains broken is the *wiring into the running game*. Both Criticals below predate the fix commit and were missed by the two earlier review rounds as well — unit tests can't see them because they live in `project.godot` and scene composition.

### V1 (Critical). The InputMap defines no gameplay actions
- **File:** [project.godot](project.godot) `[input]` section
- The only defined action is `console_toggle`. Every action the game polls — `move_forward`, `move_backward`, `move_left`, `move_right`, `jump`, `sprint`, `crouch`, `aim`, `fire`, `interact`, `brake` (in `PlayerControllerNode.CompileInputFrame`) — does not exist in Godot's InputMap. At runtime each `Input.IsActionPressed(...)` call pushes an "InputMap action doesn't exist" error (every physics frame) and returns false: **the player cannot move, and the vehicle cannot be driven, regardless of all the C3/C4 fixes**. No code registers actions via `InputMap.AddAction` either.
- **Fix direction:** author the full action set with key/mouse/gamepad bindings in `project.godot` (WASD, Space=jump, Shift=sprint, Ctrl=crouch, RMB=aim, LMB=fire, E=interact, plus a vehicle brake key), or register them at boot from a data-driven bindings resource (doc §17's rebinding requirement suggests the latter eventually). Note `look` is only an InputContextService concept (checked via `IsActionAllowed` alone), so it does not need an InputMap entry.

### V2 (Critical). `PlayerControllerNode` is never instantiated
- **Files:** [project.godot](project.godot) (not an autoload), [Game.tscn](scenes/game/Game.tscn) (not a node), no `new PlayerControllerNode` anywhere
- The possession chain's head does not exist in the running game: `IPlayerController` is never registered, so `PlayerAvatar.AutoPossess` no-ops, no `InputFrame` is ever compiled, `IInventoryProvider` is absent (UpgradeBench can't transact), the `PlayerState` save participant never registers (player position/health/stamina are silently absent from saves), and `WeatherSystemNode`'s player-modifier push finds no player. Doc §5.1 places the PlayerController "persistent, under Systems" — the persistent Systems node itself (ADR-0010's stated migration target) also doesn't exist yet.
- **Fix direction:** add a `Systems` node to `Game.tscn` with `PlayerControllerNode` under it (preferred — matches doc §3.4 and ADR-0010's migration plan), or register it as an autoload; then verify possession, movement, save round-trip, and vehicle boarding by actually running the game.

### V3 (Medium). Weather modifier can strand on a stale StatBlock across possession changes
- **File:** [WeatherSystemNode.cs](src/Meridian/Environment/WeatherSystemNode.cs) (`ApplyWeatherModifiers`/`RemoveWeatherModifiers`)
- Both apply and remove resolve the *currently possessed* entity at call time. Sequence: weather applies a `move_speed` modifier to the on-foot avatar → player boards a vehicle (possessed entity is now the vehicle) → weather changes → `RemoveWeatherModifiers` resolves the vehicle, finds no `StatBlock` child, removes nothing, then sets `_activeWeatherModifier = null` — the old modifier is now permanently stuck on the avatar's StatBlock (only a `RemoveModifierBySource` sweep could clear it). Symmetrically, a transition while nothing is possessed (e.g. during restore, before `AutoPossess`) applies nothing, and nothing reapplies on possession change.
- **Fix direction:** remember the actual `StatBlockNode` the modifier was pushed to and remove from that stored reference; publish/subscribe a possession-changed event to migrate or reapply weather modifiers.

### V4 (Medium). Combat and upgrade flows are unreachable in-game (fixed internals, no wiring)
- **Files:** [PlayerAvatar.cs](scenes/player/PlayerAvatar.cs), [PlayerControllerNode.cs](src/Meridian/Core/PlayerControllerNode.cs) (`EquippedWeapon`), scene files
- `InputFrame.FirePressed` is compiled but no consumer reads it — `PlayerAvatar` has no `WeaponController`/`EquipmentHolder`, no scene instantiates a weapon, and `PlayerControllerNode.EquippedWeapon` is never assigned. Consequently `UpgradeBench.CanInteract` is always false in-game, and the H1/H2/M16 fixes are exercisable only from tests. Acknowledged scaffolding, but track it: the doc's §5.2 avatar composition (EquipmentHolder) and §6.3 weapon runtime are not yet reachable from play.

### V5 (Low). GameDirector's state machine never reaches `Playing`
- **File:** [GameDirectorNode.cs](src/Meridian/Core/GameDirectorNode.cs)
- Boot transitions to `MainMenu` and stops; gameplay runs while `CurrentState == MainMenu`. Doc §3.4 specifies Boot → MainMenu → Loading → Playing (→ Paused). Harmless today, but anything gating behavior on `GameState.Playing` will silently never fire.

### V6 (Low). ContentValidator fails against its own repository and is never invoked
- **File:** [ContentValidator.cs](src/Meridian/Core/Validation/ContentValidator.cs)
- The `.tres` parsing (dangling `ext_resource` paths, duplicate ids) is now real, but `ValidateDirectoryStructure` requires `addons/`, `shaders/`, and `localization/` folders that don't exist in this repo — run against the project root, it reports errors by construction. It also has no caller outside tests (no editor tool, CI step, or boot check).
- **Fix direction:** create the missing folders (with `.gitkeep`) or trim the required list to what the project actually mandates, and wire the validator into CI or an editor menu action (doc §19.10).

### V7 (Low). Residual placeholders and micro-allocations
- [WorldClockNode.cs](src/Meridian/Environment/WorldClockNode.cs) `CaptureState` still hardcodes `WeatherElapsed = 0` / `ForecastSeed = 1234` (restore ignores both — harmless until a forecast system exists).
- [WorldStreamerNode.cs](src/Meridian/World/WorldStreamerNode.cs) allocates a `new StreamingRings(...)` every `_Process` frame (cache it and rebuild only when the exported radii change), and the instantiation budget is count-based (`MaxCellInstancesPerFrame`) rather than the doc's millisecond budget (§4.3) — fine at current scale, revisit when cells get heavy.
- [VehicleAvatar.cs](src/Meridian/Vehicles/VehicleAvatar.cs) `Unboard` early-returns if `_boardedAvatar` isn't `IPossessable`, leaving the player permanently stuck in the vehicle (unreachable with the current avatar, but a guard that strands the player is the wrong failure mode); boarding pushes the Vehicle input context inside `Interact` while a direct `Possess()` call would bypass it — the context push/pop would sit more safely in `OnPossessed`/`OnReleased`.

## Suggested order

1. V1 + V2 together — the "make the game actually playable" pair; verify by running the game and walking around, then boarding and driving the vehicle.
2. V3 (weather/possession lifecycle) once possession actually occurs in-game.
3. V4 wiring when combat becomes the active phase; V5–V7 opportunistically.

---

# Resolution Log — Verification findings (addendum, 2026-07-07)

V1–V7 fixed. Build clean (0 warnings/0 errors), 98/98 tests pass. Note: V1–V3 live in `project.godot`/scene composition and Node classes that can't be instantiated headlessly, so they are verified by build + code review rather than new unit tests (adding Node-instantiation tests would reintroduce the T1 tautology anti-pattern).

- **V1 (Critical) — InputMap actions.** Added [`InputMapBootstrap`](src/Meridian/Input/InputMapBootstrap.cs), invoked from `InputContextServiceNode._EnterTree`, which registers the full gameplay action set (WASD move, Space jump, Shift sprint, Ctrl crouch, E interact, R reload, Space brake, LMB fire, RMB aim, Esc menu) via the typed Godot `InputMap` API if not already present. Chosen over hand-editing `project.godot`'s `Object(...)` serialization because that can't be validated without the editor and a malformed entry would break the whole project; code registration is compile-checked and idempotent. `look` intentionally has no InputMap entry (context-only).
- **V2 (Critical) — PlayerController instantiated.** `Game.tscn` now has a persistent `Systems` node hosting `PlayerController` (`PlayerControllerNode`), plus `WorldStreamer` and `MusicManager` moved out of `[autoload]` into it — closing M2 and implementing ADR-0010's migration. Possession, `IPlayerController`/`IInventoryProvider` registration, the `PlayerState` save participant, and the weather modifier push now have a real owner.
- **V3 (Medium) — weather modifier lifecycle.** `PlayerControllerNode` publishes a `PossessionChangedEvent` on possess/release; `WeatherSystemNode` remembers the exact `StatBlockNode` it pushed to (removes from that reference, not the currently-possessed one) and subscribes to migrate the modifier across boarding/unboarding. Subscription token disposed in `_ExitTree`.
- **V4 (Medium) — combat wiring.** Tracked with an explicit TODO in `PlayerAvatar` (no `EquipmentHolder`/`WeaponController` yet; `FirePressed`/`EquippedWeapon` unconsumed). The underlying `WeaponRuntime`/`DamagePipeline` are implemented and tested; only the §5.2/§6.3 scene composition remains, deferred as acknowledged scaffolding.
- **V5 (Low) — GameState reaches Playing.** `Game.cs` (the gameplay scene) defers a transition to `GameState.Playing`, ordered after the director's boot→MainMenu, giving Boot → MainMenu → Playing.
- **V6 (Low) — ContentValidator.** The required folders (`addons/`, `shaders/`, `localization/`, `data/indexes/`) already exist with `.gitkeep`, so the validator no longer fails vacuously; added a `validate-content` DebugConsole command as a real caller.
- **V7 (Low) — residuals.** `WorldStreamerNode` caches `StreamingRings` and rebuilds only when the exported radii/margin change (no per-frame allocation); `VehicleAvatar` moves the input-context push/pop into `OnPossessed`/`OnReleased` and no longer strands the player on unboard (releases the vehicle even if the tracked body isn't `IPossessable`); the `WorldClockNode` forecast placeholders are marked with a TODO.
