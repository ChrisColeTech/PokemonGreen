# OhanaCli Split Model + Animation Plan

> Goal: rewrite export flow so one shared model can be used with separately exported animation clips,
> matching real runtime usage (single rig + many clips) and avoiding Blender COLLADA multi-clip warping.

---

## Table of Contents

1. [Problem Statement](#1-problem-statement)
2. [Target Architecture](#2-target-architecture)
3. [Output Contract and Naming](#3-output-contract-and-naming)
4. [Implementation Plan](#4-implementation-plan)
5. [Validation Plan](#5-validation-plan)
6. [Risks and Mitigations](#6-risks-and-mitigations)
7. [Acceptance Criteria](#7-acceptance-criteria)
8. [Removal and Replacement Matrix](#8-removal-and-replacement-matrix)
9. [MonoGame Runtime Wiring](#9-monogame-runtime-wiring)

---

## 1. Problem Statement

- Consolidated DAE (`one file, many clips`) is unstable in Blender import for this pipeline and causes skew/warp.
- Per-clip DAE currently works, but duplicates mesh/skeleton data per animation file.
- We need a runtime-friendly export: **one shared model asset + separate animation clip assets**.

Desired outcome:
- Export model and clips separately.
- Keep deterministic skeleton/bone naming so clips can bind reliably to the same model in tooling and game runtime.

---

## 2. Target Architecture

### 2.1 Asset Split

- **Model asset (authoritative):**
  - Meshes, skeleton, skin controllers, materials, texture references.
  - No clip stacking in timeline.
- **Animation clip assets (N files):**
  - Skeletal animation channels only.
  - Same bone target names/IDs as model asset.

### 2.2 Modes

- Rewrite target mode:
  - `split model + clips` mode (primary Blender/runtime-safe path)
- Supported DAE behaviors after rewrite:
  - split model + clips (primary)
  - single clip via `-a` for focused debugging/export
- Removed:
  - consolidated multi-clip DAE mode (`--consolidate-animations`)

---

## 3. Output Contract and Naming

### 3.1 Folder Structure

Per grouped model output folder (example `0000_model/`):

```
0000_model/
  model.dae
  clips/
    clip_000.dae
    clip_001.dae
    ...
  textures/
    <texture-name>.png
  manifest.json
```

### 3.2 Naming Rules

- Model file: `model.dae` (or `model_1.dae` for additional model indices as needed).
- Clip files: `clip_{index:D3}.dae`.
- Texture files: `<sanitized-texture-name>.png`.
- Manifest file: `manifest.json` with deterministic references.

### 3.3 Manifest Schema (v1)

- `version`
- `modelFile`
- `textures[]`
- `clips[]` with:
  - `index`
  - `id` (stable clip id, for example `clip_000`)
  - `name` (legacy/display id, same as `id`)
  - `sourceName` (raw source clip name when available)
  - `semanticName` (gameplay label, for example `Idle`/`Walk`/`Run`, nullable)
  - `file`
  - `frameCount`
  - `fps`

---

## 4. Implementation Plan

### Phase 1: CLI Surface

Files:
- `src/PokemonGreen.OhanaCli/src/OhanaCli.App/Program.cs`

Tasks:
- Add a new flag: `--split-model-anims` (DAE only).
- Validation rules:
  - incompatible with `-a`
  - explicit warning/fallback for OBJ
 - Remove `--consolidate-animations` from CLI surface.

### Phase 2: Export Pipeline Separation

Files:
- `src/PokemonGreen.OhanaCli/src/OhanaCli.App/Program.cs`
- `src/PokemonGreen.OhanaCli/src/OhanaCli.Formats/Models/GenericFormats/DAE.cs`

Tasks:
- Add model-only DAE export path (no skeletal clip emission).
- Add clip-only DAE export path (animation library only, binding to canonical bone targets).
- Ensure shared target convention remains exactly: `<BoneName>_bone_id/...`.
- Keep texture export from grouped model complete (including trailing entries past `--limit` when needed).

### Phase 3: Manifest + Deterministic Linking

Files:
- `src/PokemonGreen.OhanaCli/src/OhanaCli.App/Program.cs`

Tasks:
- Emit `manifest.json` per output group.
- Include model file, texture list, clip file list, clip metadata.
- Ensure deterministic ordering by clip index and sanitized names.

### Phase 4: Dead Code Removal + Safety

Files:
- `src/PokemonGreen.OhanaCli/src/OhanaCli.App/Program.cs`
- `src/PokemonGreen.OhanaCli/src/OhanaCli.Formats/Models/GenericFormats/DAE.cs`

Tasks:
- Remove consolidated-export branches from CLI and exporter.
- Preserve existing single-clip export behavior (`-a`).
- Preserve current texture/material fixes and materialId safety handling.

---

## 5. Validation Plan

### 5.1 Automated

- Build:
  - `dotnet build src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -c Release`
- Tests:
  - `dotnet test tests/OhanaCli.App.Tests/OhanaCli.App.Tests.csproj --no-build -v minimal`

### 5.2 Export Checks

Commands:

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- \
  convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" \
  -o "src/PokemonGreen.Tests/exports-split" -n 20 --split-model-anims
```

Verify:
- `model.dae` exists
- `clips/clip_###.dae` files exist for all discovered clips
- textures are present for each model group
- `manifest.json` references only existing files

### 5.3 Blender + Runtime Smoke

- Import `model.dae` once, then import/apply `clips/clip_###.dae` via helper workflow.
- Confirm no armature skew/warp across clip switching.
- Confirm body/eye/iris materials are correctly mapped.

---

## 6. Risks and Mitigations

- **Risk:** DAE clip-only import behavior differs by tool.
  - **Mitigation:** manifest + optional Blender helper script for clip attach.
- **Risk:** Bone target mismatches cause clips not to apply.
  - **Mitigation:** strict reuse of existing bone target naming and IDs.
- **Risk:** `--limit` truncates texture entries.
  - **Mitigation:** keep current trailing-group texture completion logic.

---

## 7. Acceptance Criteria

- New `--split-model-anims` mode exports one shared model and separate clip files.
- `--consolidate-animations` is removed from CLI and code.
- All clip files target the same skeleton naming convention as model file.
- Textures are exported alongside model output for each group.
- Blender no longer shows cross-clip warp from stacked consolidated timelines in this new mode.

---

## 8. Removal and Replacement Matrix

This section explicitly defines what is removed, replaced, and kept.

### 8.1 CLI Flags

- `--split-model-anims`
  - **Status:** Added
  - **Purpose:** Primary rewrite target mode.

- `--consolidate-animations`
  - **Status:** Removed
  - **Behavior:** No longer accepted by CLI.

- `-a, --animation-index`
  - **Status:** Kept
  - **Behavior:** Single-clip export path remains.

### 8.2 Export Behaviors

- Consolidated DAE multi-clip timeline as primary workflow
  - **Status:** Removed and replaced by split model + clips workflow.
  - **Reason:** Persistent Blender import instability/warping from stacked channels.

- Per-clip DAE with duplicated mesh per file
  - **Status:** Kept for compatibility; superseded by split mode for runtime pipelines.

- Model + texture grouped export logic
  - **Status:** Kept and strengthened.
  - **Reason:** Required so groups always include referenced textures, even when `--limit` is used.

### 8.3 Code Areas Expected to Change

- `src/PokemonGreen.OhanaCli/src/OhanaCli.App/Program.cs`
  - Add new flag parsing and mode routing.
  - Remove `--consolidate-animations` parsing and handling.
  - Add split-mode manifest emission.

- `src/PokemonGreen.OhanaCli/src/OhanaCli.Formats/Models/GenericFormats/DAE.cs`
  - Add explicit model-only and clip-only DAE emission paths.
  - Remove consolidated timeline/animation-clip stacking path.
  - Keep current channels/targets naming for compatibility.

### 8.4 Non-Goals (Not Removed in This Rewrite)

- Existing OBJ export path.
- Existing non-consolidated DAE path.
- Existing single-clip `-a` path.

---

## 9. MonoGame Runtime Wiring

In MonoGame (via content pipeline/custom processor), a DAE model effectively provides:

- skeleton hierarchy (bones)
- one or more animation clips (keyframes per bone)
- skinned meshes bound to that skeleton

### 9.1 Core Rule for Reusing One Model with Many Clips

- All clips must target the same skeleton contract:
  - same bone names
  - same hierarchy structure
- In this project, clip-to-model binding is by bone name (for example `Waist_bone_id`).

### 9.2 Organization Options

#### Option A: Single DAE with model + all clips

- Contains mesh, skeleton, and all clips (`Idle`, `Walk`, `Run`, `Attack`, etc.) in one file.
- Simpler packaging, but this path has shown warping/skew issues in Blender/COLLADA for this pipeline.

#### Option B: Model DAE + separate clip DAEs (target architecture)

- Export one shared model DAE and separate clip-only DAEs.
- MonoGame does not automatically attach clip DAEs to an already loaded model by default.
- Wiring is handled by runtime/content code:
  1. load model DAE and build skeleton once
  2. load each clip DAE and extract animation tracks
  3. map clip tracks to model bones by name
  4. store clips by key (for example `clips["Idle"]`, `clips["Run"]`)
  5. sample selected clip at runtime (for example `Play("Run")`)

### 9.3 High-Level Runtime Flow

- Keep `BoneTransforms[]` local transforms (one per bone).
- Each frame:
  - sample active clip at time `t`
  - write animated local transforms to corresponding bone indices
  - evaluate hierarchy to world transforms
  - compute final skin matrices
  - submit to `SkinnedEffect` or custom skinning shader

### 9.4 MonoGame Practical Note

- Stock MonoGame `Model` does not expose robust animation clip runtime APIs out of the box.
- Common approaches:
  - MGCB + custom content processor to emit skeleton + clips runtime data
  - classic SkinnedModel sample style pipeline
  - third-party runtime libraries (feature support varies)

### 9.5 Key Takeaway

- The engine does not magically bind separate animation assets to a model.
- Your runtime must:
  - extract clip data
  - match tracks to bones by name
  - sample the selected clip
  - apply sampled transforms to the shared skeleton
