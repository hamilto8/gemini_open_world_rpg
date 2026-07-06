# ADR 0009: Content & Polish (Audio Cues and Accessibility Settings)

**Date:** 2026-07-06  
**Status:** Accepted  
**Context:** Phase 8 Content & Polish audio footsteps and key rebinding settings.  

---

## Context & Problem Statement

Dynamic games require audio selections based on the terrain floor material (e.g. playing grass steps, wood creaks, or hollow metal sound cues). Additionally, players require settings configurations to toggles subtitles and customize input action rebindings. Driving these checks directly within heavy spatial loops or coupling options to UI scenes blocks headless test verification.

## Considered Options

1. **Tight Scene bindings:** Hardcoding material raycasts and setting key bindings directly inside the input event routers or node collision callbacks.
2. **Decoupled Settings and Detectors (Recommended):** Writing plain C# domain models (`AccessibilitySettings`, `FootstepMaterialDetector`) and checking string/tag maps in memory.

## Decision Outcome

We adopted Option 2 (**Decoupled Settings and Detectors**).
- **FootstepMaterialDetector:** Keeps target audio clip paths mapped to terrain material keys. The Node component (`FootstepMaterialDetectorNode`) queries terrain tags (using groups or metadata) on collision bodies and pulls the correct sound cue.
- **AccessibilitySettings:** Simple data configuration map holding rebind string values and subtitle visibility state variables.

## Consequences

### Positive
- **Fully Testable:** 41 headless unit tests validate settings rebindings and material step lookups.
- **Zero GC Allocations:** Lookup parameters use strings and enum structures, keeping physics update cycles fast.
- **Flexible UI bindings:** The setting configuration class can be easily bound to any custom Godot UI list menu layout.

### Negative
- Requires manual mapping setup of physical materials inside the editor inspector.
