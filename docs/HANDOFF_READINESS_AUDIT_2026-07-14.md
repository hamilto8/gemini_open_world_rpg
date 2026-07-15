# Project Meridian — Handoff Readiness Audit

**Date:** July 14, 2026  
**Scope:** Full repository review against `open-world-arpg-architecture.md`, prior review notes, runtime composition, gameplay, save/load, content authoring, performance foundations, and UX/UI.  
**Original verdict:** **Not ready for story/art/audio content handoff.** The repository was a credible, well-tested framework prototype and gray-box interaction loop, but several required framework capabilities remained disconnected or incomplete.

> **Remediation update:** the blockers in this audit were addressed in the subsequent engineering pass. The current framework-handoff verdict, evidence, authoring entry points, and deliberately deferred creative work are recorded in [HANDOFF_COMPLETION_2026-07-14.md](HANDOFF_COMPLETION_2026-07-14.md). Keep the findings below as the baseline that drove that work.

## What Was Verified

- Godot 4.6 Mono / .NET 8 project structure and warning-as-error build configuration.
- Typed event bus, services, input contexts, data registries, definition/instance separation, save participants, domain models, and automated tests are sound foundations.
- The gray-box boot, player possession, locomotion, aiming, interaction, weapon, vehicle, pause, diagnostics, and one-cell streaming paths run in the Godot scene.
- Prior review findings were checked as historical evidence, not accepted as current proof.

Final verification after remediation:

- `dotnet test Meridian.sln --no-restore`: **239 passed, 0 failed, 0 skipped**.
- Godot 4.6 Mono headless boot: clean exit through `Boot -> MainMenu -> Playing`, successful player possession, and boot content validation passed.
- `dotnet format ... whitespace --verify-no-changes`: passed.
- `git diff --check`: passed.

## Remediation Completed in This Audit

- Wired a real region index and active streamed region into the game scene.
- Made the streamer follow the currently possessed avatar instead of retaining the on-foot player after boarding.
- Fixed vehicle gravity integration and added vehicle camera look.
- Fixed controller right-stick look being blocked by a second input-context gate.
- Replaced the invisible reticle texture with a centered, resolution-independent drawn reticle.
- Added pause-menu keyboard/controller focus and `ui_cancel` behavior.
- Added basic vehicle speed/fuel/exit HUD state and cleared stale interaction prompts on possession changes.
- Made the interaction keyboard glyph use the current rebind instead of a hard-coded key.
- Guarded input-context pops so one modal cannot accidentally remove another modal's context.
- Fixed inventory oversized-stack insertion, rejected invalid counts, and preserved weapon instance data during splits.
- Made action/quest item grants resolve and register canonical item definitions instead of silently failing.
- Added an inventory/equipped-weapon save participant and restored weapon instance state.
- Connected temporary stat-modifier expiry to world-clock minute ticks.
- Initialized default weather from the registry when the scene omits an explicit profile.
- Added boot-time content validation and focused regression tests for the repaired paths.
- Corrected README and architecture-review claims that previously marked incomplete phases as finished.

## Handoff Blockers

### 1. Runtime composition and authoring surface — Critical

Quest, dialogue, progression, NPC-life, scheduled-event, faction, fast-travel, and related condition/action primitives are not assembled into the shipped game scene. Required master indexes are absent for several content categories. A narrative designer cannot yet add a quest or dialogue asset through a documented “new file + index entry” workflow and see it run in game.

**Exit criteria:** persistent composition services; typed indexes for every promised content category; authored sample quest/dialogue/NPC/event exercising the shared condition/action vocabulary; save registration; validation; a content-author cookbook.

### 2. Save/load completeness and compatibility — Critical

Inventory and equipped weapon state are now covered, but saves still do not fully restore region warm-up, possession, progression, quests/journal in the playable scene, vehicles, discoveries, factions, equipment/quick slots, or player settings. The save version is recorded but no migration/content-drift pipeline or fixture-save compatibility suite exists. Saving remains synchronous and does not provide the specified durable fsync workflow.

**Exit criteria:** versioned participants for all player-owned state; ordered region/streaming/position/possession restoration; migrations and unknown-content policy; archived fixture saves; background serialization/write with durable replacement.

### 3. Professional UX/UI and accessibility — Critical

The current HUD and pause menu are useful debug/prototype surfaces, not a professional UI framework. There is no screen registry/stack, production main menu/loading/settings/inventory/equipment/journal/map/dialogue UI, persistent settings, localization pipeline, safe-area/responsive layout standard, controller-family glyph service, conflict-aware rebinding, or accessibility-settings integration. Most UI is constructed in C#, which limits UI-artist iteration.

**Exit criteria:** scene/theme-driven UI shell and screen navigation; full keyboard/controller focus testing; persistent and consumed accessibility/settings options; localization and scalable/safe-area layouts; controller-aware prompts; production feedback patterns.

### 4. World streaming and region schema — High

The new one-cell region proves that the streamer is active, but `RegionDefinition` lacks much of the specified authoring contract. Streaming scans all cells every frame, budgets by object count rather than elapsed time, and lacks the complete priority, speed-scaled, collision-first, failure-recovery, density, and stress-test behavior required for an open world.

**Exit criteria:** complete typed region metadata; priority queues and millisecond budgets; failed/cancelled load recovery; driving-specific prefetch/collision strategy; global density/physics budgets; automated traversal and unload/reload persistence stress scenes.

### 5. Gameplay vertical slice — High

Combat terminates at a target dummy. The player, vehicles, and NPCs do not form a complete shared damage/death/respawn loop. Weapon upgrades do not apply data-driven effects. Vehicle safe-exit handling and persistence are incomplete. Animation/facing and authored feedback are placeholder-level.

**Exit criteria:** one end-to-end on-foot/vehicle combat encounter; shared damage and lifecycle contracts; functional upgrades; safe vehicle exit; animation and feedback hooks suitable for content replacement.

### 6. Environment and living world — High

Clock, weather, schedules, and event primitives exist, but there is no composed day/night visual controller, deterministic forecast/state machine, authored weather effects, or runtime scheduled-event service. NPC schedules and quests are not connected to that world state in the playable scene.

**Exit criteria:** resource-driven phases/forecast/transitions/effects; clock-subscribed scheduled actions; a sample NPC and quest reacting to time and weather; save/restore coverage.

### 7. Validation, performance, and release gates — High

Boot validation now runs, but the validator covers only the current registry subset and limited cross-references. There are no enforced performance baselines, content-budget validation, localization/action-arity checks, save-fixture gate, or representative stress scenes. “Zero steady-state allocations” and 60 fps are architectural targets, not yet measured guarantees.

**Exit criteria:** complete schema and cross-reference validation; a documented CI validation command; performance captures and thresholds on target hardware; streaming/combat/UI stress fixtures; compatibility tests.

## UX Findings Still Open

- Rebinding is keyboard-centric, lacks conflict resolution and restore-defaults, and prompts do not identify controller family.
- Modal ownership is safer but still uses a stack rather than explicit leases/tokens.
- Vehicle HUD has basic telemetry but no hold-progress, damage, or polished feedback.
- Interaction does not expose locked/unavailable reasons or target highlighting.
- Feedback queues, hit markers, damage direction, autosave state, quest updates, and failure/success messaging are not productionized.
- Accessibility resources exist but are not persisted or consumed across camera, input, audio, subtitles, color, motion, and HUD systems.

## Recommended Delivery Sequence

1. Compose a single representative vertical slice: streamed region, NPC, branching conversation, quest, combat, reward, vehicle transition, time/weather reaction, save/reload.
2. Complete the registries, validator, save participants/migrations, and authoring cookbook around that slice.
3. Build the theme/scene-driven UI shell, settings persistence, accessibility consumers, localization, and input rebinding/glyph services.
4. Replace count-based streaming work with measured time budgets and add traversal, density, failure, and persistence stress tests.
5. Record performance baselines and only then declare the framework ready for broad story, art, music, and content production.

## Creative Work That Can Legitimately Remain Deferred

Final regions, quest prose, dialogue volume, NPC catalog, art, animation, music/SFX, UI art direction, and broad weapon/vehicle catalogs can remain for the human content team. The blockers above are framework and workflow capabilities needed so that team can add those assets safely without rewriting core systems.
