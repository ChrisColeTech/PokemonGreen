# Random Map Generation Architecture (Phase 1)

## Purpose

For day-to-day usage (Generate/Regenerate, export, and game map pipeline), see `docs/workflow-to-game-pipeline.md`.

Define a deterministic, staged generation system that produces editor-ready tile grids for `map-editor` while fitting the current workflow:

- `TileGrid` (`number[][]`) remains the source of truth.
- Existing tile and building definitions (`src/data/tiles.ts`, `src/data/buildings.ts`) are reused.
- Results can be loaded through existing editor workflows (`loadMapData` in `src/store/editorStore.ts`) without changing current painting/import/export behavior.

## Generation Pipeline Passes

Generation runs as a pipeline of explicit passes. Each pass reads and mutates a shared generation context.

1. `initialize`
   - Create base grid and seed metadata.
   - Fill map with a base terrain tile (default grass).
2. `carvePrimaryPaths`
   - Create guaranteed traversable route skeleton between required exits and hubs.
3. `paintBiomes`
   - Apply terrain zones (water bands, forests, clearings, route shoulders).
4. `reserveDistricts`
   - Reserve footprint-safe regions for town core, route POIs, and landmarks.
5. `placeBuildings`
   - Place building stamps (pokecenter, mart, houses, gym, gate, etc.) using existing building IDs.
6. `placeEncounters`
   - Paint encounter tiles (tall grass, cave, surf water, rare grass) according to archetype profile.
7. `placeInteractivesAndEntities`
   - Add signs, items, NPC/trainer anchors, and optional event markers.
8. `balance`
   - Apply deterministic post-pass balancing (trainer/encounter density clamping, clutter reduction near town paths).
9. `validate`
   - Run hard constraints and score soft goals.
10. `repair`
   - Attempt bounded repairs for failing hard constraints.
11. `finalize`
   - Emit final grid, placements, diagnostics, and reproducibility metadata.

Notes:

- Passes are intentionally explicit so phase 2 can implement each independently.
- Archetypes can override pass order only when dependency-safe.

## Hard Constraints vs Soft Goals

### Hard constraints (must pass)

- `boundedMap`: all writes remain in `[0,width) x [0,height)`.
- `knownTileIdsOnly`: every tile ID exists in `TILES_BY_ID`.
- `reachableCriticalPath`: at least one walkable path connects required route anchors/town core entrances.
- `buildingFootprintsInBounds`: building placements are fully in-bounds.
- `buildingDoorConnectivity`: placed buildings with door tiles connect to a walkable path.
- `minRequiredStructures`: archetype minimum building counts are satisfied.
- `spawnSafety`: reserved spawn/entry cells are walkable and not blocked by impassable tiles.

### Soft goals (optimize, not required)

- `routeReadability`: routes are visually readable and avoid noisy zig-zag patterns.
- `biomeVariety`: meaningful distribution of terrain/encounter regions.
- `townCoherence`: town buildings cluster naturally around shared paths.
- `encounterPacing`: encounter density follows intended progression for map scale.
- `landmarkVisibility`: key landmarks are not hidden in clutter.

Soft goals produce numeric scores and weighted totals for diagnostics.

## Archetypes

Initial archetype family for phase 1 presets:

- `town_route_basic`
  - One town core linked to one route.
  - Low water usage, moderate encounter grass.
- `coastal_town_route`
  - Town + route with a coastline/water band constraint.
  - Requires bridge or safe passable shoreline transitions.
- `forest_town_route`
  - Route segments through forest pockets and clearings.
  - Higher encounter emphasis, lower water usage.

Each archetype defines:

- recommended dimensions
- pass order (default unless overridden)
- required hard constraints
- weighted soft goals
- building target ranges by `BuildingId`
- tile-role preferences (path tile, primary terrain tiles, optional encounter tiles)

## Validation and Repair Strategy

Validation is staged:

1. **Per-pass lightweight checks**
   - Detect obvious contract violations early (out-of-bounds writes, invalid tile IDs).
2. **Full post-generation validation**
   - Evaluate all hard constraints.
   - Compute soft-goal scores and weighted summary.

Repair strategy (bounded and deterministic):

- Run only if hard constraints fail.
- Apply ordered repair actions (for example: reconnect path, re-place blocked building, fill invalid tile fallback).
- Re-validate after each repair cycle.
- Stop after `maxRepairAttempts` or when all hard constraints pass.
- If still failing, return diagnostics with failure details (do not silently hide errors).

## Integration Points with Existing Store/Services

- **Grid model compatibility**
  - Generator output remains `TileGrid` for direct use by editor store.
- **Store integration**
  - Generated output can be applied via existing `loadMapData({ width, height, tiles, displayName })`.
  - Future UX can add a dedicated action (for example `generateRandomMap`) that wraps service call + `loadMapData`.
- **Existing services reused**
  - `gridService` for grid creation/resizing helpers.
  - `buildingService` for footprint rotation/placement compatibility checks.
  - `mapIoService` remains unchanged because output schema is unchanged (`width`, `height`, `tiles`).

## Phase Boundaries

Phase 1 delivers:

- architecture/types/presets/orchestrator skeleton
- compile-ready service APIs
- TODO pass placeholders without generation implementation

Phase 2+ will implement pass logic, deterministic RNG behavior, scoring algorithms, and repair operators.
