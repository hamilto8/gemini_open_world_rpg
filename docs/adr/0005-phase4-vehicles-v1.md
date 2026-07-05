# ADR 0005: Vehicles v1 Possession and Simulation

**Date:** 2026-07-05  
**Status:** Accepted  
**Context:** Phase 4 Vehicles v1 boarding and driving mechanics.  

---

## Context & Problem Statement

Open-world gameplay includes vehicles that the player can walk up to, interact with, board, and drive around. This requires a transition handshake:
1. Swapping possession in the player controller (from the player avatar to the vehicle).
2. Changing the input routing context to filter keyboard/controller maps.
3. Managing vehicle physics (steering, throttle, brakes) and engine resources (fuel).
4. Unboarding safely back onto foot.

## Considered Options

1. **Integrated Spawn/Despawn System:** Delete the player avatar upon entering a vehicle, and recreate it next to the vehicle on exit.
2. **Avatar Hibernation & Possession (Recommended):** Deactivate process modes and hide the player avatar node while boarding, and possess the vehicle node directly. Place the avatar at an exit offset and reactivate it upon exit.

## Decision Outcome

We adopted Option 2 (**Avatar Hibernation & Possession**). The vehicle avatar acts as both an `IInteractable` (for boarding triggers) and an `IPossessable` (for active input handling).

- **Boarding Handshake:** When the player interacts, the `VehicleAvatar` hides the player avatar, sets its process mode to `Disabled`, and calls `PlayerControllerNode.Possess(this)`. The input service pushes the `Vehicle` context (blocking foot actions).
- **Physics Steering & Deceleration:** Ticked during physics steps. Deceleration applies automatically when throttle drops or spacebar (braking) is held.
- **Fuel Consumption:** Throttle burns fuel at the profile rate. If fuel reaches 0, engine throttle fails to accelerate.
- **Unboarding:** When the player triggers interact while possessed, the vehicle restores the avatar's collision and visibility, offsets its position to the driver-side door, and re-possesses it.

## Consequences

### Positive
- **Visual Continuity:** The player's exact character state, equipped gear, and current health/stats are preserved in memory because the node is never deleted.
- **State Segregation:** Clear boundaries between locomotion states (handled by locomotion HSM on foot) and driving states.
- **Clean Decoupling:** Bypasses complex spawn-safe checking.

### Negative
- Requires careful exit offset checks to prevent players spawning stuck in solid terrain walls.
