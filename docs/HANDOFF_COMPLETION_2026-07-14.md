# Project Meridian — Engineering Handoff Completion

**Date:** July 14, 2026  
**Baseline:** `HANDOFF_READINESS_AUDIT_2026-07-14.md`  
**Verdict:** **Ready for human-led story, art, audio, world, and UI-content production.** This is an engineering/content-framework handoff, not a claim that the gray-box game is release-ready or that target-hardware performance certification has been completed.

## Acceptance evidence

- `dotnet build Meridian.sln --no-restore -m:1 /nodeReuse:false`: passed, 0 warnings/errors.
- `dotnet test Meridian.sln --no-restore -m:1 /nodeReuse:false`: **267 passed, 0 failed, 0 skipped**.
- Godot **4.6 stable Mono** headless boot: clean; content runtime composed; content validation passed; `Boot -> MainMenu -> Playing`; player possession succeeded.
- `dotnet format Meridian.sln whitespace --verify-no-changes --no-restore`: passed.
- `git diff --check`: passed.

Run the same gates through `scripts/validate-handoff.sh`. Set `GODOT_BIN` when Godot is not installed at the default macOS Mono path.

## Blocker closure

### Runtime composition and authoring

The shipped game scene now composes typed quest, dialogue, NPC, scheduled-event, faction, fast-travel, progression, item, weapon, loot, weather, movement, vehicle, and region registries. `ContentRuntimeNode` owns the persistent runtime services and routes quest/dialogue/event effects through one condition/action vocabulary.

The Harbor sample exercises a scheduled NPC, branching conversation, quest acceptance, objective/reward data, faction effects, a clock event, fast travel, progression, combat targets, a vehicle, weather, and streaming. Dockmaster Vale is physically interactable in the gray-box scene; the dialogue presenter reads authored nodes/choices and accepting the choice starts the sample quest.

Authoring: `CONTENT_AUTHORING.md`.

### Save compatibility and completeness

The root save format is versioned independently from per-participant payloads. The service validates contiguous migrations, preserves or rejects unknown participant data by explicit policy, rejects duplicate participants and unsafe slot names, falls back to backups, orders restoration, and writes asynchronously through a durable temp/flush/atomic-replace workflow.

Participants cover world flags/deltas, time/weather/forecast, region preparation, player transform/vitals/possession, inventory/equipped weapon instances, equipment, quick slots, progression/perks, quests, faction reputation, discoveries/fast travel, persistent vehicle fleets, and player settings. Archived v1 fixtures exercise migration and compatibility.

Authoring and migration policy: `SAVE_SYSTEM.md`.

### UX/UI and accessibility

The UI is scene/theme-driven rather than constructed as a monolithic C# overlay. A registry/back stack hosts main menu, loading, pause, settings, controls, inventory, equipment, journal, map, and dialogue screens. The shell owns modal state, focus, cancellation, pause, safe-area scaling, localization resources, controller-family glyphs, conflict-aware keyboard rebinding, defaults restoration, feedback queues, and hold-progress presentation.

Settings persist independently of save slots and are consumed by text scale, subtitles, camera/input options, and audio buses. The authored HUD remains compatible with player health/stamina/reticle and vehicle telemetry.

Authoring: `UI_CONTENT_AUTHORING.md`.

### World streaming and region schema

Streaming candidate lookup is spatial rather than a full-region per-frame scan. Work is deterministically prioritized and constrained by elapsed main-thread milliseconds, instantiation count, resident-cell limit, and relative simulation cost. Fast movement expands lookahead; optional lightweight collision proxies load before full cells. Cancelled/failed requests recover through loader cancellation and bounded exponential retry.

Region/cell resources expose biome/tags/audio/default-weather metadata, priorities, simulation cost, residency budgets, collision proxy paths, and always-loaded hubs. Region transitions flush the prior region under its correct persistence key before rebuilding the spatial index.

Authoring and profiling: `WORLD_STREAMING_AND_ENVIRONMENT.md`.

### Gameplay vertical slice

Player, vehicles, and combat targets use the same damage mitigation/result pipeline and lifecycle events. Player and vehicle death/respawn hooks are content-replaceable; vehicles persist fuel/health/position, use collision-checked exit candidates, and return possession safely. Hitscan self-exclusion and target-relative hit zones remain in the shared weapon path. Weapon instances, upgrades, inventory, equipment, and quick slots preserve instance data through saves.

Final animation graphs, enemy behavior, authored VFX, audio cues, and encounter design remain content work.

### Environment and living world

The world clock drives NPC schedules, scheduled actions, day/night light/sky presentation, and a deterministic weighted weather forecast. Weather resources author fog/light/gameplay modifiers and outgoing state transitions. Forecast cursor state is saved, so reloads remain deterministic. The scene presentation layer is replaceable without changing simulation.

### Validation and release gates

Boot validation covers every typed index, ID naming, duplicate/orphan assets, required directories, region scenes/collision proxies, dialogue graphs, quest objectives/rewards, NPC references/schedules, scheduled events, factions, progression curves, and weather transition references. Unit coverage increased from 239 to 267 tests, including save migrations/participants, content composition, UI settings/navigation primitives, damage lifecycle, and deterministic forecast behavior.

The repository supplies repeatable build/test/format/diff/runtime gates. Target-hardware 60 fps certification and final stress captures remain a per-content-milestone acceptance task because art density, animation, shaders, audio, and final world scale do not exist yet.

## Deliberately deferred to the human content team

- Final regions, traversal layout, encounters, NPC catalog, quest prose/volume, dialogue performance, and story structure.
- Character/environment/vehicle art, animation graphs, VFX, lighting art direction, UI art direction, music, voice, and SFX.
- Final screen view-model adapters and product-specific inventory/map/journal presentation.
- Platform certification, target-hardware captures, localization languages, controller icon art, and accessibility user testing.

Those tasks can now be added through documented resources, indexes, scenes, themes, localization, and replaceable presentation hooks without rewriting the core framework.
