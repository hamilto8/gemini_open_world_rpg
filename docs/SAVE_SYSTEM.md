# Save and Load System

This document is the handoff contract for durable, forward-compatible saves. Runtime save code lives in
`src/Meridian/Core/Save`; gameplay modules participate through `ISaveParticipant` and plain DTOs in
`SaveDTOs.cs`.

## Runtime flow

1. `SaveGameAsync` validates the slot and calls every participant's `CaptureState()` on the calling
   thread. Godot callers must call it from the main thread.
2. The returned DTO snapshots are serialized on a worker thread with the source-generated
   `SaveJsonContext`.
3. JSON is written to `<slot>.json.tmp` with write-through enabled and `Flush(true)`.
4. The temp file atomically replaces the slot and the former slot becomes `<slot>.json.bak`. The
   managed fallback uses same-volume rename-overwrite where `File.Replace` is unsupported.
5. Loading tries the primary, then its backup if the primary is missing, corrupt, or unsupported.
6. The root migration pipeline upgrades the container in memory. Participant payloads migrate
   independently, then restore in `SaveRestoreOrder` order.

The synchronous `SaveGame` API remains for tools/tests and blocks until the same durable operation
finishes. Frame-sensitive runtime code should await `SaveGameAsync` and surface its completion/failure
through the UI save-status channel.

`Flush(true)` makes file contents durable using portable .NET APIs. .NET does not expose a portable
directory-fsync API, so the final directory-entry durability guarantee remains platform/filesystem
dependent. The replacement and backup operations are same-directory/same-volume.

## Adding a participant

- Use a globally unique, stable `ParticipantId`. Never derive it from a display name.
- Return a detached DTO containing primitives, collections, and stable content ids—never Godot nodes,
  resources, signals, or registry object references.
- Register the DTO and its nested collection shapes in `SaveJsonContext`.
- Pick the appropriate `SaveRestoreOrder` band. Dependencies must restore before consumers.
- Start `StateVersion` at 1. When the DTO changes incompatibly, increment it and implement
  `ISaveStateMigrator`. Migrations accept old JSON and return current-schema JSON; keep fixtures for
  every supported historical shape.
- Register/unregister symmetrically with `ISaveService` from the feature's persistent composition owner.
- Add a real JSON round-trip test through `SaveService`, not only a direct `CaptureState`/`RestoreState`
  test.

Duplicate participant ids fail during registration. Capturing the wrong DTO type fails the save rather
than writing a misleading file.

## Restore order

| Band | Order | Examples |
|---|---:|---|
| Global flags | 10 | consequence/world flags |
| Environment | 20 | clock and weather |
| Region warm-up | 30 | region selection and collision-first streaming |
| World objects | 40 | cell deltas and persistent vehicles |
| Narrative | 50 | quests, journal, factions, discoveries |
| Progression | 60 | level, XP, perks |
| Inventory | 70 | inventory and equipped weapon instance |
| Equipment | 80 | armor slots and quick slots |
| Player transform | 90 | transform/vitals if separated |
| Possession | 100 | final on-foot/vehicle possession |
| Settings | 200 | player preferences |

`PlayerControllerNode` uses `IPlayerRestoreCoordinator` at the possession band. Composition must provide
an implementation that warms the saved region, maps the currently possessed entity to a stable id, and
resolves that id after warm-up. Do not use localized names or transient instance ids.

`VehiclePersistenceService` owns `IPersistentVehicle` adapters. A streamed vehicle can register after a
load; its pending DTO is applied then. The composition owner supplies current-region and possessed-id
callbacks and registers the service as a save participant.

## Content drift policy

The default is `UnknownSaveContentPolicy.PreserveAndWarn`:

- an unregistered participant's raw JSON and version are retained and written back on the next save;
- unknown inventory items become inert zero-weight placeholders while retaining instance payload;
- unknown equipment remains in its saved slot without invented stat effects;
- quick-slot content ids remain bound but can be reported as unavailable;
- discoveries for unloaded/unregistered terminals are retained until the terminal registers;
- vehicles not currently streamed remain pending until their stable id registers.

This quarantine policy prevents a temporary DLC/mod/build-configuration change from destroying player
state. Critical/server-authoritative products can construct `SaveService` with
`UnknownSaveContentPolicy.RejectLoad`; rejection is preflighted before any participant mutates.

Participant migration failures and malformed current participant payloads fail the load and are logged.
The original file is never rewritten during load.

## Compatibility fixtures and verification

Archived saves live under `tests/Meridian.Tests/Fixtures/Saves` and are copied into the test output.
`v1_player_and_flags.json` is an immutable v1-format fixture: it proves root migration, ordered typed
restore, and preservation of a retired module. Never regenerate an archived fixture with the current
serializer; add a new fixture when a shipped schema changes.

Run:

```sh
dotnet test Meridian.sln --no-restore
```

The focused suites are `SaveTests` and `SaveParticipantTests`.
