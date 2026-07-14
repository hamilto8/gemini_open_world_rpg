# Project Meridian

Project Meridian is a **theme-agnostic, framework-first architecture** for a 3D third-person open-world action RPG built in **Godot 4.6 (.NET)** using **C# (.NET 8)**.

The framework is designed around strict data-driven patterns to allow solo developers and small teams to expand content (weapons, items, vehicles, regions, quests) with **zero modifications to the core C# codebase** ("new files + one index entry, zero edits to core C#").

> **Readiness status (July 14, 2026):** this repository is a strong framework prototype, not yet a story/content-team handoff build. See the [current readiness audit](docs/HANDOFF_READINESS_AUDIT_2026-07-14.md) for verified capabilities, remediation completed in this pass, and remaining blockers.

---

## 🚀 Architectural Pillars

1. **Data over Code:** Every game noun—weapons, items, quests, weather presets—is represented by a Godot `Resource` (`.tres`). Code dictates behavior; data dictates existence.
2. **Composition over Inheritance:** Game entities are shallow node trees composed of focused components. Domain models are plain C# classes completely decoupled from Godot's `Node` inheritance for full headless testability.
3. **Decoupled Messaging ("Call Down, Signal Up, Event Across"):** Parent nodes call child methods directly; child nodes notify parent nodes via Godot signals; unrelated systems communicate only via a strongly-typed C# `EventBus`.
4. **Definition vs. Instance Separation:** Immutable, read-only definitions (`WeaponResource`) are separated from mutable, serializable runtime instances (`WeaponInstance`), eliminating shared-state mutation bugs and simplifying save systems.
5. **Frame Budget as a Contract:** Engineered around a strict **16.6 ms (60 fps)** target frame budget, employing time-sliced async streaming, ring-based LODs, and zero steady-state per-frame garbage collection allocations.

---

## 🛠️ Implemented Framework Prototype

Phase 0 establishes the "Walking Skeleton" and core services:

- **Services Locator:** A static `Services` register facilitating clean dependency inversion and interface exposure, allowing fakes and mocks to be swapped in for headless unit tests.
- **EventBus:** Allocation-efficient, typed pub/sub event dispatcher requiring explicit subscription tokens to prevent memory leaks.
- **InputContextService:** Stack-based input mapping that automatically isolates modal input states (e.g. blocking movement and shooting actions while the UI, dialogue, or console is open).
- **SaveService:** Atomic JSON serialization engine employing a write-to-temp-then-rename swap pattern, automatic `.bak` rotation, and ordered participant restoration.
- **WorldClock & WeatherSystem:** Global clock and weather engines simulating continuous day/night phases and weather transitions, integrated with save states.
- **PerfHUD:** Always-on debug overlay tracking frame time against the 16.6ms budget, memory utilization, draw calls, and object count.
- **DebugConsole:** Developer console supporting runtime cheat/test commands (`set-time`, `set-weather`, `save`, `load`) accessible via the Grave/Tilde key (`~`).
- **ContentValidator:** Validation tool checking indices and directories at boot for missing files, duplicate IDs, or broken references.

The repository also contains tested domain primitives for inventory, weapons, quests, dialogue, progression, NPC schedules, streaming, vehicles, environment simulation, and fast travel. Several of those primitives are not yet composed into the shipped game scene, backed by complete content registries, or represented by production UI. The playable build is therefore a systems prototype rather than a complete vertical slice.

---

## 📂 Project Structure

The project layout enforces strict separation of content assets, database definitions, and systems code:

```
res://
├── addons/         # Pinned third-party GDExtension and GDScript add-ons
├── assets/         # Raw importable assets organized by domain (characters, environment, props)
├── data/           # Database layer containing all immutable .tres resource definitions
│   └── indexes/    # Master index resources for content discovery and validation
├── scenes/         # Composed scene trees (Tscn) and scene-glue scripts
│   ├── game/       # Main Game root scene, Systems node, and UI layer
│   └── player/     # Player rig and camera configuration
├── src/            # Systems C# code organized by namespace
│   ├── Meridian/
│   │   ├── Core/   # EventBus, Services, GameDirector, and Save systems
│   │   ├── Input/  # InputContext stack routing
│   │   └── UI/     # Telemetry overlays and debug console
├── shaders/        # Custom rendering shaders
├── localization/   # Translation files
└── tests/          # Headless xUnit unit testing project
```

---

## ⚙️ Getting Started

### Prerequisites
* [Godot Engine 4.6+ (Mono/C# version)](https://godotengine.org/)
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download)

### 1. Clone the Repository
```bash
git clone https://github.com/hamilto8/gemini_open_world_rpg.git
cd gemini_open_world_rpg
```

### 2. Restore NuGet Packages & Build
```bash
dotnet restore
dotnet build
```

### 3. Run Automated Unit Tests
A headless xUnit test project checks domain systems (event bus, services, input contexts, save serializations, validation rules) in milliseconds:
```bash
dotnet test
```

### 4. Running the Game
Open Godot and import the `project.godot` file. Alternatively, run the project from your terminal:
```bash
godot --path .
```

---

## 🗺️ Roadmap & Phasing

- [x] **Phase 0: Foundations** — Walking skeleton, core services, input contexts, save skeleton, diagnostics, and validator foundations.
- [ ] **Phase 1: On-Foot Core** — Functional gray-box locomotion, camera, stats, and aiming; final animation, combat lifecycle, and feedback remain.
- [ ] **Phase 2: Items & Weapons** — Domain models and a hitscan prototype exist; production inventory/equipment UI, data-driven upgrades, and crafting remain.
- [ ] **Phase 3: World & Streaming** — A wired one-cell streaming smoke path now exists; authoring schema, priority/time budgets, terrain, and stress validation remain.
- [ ] **Phase 4: Vehicles v1** — A board/drive/exit prototype exists; persistence, damage, safe exit, camera polish, and production HUD remain.
- [ ] **Phase 5: Time & Weather v1** — Clock/weather primitives exist; visual day/night, forecast scheduling, and authored effects remain.
- [ ] **Phase 6: Quests, Dialogue & NPC Life** — Tested domain primitives exist but are not runtime-composed into the shipped scene.
- [ ] **Phase 7: Systems Depth** — Partial progression, fast-travel, and music primitives exist; the phase exit criteria are not met.
- [ ] **Phase 8: Content & Polish** — Content, accessibility integration, UI art/theme, localization, profiling, and polish remain.
