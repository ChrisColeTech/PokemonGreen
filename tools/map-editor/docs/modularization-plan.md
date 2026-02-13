# Map Editor Modularization Plan

## Goal

Refactor `tools/map-editor/src` into smaller, testable modules with clear boundaries, without changing user-facing behavior.

## Non-goals

- No visual redesign.
- No schema-breaking import/export changes.
- No gameplay logic changes for generated maps unless explicitly marked as bug fixes.

## Guiding Rules

- Behavior-preserving refactors first, feature changes second.
- Keep deterministic generation outputs stable for fixed seed/archetype/template.
- Prefer small files with single responsibility.
- Keep browser APIs isolated from pure logic.
- Add or update tests alongside each refactor phase.

## Target Architecture

```text
src/
  app/
  components/
    layout/
    sidebar/
  hooks/
  store/
    slices/
    selectors/
  services/
    io/
    map/
    generation/
      passes/
      validation/
      repair/
      balance/
      shared/
  data/
    generation/
  types/
  utils/
```

## Phase Plan

### Phase 1 - Shared Contracts and Constants

Scope:

- Introduce centralized tile role/constants module used by generation/store/io.
- Extract shared helper utilities (`pointKey`, clamp/lerp/math helpers, bounds checks) into `services/generation/shared`.
- Add typed selector helpers for store reads.

Deliverables:

- No behavior changes.
- Reduced magic numbers and duplication.

Acceptance:

- Build and tests pass.

---

### Phase 2 - Generation Pass Decomposition

Scope:

- Split `services/generation/randomMapGenerator.ts` into pass modules:
  - `passes/initializePass.ts`
  - `passes/carvePrimaryPathsPass.ts`
  - `passes/paintBiomesPass.ts`
  - `passes/reserveDistrictsPass.ts`
  - `passes/placeBuildingsPass.ts`
  - `passes/placeEncountersPass.ts`
  - `passes/placeInteractivesAndEntitiesPass.ts`
  - `passes/balancePass.ts`
  - `passes/validatePass.ts`
  - `passes/repairPass.ts`
  - `passes/finalizePass.ts`
- Keep orchestrator thin and registry-driven.

Deliverables:

- Small pass files with clear inputs/outputs.
- Existing tests unchanged and green.

Acceptance:

- Determinism tests still pass for fixtures.

---

### Phase 3 - Validation and Repair Decomposition

Scope:

- Split hard constraint validators into per-rule modules.
- Split repairs into per-action modules.
- Add registry maps for constraints and repair actions.

Deliverables:

- Easier targeted tests and debugging by rule/action id.

Acceptance:

- Existing hard constraint and repair behavior preserved.

---

### Phase 4 - Store Slice Refactor

Scope:

- Break `editorStore.ts` into slice creators:
  - `mapSlice`
  - `paintSlice`
  - `buildingSlice`
  - `historySlice`
  - `generationSlice`
  - `ioSlice`
- Keep one composed store export for compatibility.

Deliverables:

- Reduced merge conflicts and easier maintenance.

Acceptance:

- No UI behavior regressions.

---

### Phase 5 - Sidebar Component Decomposition

Scope:

- Split `components/layout/LeftSidebar.tsx` into section components:
  - `PaletteSection`
  - `MapDimensionsSection`
  - `RandomGenerationSection`
  - `BuildingControlsSection`
- Keep shared styling and props contracts.

Deliverables:

- Smaller, testable UI units and reduced rerender scope.

Acceptance:

- Same controls and interactions available as before.

---

### Phase 6 - IO Service Decomposition

Scope:

- Split `mapIoService.ts` into:
  - `io/mapSchema.ts`
  - `io/mapParser.ts`
  - `io/mapSerializer.ts`
  - `io/fileDownloadService.ts`
  - `io/fileOpenService.ts`
- Keep existing public API facade for backward compatibility during migration.

Deliverables:

- Pure parse/serialize logic separated from browser file APIs.

Acceptance:

- Import/export and save workflows unchanged.

---

### Phase 7 - Hooks and Selector Optimization

Scope:

- Reduce over-selection from store in hooks using selectors and shallow comparison.
- Create selector modules for sidebar/generation/menu hooks.

Deliverables:

- Lower unnecessary rerenders and cleaner hook code.

Acceptance:

- No functional changes; performance visibly smoother on large maps.

---

### Phase 8 - Test and Regression Hardening

Scope:

- Add snapshot-like deterministic output checks for representative seeds.
- Add focused tests for each pass/constraint/repair module.
- Add smoke tests for import/export parser/serializer round-trip.

Deliverables:

- Safer refactoring baseline.

Acceptance:

- `npm run test` and `npm run build` stable.

## Execution Strategy

- Run one phase at a time using an agent to minimize merge conflicts.
- After each phase:
  - run `npm run test`
  - run `npm run build`
  - verify no behavior regressions in key workflows (paint, building place, generate, export/import).

## Rollback Safety

- Keep refactors behavior-preserving.
- Prefer adapter/facade exports during transitions.
- Avoid cross-phase breaking moves in one commit.
