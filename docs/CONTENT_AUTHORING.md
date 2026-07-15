# RPG Content Authoring Cookbook

Project Meridian content follows one rule: add a resource file and one index entry; do not edit core C# for ordinary content.

## Required workflow

1. Duplicate the nearest sample resource under `data/`.
2. Assign a permanent, category-unique `snake_case` ID. Save files and cross-references depend on this ID; never rename a shipped ID without a migration.
3. Replace references and authored values.
4. Add the resource to its typed master index under `data/indexes/`.
5. Run `scripts/validate-handoff.sh`. A content branch is not ready if boot validation reports an orphan, missing index entry, duplicate ID, invalid schema, or broken cross-reference.

## Representative vertical slice

The Harbor sample is the canonical working example:

- `data/dialogue/dockmaster_intro.tres` contains a conditional branching conversation.
- `data/quests/harbor_relay.tres` contains an objective, reward, acceptance actions, and completion actions.
- `data/npcs/dockmaster_vale.tres` contains dialogue/faction references and a wraparound daily schedule.
- `data/events/harbor_morning_bell.tres` contains clock-driven conditions and actions.
- `data/factions/harbor_union.tres`, `data/fast_travel/harbor_pier.tres`, and `data/progression/profiles/default_progression.tres` exercise the remaining persistent systems.

In the gray-box game, approach Dockmaster Vale and use the interaction prompt. The scene starts `dockmaster_intro`; accepting its authored choice starts `harbor_relay` through the shared action vocabulary.

## Conditions and actions

Quest, dialogue, interactable, and scheduled-event content share the same concrete resource vocabulary. Use the concrete scripts in `src/Meridian/Data/Conditions/` and `src/Meridian/Data/Actions/`; never attach the abstract base resource scripts to a `.tres` subresource.

Available sample-facing conditions include player region, quest state, and faction reputation. Available actions include start quest, world flag, notification, XP, and faction reputation. Conditions are evaluated before effects. Action order is authored order and can be observable, so keep prerequisite changes before dependent effects.

## Dialogue

Every node needs a unique `NodeId`. `StartNodeId` must name an existing node. Every choice needs visible text and targets another node or the reserved target `end`. The dialogue UI builds choice controls from the domain model; final typography and art can replace the authored scene without changing dialogue data.

## Quests

Every objective needs a unique ID, target, type, and positive required count. Reward item IDs must exist in `item_index.tres`. Use stable objective IDs because quest saves store progress by objective ID. Acceptance/completion effects belong in their action arrays, not NPC scripts.

## NPC schedules and events

Schedule hours are inclusive authored ranges and may wrap midnight. NPC dialogue and faction IDs must resolve through their indexes. Scheduled events use a valid hour/minute and at least one action; recurring events run daily, while one-shot events are removed after firing.

## Content drift and saves

Unknown participant data is preserved by the save container. Unknown items/equipment/vehicles retain stable IDs but do not invent effects. If a schema changes, increment the participant version, add a contiguous migration, and archive a fixture save. See `docs/SAVE_SYSTEM.md`.

## UI, world, and environment

- UI screens, localization, focus, glyphs, and accessibility: `docs/UI_CONTENT_AUTHORING.md`.
- Regions, collision-first cells, budgets, weather, and day/night: `docs/WORLD_STREAMING_AND_ENVIRONMENT.md`.

## Audio and music

Create sound cues as `SoundCueResource` files and add them to `data/indexes/sound_cue_index.tres`. Actions refer to the stable cue ID; the pooled audio director resolves the stream, bus, gain, and pitch range. A direct `res://` audio path remains supported for footstep-profile compatibility. Author floor material metadata as `footstep_material`; animation events call `FootstepMaterialDetectorNode.PlayFootstep()`.

Assign exploration and combat streams on the scene's `MusicManager` node. The two synchronized players crossfade from the engine-free tension model. If a named SFX/Music bus is absent, playback safely falls back to Master; production projects should author the standard bus layout before final mixing.
