# ADR 0003: Interface-Based Decoupling for Godot Resource Unit Testing

**Date:** 2026-07-05  
**Status:** Accepted  
**Context:** Phase 2 Items & Weapons database and inventory testing.  

---

## Context & Problem Statement

Classes deriving from Godot's native classes (like `Godot.Resource` or `Godot.Node`) require native Godot engine memory allocation. When instantiated via standard `new()` inside pure C# headless unit tests (e.g. xUnit runner), the test host process crashes instantly with native execution faults. Because game items and weapons are defined via Godot inspector resources (`ItemResource`, `WeaponResource`), any inventory domain logic consuming them directly becomes untestable headlessly.

## Considered Options

1. **In-Engine Testing Only:** Writing and running all unit tests inside the Godot game environment using add-ons like GUT or GoDotTest.
2. **Interface-Based Decoupling (Recommended):** Extracting pure C# interfaces for all domain resource needs, making the domain layer depend solely on interfaces, and implementing them on the Godot resources.

## Decision Outcome

We adopted Option 2 (**Interface-Based Decoupling**). We created three domain interfaces:
1. `IItemDefinition` representing base items.
2. `IEquippableBehavior` representing stats equipment modifiers.
3. `IWeaponDefinition` representing shooting rates and magazine sizes.

Concrete Godot resource classes (`ItemResource`, `WeaponResource`, `EquippableBehavior`) implement these interfaces. 

Inside the domain layer (`InventoryModel`, `EquipmentModel`, `WeaponController`), dependencies are declared strictly on the interfaces.

Inside unit tests, we instantiate simple pure C# mocks (`BasicItemDefinition`, `BasicWeaponDefinition`, `MockEquippableBehavior`), completely bypassing Godot resource allocations and avoiding native crashes.

## Consequences

### Positive
- **Headless test speed:** 27 unit tests execute and pass in under 40 milliseconds.
- **Trimming and Tracing compatibility:** Domain models are 100% C# standard code, compliant with compilation tracing.
- **Architectural Seam:** Clean decoupling forces developers to outline exact data contracts required by systems.

### Negative
- Minor boilerplate addition (declaring interfaces and implementing them explicitly on Godot resources).
