# SPICA Split Model + Animation Plan

> Goal: rewrite the SPICA DAE export so it produces one shared model file and separate
> animation clip files, matching real game runtime usage (single rig + many clips)
> and eliminating Blender COLLADA warping from baked-in animations.

---

## Table of Contents

1. [Problem Statement](#1-problem-statement)
2. [Current Architecture](#2-current-architecture)
3. [Target Architecture](#3-target-architecture)
4. [Output Contract and Naming](#4-output-contract-and-naming)
5. [Implementation Plan](#5-implementation-plan) (Phases 1–6)
6. [Validation Plan](#6-validation-plan)
7. [Risks and Mitigations](#7-risks-and-mitigations)
8. [Acceptance Criteria](#8-acceptance-criteria)

---

## 1. Problem Statement

- Current export bakes one animation per DAE file, duplicating the full model (mesh, skeleton,
  skin controllers, textures) in every animation file. For Bulbasaur: 57 copies of the same
  ~900KB mesh data.
- Consolidated exports (model + baked animation) warp/skew in Blender when the animation plays.
  The static T-pose is correct but playback distorts the body.
- Individual per-clip DAE files (from Spica.Registry) work correctly but are wasteful.
- The game runtime needs: **one model asset loaded once + animation clips swapped at runtime**.

### What works today

| Export | Tool | Result |
|---|---|---|
| `anim_000_GFMotion.dae` (model + 1 clip) | Spica.Registry | Works in Blender |
| `PM_HighPoly.dae` (model + 1 clip) | SpicaCli grouped | Warps in Blender |
| `model_0.dae` (static, no animation) | Spica.Registry | Works (no animation to warp) |

### Root cause

Both exports produce nearly identical DAE data (same bones, same bind matrices, same animation
channels). The warping is likely caused by how the H3D scene is constructed when merging
animation entries from separate GARC entries through different code paths
(`FormatIdentifier` → `GFPkmnModel.OpenAsH3D` → `GFModelPack.ToH3D()` vs
Spica.Registry's `LoadPCPackage` → `GFModel.ToH3DModel()` directly).

Regardless of root cause, the correct fix is architectural: **stop baking animations into
model files entirely**. Export model and clips separately.

---

## 2. Current Architecture

### 2.1 DAE Constructor (`Spica.Core/Formats/Generic/COLLADA/DAE.cs`)

```csharp
public DAE(H3D Scene, int MdlIndex, int AnimIndex = -1)
```

- `AnimIndex == -1`: exports static model with Euler bone format (`SetBoneEuler`)
- `AnimIndex >= 0`: exports model with one animation baked in, matrix bone format (`SetBoneMatrix`),
  animation channels targeting `{BoneName}_bone_id/transform`

There is **no animation-only export mode**. Every DAE with animation also contains the full
model geometry.

### 2.2 Export Tools

**Spica.Registry** (`Spica.Registry/Program.cs`):
- GARC scan → classify entries → `BuildGroups()` → per-Pokemon folders
- Exports `model_0.dae` (static, `animIdx=-1`) + `anim_NNN_*.dae` (each = full model + 1 clip)
- Uses its own `LoadPCPackage` to read GARC entries

**SpicaCli** (`SpicaCli/Program.cs`):
- GARC → per-entry processing via `FormatIdentifier.IdentifyAndOpen`
- Uses `GFPkmnModel.OpenAsH3D` → `GFModelPack.ToH3D()`
- Currently processes entries independently (no grouping)

### 2.3 GARC Entry Layout (Pokemon Model GARC)

```
Entry 0: Model PC package   (GFModel × 2: high-poly + low-poly, shaders)
Entry 1: Texture PC package  (GFTexture × N: body, eye, iris variants + normals)
Entry 2: Shiny/alt textures  (GFTexture × N)
Entry 3+: Animation PC pkgs  (GFMotion × N per pkg: idle, walk, attack, etc.)
...
Entry M: Next Pokemon model  (starts new group)
```

---

## 3. Target Architecture

### 3.1 New DAE Export Modes

Add a third mode to the `DAE` constructor — **clip-only export**:

| Mode | Contains | Bone format | Use case |
|---|---|---|---|
| Static (`AnimIndex == -1`) | Mesh + skeleton + skin + materials + textures | Euler | Model asset |
| Clip-only (new) | Skeleton + animation channels only | Matrix | Animation clip |
| Baked (existing) | Mesh + skeleton + skin + materials + textures + animation | Matrix | Legacy/self-contained |

The clip-only DAE contains:
- `library_visual_scenes` with skeleton hierarchy (bone nodes, no mesh nodes)
- `library_animations` with matrix channels targeting `{BoneName}_bone_id/transform`
- No `library_geometries`, `library_controllers`, `library_images`, `library_materials`, `library_effects`

### 3.2 Skeleton Consistency

Both model and clip DAEs must use **identical bone node IDs and SIDs**:
- Node `id="{BoneName}_bone_id"`, `sid="{BoneName}"`, `type="JOINT"`
- Animation channel target: `{BoneName}_bone_id/transform`

The model file uses Euler format for bone transforms (bind pose). The clip files use matrix
format for animation channels. Blender resolves animation channels by matching the channel
target (`{BoneName}_bone_id/transform`) to the node ID, regardless of the bone's initial
transform format.

**Important:** Blender's COLLADA importer does NOT auto-bind clip animations to an existing
armature. Importing a clip DAE creates a **second, disconnected armature** with its own Action.
To use the clip on the original model, you must transfer the Action to the original armature
and delete the extra one. This can be done manually or via a helper script (see Phase 5).

The clip DAE still needs the full skeleton hierarchy so Blender creates the Action with
correct bone names that match the model armature.

### 3.3 Runtime Wiring (MonoGame)

MonoGame does not automatically "attach" separate DAE animation files to a loaded model.
The wiring is our responsibility. The runtime flow:

1. **Load model once** — parse `model.dae` via Assimp → build skeleton hierarchy, mesh
   geometry, skin weights, textures. This is the `SkeletalModelData` that lives in memory.
2. **Load clips separately** — parse each `clips/clip_NNN.dae` via Assimp (or direct
   COLLADA XML parsing) → extract animation keyframes (position, rotation, scale per bone
   per frame). No mesh data needed.
3. **Map clip bone tracks to model bones by name** — each clip channel targets a bone
   name like `Waist`. Match it to the model's bone index. Store as a lightweight
   `AnimationClip` (just keyframe arrays + bone index mapping).
4. **Store clips in a dictionary** — `clips["Idle"]`, `clips["Walk"]`, `clips["Attack"]`, etc.
5. **At runtime: sample and apply** — each frame:
   - Sample the active clip at time `t` → get local transform per animated bone
   - Write transforms into `BoneTransforms[]` array
   - Walk hierarchy: `worldTransform[i] = localTransform[i] * worldTransform[parent]`
   - Compute final skin matrices: `skinMatrix[i] = inverseBindPose[i] * worldTransform[i]`
   - Apply to vertices (CPU skinning) or pass to shader

The existing `SkeletalModelData` class (`src/PokemonGreen.Assets/SkeletalModelData.cs`)
already does CPU skinning with a single baked animation. It needs to be extended to:
- Separate the animation data from the model data
- Support loading multiple clips from separate files
- Allow switching active clip at runtime

**Key constraint:** clip bone names must exactly match model bone names. Both the export
pipeline and the runtime loader depend on this name-based matching.

---

## 4. Output Contract and Naming

### 4.1 Folder Structure

```
pm0001_00/
  model.dae                     Static model (mesh + skeleton + skin + materials)
  model_lowpoly.dae             Low-poly variant (optional)
  textures/
    pm0001_00_BodyA1.tga.png
    pm0001_00_BodyB1.tga.png
    pm0001_00_Eye1.tga.png
    pm0001_00_Iris1.tga.png
    ...
  clips/
    clip_000.dae                Animation-only DAE (skeleton + channels)
    clip_001.dae
    ...
  manifest.json
```

### 4.2 Manifest Schema (v1)

```json
{
  "version": 1,
  "pokemonId": "pm0001_00",
  "model": {
    "file": "model.dae",
    "meshCount": 9,
    "boneCount": 55
  },
  "modelLowPoly": {
    "file": "model_lowpoly.dae",
    "meshCount": 6,
    "boneCount": 55
  },
  "textures": [
    { "name": "pm0001_00_BodyA1.tga", "file": "textures/pm0001_00_BodyA1.tga.png", "width": 256, "height": 256 }
  ],
  "clips": [
    { "index": 0, "name": "Motion_0", "file": "clips/clip_000.dae", "frameCount": 42, "fps": 30 }
  ]
}
```

---

## 5. Implementation Plan

### Phase 1: Clip-Only DAE Export Mode

**File:** `src/PokemonGreen.Spica/Spica.Core/Formats/Generic/COLLADA/DAE.cs`

Add a new constructor or flag for clip-only export:

```csharp
// Option A: new constructor
public DAE(H3D Scene, int MdlIndex, int AnimIndex, bool clipOnly)

// Option B: sentinel value (e.g. MdlIndex = -1 means no model)
public DAE(H3D Scene, int AnimIndex)  // clip-only, no model geometry
```

Implementation:
1. Build skeleton hierarchy in `library_visual_scenes` (same bone nodes as model export)
2. Use `SetBoneMatrix` for bone transforms (bind pose as initial values)
3. Build `library_animations` with matrix channels (existing animation code)
4. **Skip**: `library_geometries`, `library_controllers`, `library_images`, `library_materials`, `library_effects`
5. Keep the same bone ID convention: `{BoneName}_bone_id`

### Phase 2: Update Spica.Registry Export

**File:** `src/PokemonGreen.Spica/Spica.Registry/Program.cs`

Update `RunExport` to use split mode:

1. Export model with `new DAE(scene, modelIndex, -1)` → `model.dae` (existing, no change)
2. Export each animation with `new DAE(scene, animIndex, clipOnly: true)` → `clips/clip_NNN.dae`
3. Export textures to `textures/` subdirectory
4. Write `manifest.json`
5. Stop exporting `anim_NNN_*.dae` files (full model + animation)

### Phase 3: Update SpicaCli GARC Export

**File:** `src/PokemonGreen.Spica/SpicaCli/Program.cs`

Update `ConvertGARC` to:

1. Group entries by Pokemon (model starts group, subsequent texture/animation entries join)
2. For each group:
   - Load model via `FormatIdentifier.IdentifyAndOpen`
   - Merge textures from texture entries
   - Load animations from animation entries (using model skeleton)
   - Export using split mode (model.dae + clips/ + textures/ + manifest.json)

### Phase 4: Model Bone Format

Both model and clip DAEs should use `SetBoneMatrix` (matrix format) so bone node SIDs are
consistent. The model uses the bind-pose matrix; clips use animated matrices per frame.
This ensures the Action bone names from a clip import match the armature bone names from
the model import exactly.

### Phase 5: Blender Helper Script

**File:** `tools/blender_import_clips.py` (Blender Python script)

Blender does not auto-bind clip animations to an existing armature. Importing a clip DAE
creates a second disconnected armature. The helper script automates the transfer workflow:

1. User imports `model.dae` manually (creates armature + mesh)
2. User runs the script, pointing it at the `clips/` folder (or `manifest.json`)
3. Script iterates clip DAE files:
   - Imports each clip DAE (Blender creates a temp armature + Action)
   - Renames the Action to match the clip name from manifest
   - Transfers the Action to the original model armature
   - Deletes the temp armature
4. Result: one armature with all clips available as Actions in the Action Editor / NLA

Alternatively, a simpler manual workflow (document in README):
1. Import `model.dae`
2. Import `clip_000.dae` — creates second armature
3. In Dope Sheet > Action Editor, find the new Action
4. Select the original armature, assign the Action to it
5. Delete the second armature
6. Repeat for each clip

### Phase 6: MonoGame Runtime — Clip Loading and Switching

**Files:**
- `src/PokemonGreen.Assets/SkeletalModelData.cs` (extend)
- `src/PokemonGreen.Assets/AnimationClip.cs` (new)
- `src/PokemonGreen.Assets/PokemonModelLoader.cs` (update)

The existing `SkeletalModelData` bakes one animation into the model. Refactor to separate
model data from animation data:

**AnimationClip (new class):**
```
- Name (string)
- Duration (float, seconds)
- TicksPerSecond (float)
- Channels[] — one per animated bone:
  - BoneIndex (int, mapped to model skeleton)
  - PositionKeys[] (time + Vector3)
  - RotationKeys[] (time + Quaternion)
  - ScaleKeys[] (time + Vector3)
```

**SkeletalModelData changes:**
- Remove baked animation fields (single AnimChannel[] etc.)
- Add `Dictionary<string, AnimationClip> Clips`
- Add `AnimationClip ActiveClip` + `double ClipTime`
- `Update(double elapsedSeconds)` samples `ActiveClip` instead of baked data
- `Play(string clipName)` switches active clip, resets time
- `LoadClip(string daeFilePath, string clipName)` parses a clip DAE and adds to dictionary

**PokemonModelLoader changes:**
- `LoadSkeletal()` loads `model.dae` (no animation) → `SkeletalModelData`
- Reads `manifest.json` to discover clip files
- Loads each clip file → `AnimationClip` → adds to model's `Clips` dictionary
- Or: lazy-load clips on first use

---

## 6. Validation Plan

### 6.1 Build

```bash
dotnet build src/PokemonGreen.Spica/Spica.Core/Spica.Core.csproj
dotnet build src/PokemonGreen.Spica/SpicaCli/SpicaCli.csproj
dotnet build src/PokemonGreen.Spica/Spica.Registry/Spica.Registry.csproj
```

### 6.2 Export Test

```bash
# Spica.Registry (scan + export)
dotnet run --project src/PokemonGreen.Spica/Spica.Registry -- \
  scan "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o registry.json -n 20

dotnet run --project src/PokemonGreen.Spica/Spica.Registry -- \
  export registry.json "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" \
  -o "src/PokemonGreen.Tests/exports-split"

# SpicaCli (direct GARC export)
dotnet run --project src/PokemonGreen.Spica/SpicaCli -- \
  convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" \
  -o "src/PokemonGreen.Tests/spicacli-split" -n 20
```

Verify:
- `model.dae` has mesh geometry, skeleton, skin controllers, materials — no `<library_animations>`
- `clips/clip_000.dae` has skeleton and `<library_animations>` — no `<library_geometries>`
- Bone IDs match between model and clip files
- `manifest.json` references only existing files
- Texture PNGs exist in `textures/`

### 6.3 Blender Smoke Test

1. Import `model.dae` — model displays in T-pose, correct textures
2. Import `clips/clip_000.dae` — creates Action on same armature (or new armature with matching names)
3. Apply action to model armature — animation plays without warping
4. Switch between clip_000, clip_001, clip_002 — all play correctly on the same model
5. No body distortion, no bone skew

---

## 7. Risks and Mitigations

1. **Risk (known):** Blender COLLADA importer does NOT auto-bind clip animations to existing armatures.
   Importing a clip creates a second disconnected armature.
   - **Mitigation:** Phase 5 Blender helper script automates Action transfer + cleanup.
   - **Mitigation:** Document manual workflow as fallback.

2. **Risk:** Bone name mismatch between model and clip armatures prevents Action transfer.
   - **Mitigation:** Both model and clip DAEs use identical bone IDs (`{BoneName}_bone_id`)
     generated from the same skeleton data. Phase 4 standardizes both on matrix format.

3. **Risk:** GFModelPack.ToH3D() vs LoadPCPackage produce subtly different skeleton data.
   - **Mitigation:** Standardize both SpicaCli and Spica.Registry on the same loading path.
     Prefer `LoadPCPackage` (simpler, proven to work).

4. **Risk:** GARC `-n` limit truncates a group mid-way (model loaded but textures/animations cut off).
   - **Mitigation:** Complete the current group even if entry count exceeds `-n`.

---

## 8. Acceptance Criteria

### Export pipeline
- [ ] New clip-only DAE export mode in `DAE.cs` — skeleton + animation channels, no mesh
- [ ] Both model and clip DAEs use matrix bone format with identical bone IDs
- [ ] Spica.Registry `export` produces `model.dae` + `clips/clip_NNN.dae` + `textures/` + `manifest.json`
- [ ] SpicaCli `convert` for GARC produces the same split structure
- [ ] Existing baked export mode (`animIndex >= 0`) still functions for backward compatibility
- [ ] Textures exported alongside model, not duplicated per clip

### Blender validation
- [ ] Blender helper script imports clips and transfers Actions to model armature
- [ ] Transferred clip Action plays on model without warping
- [ ] Switching between clip Actions on same armature works correctly

### MonoGame runtime
- [ ] `SkeletalModelData` loads model from static DAE (no baked animation)
- [ ] `AnimationClip` loads from clip-only DAE, maps bone tracks by name
- [ ] Multiple clips loadable per model, switchable at runtime via `Play(clipName)`
- [ ] Pokemon renders with correct idle animation in battle
- [ ] Switching clips (idle → attack → faint) works without warping
