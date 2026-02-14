# 01 - Dynamic Tile Registry (Map Editor Scope)

## Intent

This doc defines the implementation for a dynamic tile registry inside the existing map editor UI.

It is not a full UI redesign. We keep the current sidebar/layout and make existing controls data-driven from the active registry.

## Decisions Already Made

1. Registry selection drives the editor.
2. Sidebar buttons/options update per registry.
3. Building size is not hardcoded; it comes from registry building definitions.
4. Grid and palette colors must be routed through the existing distinct-color service.
5. This is an app-focused tool for PokemonGreen (not a universal editor platform), but implemented cleanly.

---

## Required Outcome

When the active registry changes:

- Category tabs update to categories exposed by that registry.
- Tile options update to tiles in the selected category.
- Building options update to building definitions in that registry.
- Building preview + placement dimensions come from registry footprint data.
- Palette and canvas use registry tile colors through `tileColorService`, with distinct-color mode still supported.

---

## Current Hardcoded Points To Replace

- `tools/map-editor/src/data/tiles.ts`
  - `TILE_CATEGORIES`, `TILES`, `TILES_BY_ID` are currently static.
- `tools/map-editor/src/data/buildings.ts`
  - `BUILDINGS`, `BUILDINGS_BY_ID`, `BUILDING_PREFAB_STAMPS_BY_ID` are currently static.
- `tools/map-editor/src/hooks/useSidebarControls.ts`
  - Reads static `TILES` and `BUILDINGS` for palette/building lists.
- `tools/map-editor/src/store/slices/buildingSlice.ts`
  - Validates and places from static `BUILDINGS_BY_ID` and `TILES_BY_ID`.
- `tools/map-editor/src/components/sidebar/PaletteSection.tsx`
  - Uses static `TILE_CATEGORIES` and a distinct map created from static `TILES`.

---

## Data Model (Editor)

```ts
type RegistryId = string

interface EditorTileDefinition {
  id: number
  name: string
  color: string
  walkable: boolean
  category: string
  encounter?: string
  direction?: 'up' | 'down' | 'left' | 'right'
  isOverlay?: boolean
}

interface EditorCategoryDefinition {
  id: string
  label: string
  showInPalette: boolean
}

interface EditorBuildingDefinition {
  id: string
  name: string
  tiles: (number | null)[][]
}

interface EditorTileRegistry {
  id: RegistryId
  name: string
  version: string
  categories: EditorCategoryDefinition[]
  tiles: EditorTileDefinition[]
  buildings: EditorBuildingDefinition[]
}
```

Notes:
- `width` and `height` for buildings are derived from `tiles` at runtime.
- Category IDs are dynamic strings (not a closed union).

---

## Store + Selector Plan

Add a registry slice to editor state:

- `activeRegistry: EditorTileRegistry`
- `setActiveRegistry(registry)`
- Derived selectors:
  - `categoriesForPalette`
  - `tilesById`
  - `tilesForSelectedCategory`
  - `buildings`
  - `buildingsById`

Behavior rules:
- If selected category is missing in a new registry, fall back to the first visible category.
- If selected tile is missing, fall back to registry default grass-equivalent tile (or first walkable terrain tile).
- If selected building is missing, clear building selection.

---

## UI Wiring (No Redesign)

Use existing components, change only their data source:

- `PaletteSection.tsx`
  - Render category buttons from `activeRegistry.categories`.
  - Render tile buttons from `tilesForSelectedCategory`.
  - Keep existing Distinct toggle.
- Building list inside `PaletteSection.tsx`
  - Render from `activeRegistry.buildings`.
  - Display computed size from footprint matrix.
- `BuildingControlsSection.tsx`
  - Preview dimensions from selected registry building + rotation.

No new layout, no inspector redesign, no new dock model.

---

## Building Size De-Hardcoding

Update building pipeline so footprint is authoritative:

- Compute width/height from each footprint matrix.
- Rotation helper should accept a dynamic building object, not only typed `BuildingId` constants.
- Placement and preview use computed rotated footprint dimensions.

Result: any registry can define 2x2, 3x5, 7x4, etc. without code edits.

---

## Distinct Color Service Integration

Use the existing service at `tools/map-editor/src/services/tileColorService.ts` with runtime registry tiles:

- Build distinct color map from `activeRegistry.tiles`.
- `PaletteSection.tsx` and `CanvasRegion.tsx` both consume the same map.
- Preserve `useDistinctColors` toggle from `uiStore`.
- If tile is unknown, use a single safe fallback color.

This keeps grid readability while removing static color assumptions.

---

## Implementation Checklist

1. Add registry model + default registry loader in map editor.
2. Add registry state slice + selectors.
3. Refactor sidebar hooks/components to consume registry selectors.
4. Refactor building service/slice to use registry building definitions.
5. Rewire color map creation to active registry tiles.
6. Add tests for registry switch behavior (category/tile/building fallback).
7. Add tests for variable-size building placement and rotation.

---

## Acceptance Criteria

- Switching registry updates category buttons, tile list, and building list immediately.
- Buildings from registry place correctly regardless of size.
- No building size constants required in code for new registries.
- Distinct color mode works with registry tile colors in both palette and canvas.
- Existing editor layout remains intact.

## Phase 4 Coverage

Tests now cover:

- Registry JSON loader validation (`parseRegistryJson`) for malformed references and duplicate identifiers.
- Registry switch fallback behavior for selected category, tile, and building.
- Building footprint rotation/placement using variable matrix dimensions.
- Distinct color behavior for dynamic category IDs and fallback tile color resolution.

Reference tests:

- `tools/map-editor/src/services/__tests__/registryService.test.ts`
- `tools/map-editor/src/store/slices/__tests__/registrySlice.test.ts`
- `tools/map-editor/src/services/__tests__/buildingService.test.ts`
- `tools/map-editor/src/services/__tests__/tileColorService.test.ts`

---

## Phase 1 Export Artifact

- Registry JSON is exported by `PokemonGreen.MapGen` via:
  - `dotnet run -- export-registry --output <path>`
- Default artifact path is:
  - `tools/map-editor/src/data/registries/default.json`
- Tile/category data is sourced from `PokemonGreen.Core/Maps/TileRegistry.cs`.
- Building footprints are temporarily mirrored from existing editor building definitions for parity and will move to a shared source in a later phase.
