# ADR 0002: Locomotion Hierarchical State Machine (HSM) Implementation

**Date:** 2026-07-05  
**Status:** Accepted  
**Context:** Phase 1 On-Foot Core locomotion and state tracking.  

---

## Context & Problem Statement

Open-world locomotion involves complex, overlapping states (Grounded, Airborne, Sprinting, Crouching, Aiming) that trigger physics adjustments, camera focal transitions, and animation changes. Coupling state evaluation directly to Godot's `_PhysicsProcess` or using standard unstructured boolean checks (e.g. `is_sprinting`, `is_jumping`) leads to bug-prone spaghetti logic, unhandled edge cases (e.g., shooting while falling, sprinting from a crouch), and prevents headless unit testing.

## Considered Options

1. **Godot AnimationTree State Machine:** Driving gameplay state entirely from the animation player tree.
2. **Third-Party C# HSM Library:** Integrating Chickensoft LogicBlocks or other external state engines.
3. **Pure C# State Machine (Recommended):** Writing a custom state manager decoupled from Godot Nodes, using enum mappings and event-driven notifications.

## Decision Outcome

We adopted Option 3 (**Pure C# State Machine**). The `LocomotionStateMachine` class is written in pure C# with no dependencies on Godot nodes. It tracks current states, exposes a `StateChanged` event, and handles aiming overlays. 

- **Decoupled Physics:** The state machine calculates logical states (e.g., Grounded -> Walk -> Run -> Jump) based on input velocity, floor checks, and stamina values passed in as arguments.
- **Node Integration:** The `PlayerAvatar` node instances `LocomotionStateMachine` and ticks it during physics frames. The `MovementMotor` and `CameraRig` query the state machine's output to apply correct physics parameters and camera transitions.

## Consequences

### Positive
- **Headless Unit Testing:** Locomotion states can be tested for correctness under any input or physics condition without spinning up Godot viewport nodes.
- **Zero Allocations:** No GC allocations occur during frame tick evaluations.
- **Clean Execution Flow:** Separates "what the character is doing" (logical state) from "how it moves physically" (MovementMotor integration).

### Negative
- Requires manual entry/exit state wiring if states become excessively complex later on.
