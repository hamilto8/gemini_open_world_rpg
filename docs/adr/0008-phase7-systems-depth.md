# ADR 0008: Systems Depth (Progression, Fast Travel, Dynamic Music)

**Date:** 2026-07-06  
**Status:** Accepted  
**Context:** Phase 7 Systems Depth progression levels, fast travel portals, and audio crossfades.  

---

## Context & Problem Statement

Open-world depth relies on systemic integrations:
1. Progression systems mapping XP requirements and level thresholds.
2. Fast travel networks gating teleportation coordinates depending on discovery events.
3. Audio managers dynamically adjusting track layer decibels based on player tension.

These subsystems are traditionally tightly bound to Godot Nodes or scene trees, preventing headless simulation and causing test host crashes.

## Considered Options

1. **Integrated Node Subsystems:** Driving progression, travel vectors, and audio buses directly from active Godot Nodes.
2. **Decoupled Domain Services (Recommended):** Writing C# domain adapters (`ProgressionManager`, `FastTravelNetwork`, `MusicManager`), checking interfaces (`IProgressionProfile`), and testing computations in under 40 milliseconds without Godot attachments.

## Decision Outcome

We adopted Option 2 (**Decoupled Domain Services**).
- **ProgressionManager:** Ticks levellings, allocates skill points, and pushes dynamic perk modifier objects to the player's `StatBlock`.
- **FastTravelNetwork:** Centralized directory managing node coordinates and discovery tags, validating travel coordinates, and setting teleport positions.
- **MusicManager:** Calculates Exploration and Combat bus volume decibels depending on a float tension level.

## Consequences

### Positive
- **Fully Testable:** 39 headless unit tests validate perk modifier additions, travel gates, and dynamic audio crossfades.
- **Zero GC overhead:** Volume lerps and XP scaling equations use math primitives.
- **Maintainable Audio Layout:** The music manager calculates DB volume values, which are subsequently routed to actual Godot buses during runtime.

### Negative
- Minor adaptation layers required in runtime Autoload wrappers.
