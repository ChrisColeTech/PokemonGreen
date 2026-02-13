# 00 - Frontend Implementation Plan

## Goal

Build a stable, maintainable frontend for the map editor in `tools/map-editor` using React + Vite + TypeScript + Tailwind.

We will execute in two major stages:

1. Build the **core web UI** (layout, UX foundations, responsiveness).
2. Port the corrupted legacy JavaScript logic into React architecture using **services, hooks, and stores**.

---

## Principles

- Keep UI and domain logic separated from day one.
- Prefer typed models and explicit state transitions.
- Port incrementally in vertical slices (feature-by-feature), not as one massive rewrite.
- Preserve existing behavior where valid, improve UX where unclear or broken.
- Keep code testable and debuggable (pure functions in services, minimal component logic).

---

## Target Architecture

### App Layers

- **UI Layer (components):** presentational React components for layout, controls, panels, canvas wrapper, dialogs.
- **State Layer (store):** global app/editor state (selected tool, active layer, tile palette, map metadata, history, project state).
- **Hook Layer (hooks):** reusable interaction logic (keyboard shortcuts, pointer handling, zoom/pan, autosave triggers).
- **Service Layer (services):** pure domain and IO logic (map transforms, validation, serialization, import/export).
- **Type Layer (types):** shared interfaces for map schema, tools, tiles, entities, and editor actions.

### Suggested Folder Structure

```text
tools/map-editor/src/
  app/
    AppShell.tsx
    routes.tsx
  components/
    layout/
    panels/
    canvas/
    common/
  features/
    editor/
    palette/
    layers/
    history/
    export/
  hooks/
  services/
  store/
  types/
  utils/
  styles/
```

---

## Legacy Feature Baseline (from `tools/MapEditor/index.html`)

This is the source-of-truth feature inventory for the React port.

### Current Feature Set to Preserve

- **Map controls:** width, height, cell size, resize grid, clear all, reset saved data.
- **Painting workflow:** click to paint, drag painting, right-click erase to grass, click same tile toggles back to grass.
- **Palette categories:** Terrain, Encounters, Interactive, Entities, Trainers, Buildings.
- **Tile library:** 51 tile types (`id` 0-50) with metadata fields such as `walkable`, `category`, `encounter`, `direction`, `requires`, and role flags (`villain`, `minion`, `rival`, `elite`, `champion`, `final`, `hidden`, `alt`).
- **Building presets:** 10 preset structures (Pokecenter, Pokemart, Gym, House Small/Large, Lab, Cave Entrance, Gate, Pond, Fence H/V) with placement preview and 90-degree rotation.
- **Trainer vision overlay:** directional vision cones with range 4; blocked by non-walkable/non-trainer tiles.
- **Entity analysis panel:** grouped listing of special trainers, villains, minions, trainers, entities, and encounter counts by encounter type.
- **Persistence:** autosave to local storage key `pokemonGreenMap` and restore on load.
- **Import/export:** JSON import/export and C# array export with tile/type comments plus generated trainer/encounter snippets.

### Legacy File Issues Identified

- Duplicate/conflicting function declarations (for example `selectTile` and `showBuildingPreview`).
- Corrupted block structure with duplicated segments and stray braces.
- Tight DOM coupling and mixed responsibilities (UI rendering + business logic + storage + export in one script).

These issues confirm the migration must be modular and parity-driven, not copy-pasted.

---

## Implementation Phases

## Phase 0 - Foundation Setup

### Scope

- Finalize Tailwind setup in Vite.
- Establish global styling tokens (color, spacing, radius, shadows, z-index, transitions).
- Set up app shell and baseline TypeScript strictness.

### Deliverables

- Working Tailwind pipeline.
- `AppShell` skeleton and layout primitives.
- Initial type definitions for core map/editor models.

### Acceptance Criteria

- `npm run dev` starts cleanly.
- No TypeScript errors in baseline scaffolding.
- Layout primitives are responsive and reusable.

---

## Phase 1 - Core Web UI (UX-first)

### Scope

Build a user-friendly editor shell before porting legacy behavior.

### Core Screens / Regions

- **Top bar:** project name, file actions, undo/redo, zoom indicators.
- **Left sidebar:** tools and brush controls.
- **Main canvas region:** map viewport container and interaction overlays.
- **Right sidebar:** palette/layers/properties tabs.
- **Bottom/status bar:** cursor tile coordinate, active layer, hints.

### UX Requirements

- Fully responsive (desktop first, usable tablet, constrained mobile fallback).
- Keyboard-first efficiency (shortcut hints visible in tooltips/menus).
- Clear visual hierarchy and discoverable controls.
- Feedback states: hover, selected, disabled, loading, error.

### Deliverables

- Implemented layout components with placeholder interactions.
- Shared component library for buttons, tabs, panels, modals, menus.
- Theme and spacing system applied consistently.

### Acceptance Criteria

- Editor shell is usable without domain logic.
- Navigation and controls are clear for first-time user.
- No layout breakpoints cause overlap/hidden critical actions.

---

## Phase 2 - Domain Model + Store

### Scope

Introduce typed editor state and predictable updates.

### State Areas

- Map metadata (name, dimensions, tile size).
- Grid data (`tiles: number[][]`) and tile catalog metadata.
- Layers and visibility/lock state (future-proofing even if legacy is single-layer now).
- Tool state (active tool, selected tile, selected building, building rotation, eraser/toggle behavior).
- View state (zoom, pan, grid visibility).
- Pointer/editor session state (isDrawing, hover cell, drag state).
- Derived data state (entity summary, trainer vision overlays).
- Persistence/export state (dirty flag, autosave status, last import/export result).
- Edit history (undo/redo stacks).

### Deliverables

- Central store with typed actions/selectors.
- Immutable update strategy for map edits.
- History system integrated with store actions.

### Acceptance Criteria

- State changes are traceable and deterministic.
- Undo/redo works for core edit actions.
- Components consume selectors instead of ad-hoc local state.

---

## Phase 3 - Legacy JS Port (Services + Hooks + Store)

### Scope

Port corrupted legacy logic from `tools/MapEditor/index.html` into modular React architecture.

### Port Strategy

1. **Lock parity contract** from the legacy baseline section in this document.
2. **Extract pure logic first** into services (no DOM dependencies).
3. **Define typed store slices** for map, tools, view, history, and io.
4. **Wrap editor interactions** into hooks.
5. **Connect hooks/services to store actions**.
6. **Bind UI controls** to typed actions/selectors.
7. **Validate each migrated feature** against parity checklist before moving on.

### Port Order (Vertical Slices)

1. **Map core slice:** resize, clear, paint/erase/toggle, drag paint.
2. **Palette slice:** categorized tile palette with active-state handling.
3. **Building slice:** preset selection, preview overlay, rotation, placement.
4. **Trainer slice:** directional marker rendering + vision cone overlay rules.
5. **Entity analyzer slice:** grouped entity list and encounter aggregation.
6. **Persistence slice:** local storage autosave/load and reset behavior.
7. **IO slice:** JSON import/export and C# export output panel.

### Service Candidates

- `mapTransformService` (paint, fill, resize, normalize).
- `buildingPlacementService` (rotation matrix, bounds checks, preview footprint).
- `trainerVisionService` (vision cone rays and blocking rules).
- `entitySummaryService` (derive grouped entity/encounter report from grid).
- `mapValidationService` (bounds, schema checks, tile compatibility).
- `mapSerializationService` (JSON in/out, schema versioning).
- `exportService` (game-ready data structures for MonoGame pipeline).

### Hook Candidates

- `useCanvasInteraction`
- `useKeyboardShortcuts`
- `useViewport`
- `useBuildingPlacement`
- `useTrainerVisionOverlay`
- `useEntitySummary`
- `useAutosave`
- `useSelection`

### Deliverables

- Legacy behavior restored in modular form.
- No inline monolithic script logic in React components.
- Feature parity checklist with pass/fail status.

### Acceptance Criteria

- Critical legacy workflows behave correctly.
- Logic is unit-testable via services.
- No duplicate/conflicting implementations of the same feature.

---

## Feature Parity Checklist (Legacy -> React)

- Grid sizing and cell size changes update rendering correctly.
- Paint, drag paint, right-click erase, and same-tile toggle behavior match legacy.
- Category palette and tile selection behavior match legacy.
- Building presets can be selected, rotated, previewed, and stamped.
- Trainer arrows and vision overlays render with correct blocking behavior.
- Entity panel grouping/counts match map contents.
- Local storage autosave/load/reset behavior works.
- JSON import/export round trips valid maps.
- C# export includes map array + trainer/encounter helpers.

---

## Phase 4 - Data Reliability + Export Pipeline

### Scope

Ensure generated map data is stable for game integration.

### Deliverables

- Versioned map schema.
- Save/load robustness and migration path for older map files.
- Export format aligned with game runtime expectations.

### Acceptance Criteria

- Round-trip save/load is lossless for supported fields.
- Invalid data is blocked with actionable errors.
- Exported files are consumable by game-side tooling.

---

## Phase 5 - Hardening and Polish

### Scope

- Error boundaries and resilient recovery paths.
- Performance pass for large maps.
- Accessibility and keyboard completeness.

### Deliverables

- Performance optimizations (memoization, selective rendering, canvas batching where needed).
- A11y improvements (focus management, aria labels for controls).
- QA checklist and bug triage report.

### Acceptance Criteria

- Smooth editing on target map sizes.
- Core workflows accessible without mouse-only dependence.
- No blocker bugs in create/edit/export flow.

---

## Milestone Plan (Execution Order)

1. Tailwind + app shell finalized.
2. Responsive layout + component primitives complete.
3. Store and typed models integrated.
4. First vertical slice ported (paint + layer select + save/load).
5. Remaining legacy features ported in batches.
6. Export pipeline verified with game side.
7. Hardening/performance/a11y pass.

---

## Definition of Done

- Frontend is modular (components/hooks/services/store cleanly separated).
- Legacy feature set is ported with validated parity.
- Editor provides a user-friendly, responsive workflow for map creation.
- Output data is reliable and game-ready.
- Codebase is maintainable for future features (NPC tools, event scripting, biome presets, etc.).

---

## Risks and Mitigations

- **Risk:** Legacy behavior is ambiguous due to corrupted code.
  - **Mitigation:** Build parity checklist and validate each feature against observable behavior.
- **Risk:** State complexity causes regressions.
  - **Mitigation:** Centralized typed actions + service-level tests.
- **Risk:** Canvas interaction bugs across zoom/pan.
  - **Mitigation:** Isolate coordinate math in tested utilities/hooks.
- **Risk:** Export mismatch with runtime.
  - **Mitigation:** Early schema contract with game-side consumer and sample fixtures.

---

## Immediate Next Work Items

1. Wire Tailwind into `tools/map-editor` (`vite.config.ts`, global CSS import).
2. Implement `AppShell` with responsive top/left/main/right/bottom regions that map to legacy controls.
3. Build reusable UI primitives (button, icon button, panel, tabs, input groups, status bar).
4. Define initial editor types (`TileType`, `BuildingPreset`, `MapGrid`, `TrainerVision`, `EntitySummary`) and scaffold store slices.
5. Implement first parity slice: resize + paint/erase/toggle + drag paint + autosave.
6. Add a parity test matrix using the checklist in this doc.
