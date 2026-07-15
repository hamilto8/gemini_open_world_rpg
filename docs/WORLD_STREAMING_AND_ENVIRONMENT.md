# World Streaming and Environment Authoring

This guide describes the handoff-facing contracts for adding regions, streaming cells, day/night presentation, and weather without changing C#.

## Add a region

1. Create one scene per streaming cell under `scenes/world/regions/`.
2. Add a `CellDefinition` entry to a `RegionDefinition` in `data/regions/`.
3. Add the region resource to `data/indexes/region_index.tres`.
4. Run `scripts/validate-handoff.sh` before handing the content branch to another developer.

Each cell needs a unique `GridPosition` and a full `ScenePath`. Tune `StreamPriority` when a landmark or transition cell should win over equally distant cells. `SimulationCost` is a relative AI/physics cost; the region admits nearby simulation until `MaxSimulationCost` is reached and leaves lower-priority cells visual-only. `AlwaysLoaded` is reserved for small global transition/hub cells.

For vehicle routes, author a lightweight `CollisionScenePath` containing only static collision and navigation proxies. The streamer instantiates this proxy before the full scene, expands prefetch at driving speed, and removes it with the cell. Do not duplicate gameplay state in the proxy.

`RegionDefinition` also carries content-facing biome, tag, default-weather, ambient-audio, resident-cell, and simulation budgets. Treat the limits as part of the region contract rather than increasing global values to hide an over-budget cell.

## Streaming behavior

- Candidate lookup is spatially bounded around the possessed player or vehicle; region size does not create a full-region scan every frame.
- Work is deterministically ordered by cancellation urgency, target ring, authored priority, distance, and grid coordinate.
- Main-thread transitions stop at `WorkBudgetMilliseconds`; cell instantiation also respects `MaxCellInstancesPerFrame`.
- Failed loads use bounded exponential retry. Leaving a loading ring cancels the request through the loader abstraction.
- `Visual` cells do not process. `Simulated` and `Active` cells process subject to the region simulation budget.
- Persistent scene-object deltas and dynamic objects are captured before unload and rehydrated on reload.

## Weather authoring

Weather lives in `data/environment/weather_profiles/`. A `WeatherProfile` controls fog, light tint, movement modifier, and weighted outgoing forecast transitions. Every transition specifies a target weather ID, weight, intensity, game-minute duration range, and visual transition time.

The forecast uses a deterministic saved random state. This makes reloads and automated tests repeatable. Ensure every target ID exists in `weather_index.tres`; boot validation rejects missing cross-references.

`EnvironmentPresentationController` is scene presentation only. It consumes the world clock and current weather to drive the sun, ambient light, procedural sky, and fog. Art teams can replace its exported phase colors/energies or replace the controller entirely without altering clock or forecast simulation.

## Performance acceptance

Region budgets are authoring guardrails, not proof of target-hardware performance. Before shipping a region, record a traversal capture on target hardware that includes walking, maximum-speed driving, cell boundary reversals, combat, UI opening, and save/reload. The release target remains 60 fps / 16.6 ms; regressions must be fixed in content cost or streaming policy rather than waived silently.
