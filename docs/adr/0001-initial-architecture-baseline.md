# ADR 0001: Initial Architecture Baseline & Phase 0 Foundation

**Date:** 2026-07-05  
**Status:** Accepted  
**Context:** Project Meridian initialization and implementation of Phase 0 (Foundations).  

---

## Context & Problem Statement

Project Meridian is an open-world 3D third-person action RPG framework built in Godot 4.6 with C# (.NET 8). To ensure solo and small-team sustainability over multiple years of content expansion, we must establish rigorous architectural baselines at project inception. Without strict separation of concerns, open-world codebases quickly degenerate into tightly coupled systems where adding content (e.g., weapons, vehicles, quests) requires modifying core engine scripts and breaking existing features.

## Considered Options

1. **Monolithic Node-Based Scripting:** Putting simulation and game rules directly into Godot `Node` subclasses (`CharacterBody3D`, `Area3D`, etc.) and using Godot signals across all boundaries.
2. **Layered Data-Domain-Scene Architecture (Recommended by GDD):** Decoupling data definitions (`Resource`), simulation rules (`plain C# domain classes`), and visual/physical presentation (`Node` hierarchies), connected by an asynchronous typed C# `EventBus` and static `Services` locator.

## Decision Outcome

We adopt Option 2 (**Layered Data-Domain-Scene Architecture**) with the following specific technology and pattern selections for Phase 0:

- **Language & Runtime:** C# targeting `.NET 8.0` with nullable reference types enabled and warnings treated as errors. All Godot scripts and resources must be declared as `partial class` to integrate cleanly with Godot 4 source generators.
- **Service Access Pattern:** A static `Services` locator class providing read-only access to interfaces (`IEventBus`, `ISaveService`, `IGameDirector`, `IInputContextService`, `IWorldClock`). Autoload nodes register their implementations at application boot. This ensures clean headless testability without Godot engine dependencies.
- **EventBus Discipline:** To prevent memory leaks and dangling references when scene nodes are freed, all subscriptions to the typed C# `EventBus` return an `IDisposable` token. Nodes must track their tokens and call `Dispose()` inside their `_ExitTree()` method.
- **Serialization & Save Infrastructure:** We use `System.Text.Json` with compile-time source generation (`JsonSerializerContext`) for high performance and trimming safety. All Godot math types (`Vector3`, `Vector2`, `Basis`, etc.) are handled via custom JSON converters. Save file operations use an atomic write-and-rename pattern (`temp` -> `fsync` -> overwrite -> `.bak`) to guarantee corruption-proof persistence.
- **Content Discovery & Validation:** Master index resources (`data/indexes/`) maintain explicit registries of game content. A headless `ContentValidator` service checks all indexes at boot and during CI tests to assert zero missing files, zero duplicate IDs, and valid cross-references.

## Consequences

### Positive
- **Zero-Code Content Addition:** Future weapons, items, vehicles, and regions can be added entirely by creating new resource/scene files and adding one line to an index resource.
- **Testability:** Plain C# domain models and serialization contracts can be tested headlessly using standard .NET testing frameworks without launching the Godot rendering engine.
- **Robustness:** Atomic save writing and boot-time content validation prevent runtime crashes caused by corrupted saves or typo-ridden content IDs.

### Negative / Trade-offs
- **Up-Front Discipline:** Developers and AI coding agents must strictly adhere to layering rules (e.g., never put inventory transaction math in a UI script or scene node; always use DTOs for saves).
- **Boilerplate for New Categories:** Adding a brand-new content *category* or *system* requires creating its interface, DTO contract, and index registry before content can be authored.
