# ADR 0007: Quest, Dialogue & NPC Life Implementations

**Date:** 2026-07-06  
**Status:** Accepted  
**Context:** Phase 6 Quests, Dialogue & NPC Life schedules.  

---

## Context & Problem Statement

Open-world games require quest trackers, dialogue branch controllers, and daily schedule updates for NPCs. Driving these features with heavy engine couplings prevents testing them headlessly and makes changes to story flows highly prone to regression bugs. 

## Considered Options

1. **Unity-style Node Graphs:** Using Visual Scripting nodes in-editor to track quest states and dialogue choices.
2. **Adapter-based Domain Models (Recommended):** Writing plain C# domain classes (`QuestManager`, `DialogueService`, `NpcScheduler`), adapting Godot Resource classes through an adapter layer (`QuestDefinitionAdapter`), and triggering behaviors via the global `EventBus`.

## Decision Outcome

We adopted Option 2 (**Adapter-based Domain Models**).
- **QuestManager:** Evaluates objective completions in response to EventBus events (e.g., items gathered, targets defeated).
- **DialogueService:** Processes dialogue steps and executes choice side-effects (e.g. starting a quest).
- **NpcScheduler:** Evaluates activity states (Sleep/Work/Tavern) based on the world hour.
- **QuestDefinitionAdapter:** A clean wrapper adapting Godot's `QuestDefinition` Resource to the domain interface. This resolves a known issue where Godot C# source generators fail on explicit interface properties.

## Consequences

### Positive
- **Complete Decoupling:** Narrative sequences and schedule evaluations run entirely in memory.
- **Robust testing:** 36 headless unit tests validate quest progress, conversation branches, and NPC locations in under 40 milliseconds.
- **Source Generator Safety:** Using adapters completely bypasses Godot code generation limitations.

### Negative
- Minor adaptation layer boilerplate.
