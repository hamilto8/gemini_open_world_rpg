# ADR 0006: Time Scheduling & Weather Modifier Integrations

**Date:** 2026-07-05  
**Status:** Accepted  
**Context:** Phase 5 Time & Weather v1 scheduling routines and dynamic modifiers.  

---

## Context & Problem Statement

Project Meridian features a dynamic environment (time clock, weather cycles) that affects both visual settings (fog density, sunlight hue) and player statistics (move speed slows in rain, stamina exhausts faster in heat). To ensure these components execute safely without creating tight bindings across modules:
1. We need a scheduling system that evaluates events without performance hits or frame skipping.
2. We need a decoupled approach to apply and remove stats modifiers on characters during environmental changes.

## Considered Options

1. **Direct Modifiers Poll:** Let the locomotion system poll the weather autoload inside its update loop, adjusting velocity thresholds depending on current strings.
2. **Event-Driven Modifier Push (Recommended):** The weather autoload registers and deletes modifiers directly to the player's `StatBlock` upon entering and leaving weather profiles.

## Decision Outcome

We adopted Option 2 (**Event-Driven Modifier Push**).
- **ScheduledEventRunner:** Tracks recurring and one-shot callbacks in a clean list, evaluating on game minute ticks. Exposes a fail-safe try/catch block per callback.
- **WeatherSystemNode:** AUTOLOAD class managing active profiles (`WeatherProfile`). On transition, it:
  1. Instantiates a `Modifier` object with a unique source tag (e.g. `weather_rain`).
  2. Resolves the player avatar's `StatBlockNode` and registers the modifier.
  3. Removes the modifier when weather clears or transitions to a different profile.

## Consequences

### Positive
- **Decoupled Locomotion:** The locomotion motor does not know about the weather system; it simply queries `StatBlock.GetStat("move_speed")`, which caches the combined modifiers automatically.
- **Performance:** Scheduled events are only evaluated during minute ticks (every few real-world seconds), keeping physics frame ticks fast.
- **Unit Test Coverage:** High-priority weather stat modification math is 100% testable headlessly.

### Negative
- Requires robust check routines to ensure modifiers are removed if the player avatar despawns or changes during possession swaps.
