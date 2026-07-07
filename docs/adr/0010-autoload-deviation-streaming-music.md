# ADR 0010: Streaming & Music Registered as Autoloads (Deviation from §3.4)

**Date:** 2026-07-07
**Status:** Resolved — the persistent Systems node now hosts streaming, music, and the PlayerController
**Context:** Code-review findings M2 and V2. Doc §3.4 explicitly lists streaming and music as **not** autoloads ("Streaming, UI root, spawners, dialogue bridge — Systems node children").

> **Update (V2 fix):** `scenes/game/Game.tscn` now has a persistent `Systems` node whose children are
> `PlayerController` (`PlayerControllerNode`), `WorldStreamer` (`WorldStreamerNode`), and `MusicManager`
> (`MusicManagerNode`). `WorldStreamer` and `MusicManager` were removed from `[autoload]`. This closes
> M2 and resolves V2 (the PlayerController was previously never instantiated). The migration plan below
> is retained for historical context.

---

## Context & Problem Statement

The architecture document reserves autoloads for services that must survive scene changes and be reachable from anywhere (EventBus, GameDirector, SaveService, WorldClock, WeatherSystem, AudioDirector, InputContextService). Everything else — streaming, UI root, spawners, the dialogue bridge — is meant to be a child of a persistent `Systems` node under `Game.tscn`, giving the same app-lifetime without polluting the global autoload namespace.

The current project registers `WorldStreamer` and `MusicManager` as autoloads in `project.godot`, and `Game.tscn` has no `Systems` node implementing the intended pattern. This diverges from §3.4.

## Considered Options

1. **Move now:** Add a `Systems` node to `Game.tscn`, reparent `WorldStreamerNode` and `MusicManagerNode` under it, and remove the two autoload entries.
2. **Record the deviation and migrate deliberately (chosen):** Keep them as autoloads for now, register the divergence here, and move them when the persistent `Game.tscn` "Systems" node is introduced as a tracked change that can be validated in-editor.

## Decision Outcome

We adopt Option 2 for now. The nodes already follow interface-based registration (`WorldStreamerNode` registers `IWorldStreamer`; the music manager exposes its domain model), so consumers do not depend on their autoload status — the migration is a scene-graph move, not an API change.

### Migration plan (to close M2)

1. Add a `Systems` node (plain `Node`) to `scenes/game/Game.tscn`, created once and never freed.
2. Attach `WorldStreamerNode` and `MusicManagerNode` as children of `Systems`.
3. Remove `WorldStreamer` and `MusicManager` from `[autoload]` in `project.godot`.
4. Verify boot in-editor: both still register with the `Services` locator in `_EnterTree`, and `_ExitTree` unregistration remains symmetric.

This move touches `Game.tscn` (a scene) and must be validated by launching the editor/game, which is why it is deferred to a change that can be run rather than made blind.

## Consequences

### Positive
- Documents the known divergence so it isn't mistaken for an oversight (doc §22).
- No behavioral change; consumers already depend on interfaces, not autoload identity.

### Negative
- Two services remain in the global autoload namespace until the Systems-node migration lands.
