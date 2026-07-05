# ADR 0004: Decoupled Cell Loading and State Persistence in World Streaming

**Date:** 2026-07-05  
**Status:** Accepted  
**Context:** Phase 3 World & Streaming design.  

---

## Context & Problem Statement

Open-world streaming requires cell nodes to load and unload in response to player distance. When a cell unloads, any state modifications that occurred (e.g., a chest was looted, a vehicle was parked, items were dropped) are lost unless explicitly captured. Conversely, during loading, these changes must be re-applied to prevent the world resetting to its authored defaults. Additionally, testing streamer scheduling and loading sequences in C# headlessly requires a decoupled way to simulate node lifecycle operations.

## Considered Options

1. **Direct Godot Streaming API:** Relying on `ResourceLoader.LoadThreadedRequest` directly inside streamer scripts, instantiating scenes directly, and writing state serialization within each scene-glue script.
2. **Decoupled Cell Loading via ICellLoader & WorldStateStore (Recommended):** Routing cell instancing through a loader abstraction, and storing cell modifications as serialized deltas in a centralized `WorldStateStore` keyed by cell coordinates or GUIDs.

## Decision Outcome

We adopt Option 2.
- **ICellLoader:** Interface managing cell asset requests. During game runtime, the concrete loader implements Godot threaded resource loading. In unit tests, a mock loader simulates load ticks and returns mock scene structures.
- **WorldStateStore:** A centralized registry tracking state deltas from authored defaults.
  - When a cell prepares to unload, it captures modified elements (e.g. looted state, dropped items) and serializes them to the store.
  - When a cell loads, it queries the store and applies the cached deltas.
  - The store registers as an `ISaveParticipant` under `SaveService`, ensuring all cell modifications are written to slot saves.

## Consequences

### Positive
- **No World Resetting:** Opened chests and dropped items remain modified when the player unloads and returns to a region.
- **Save Game Size Optimization:** Only *deltas* (changes from default) are stored, minimizing save slot storage size.
- **Testable Streaming scheduler:** Headless unit tests can assert cell ring transitions (Active ↔ Simulated ↔ Visual) without loading heavy 3D mesh assets.
