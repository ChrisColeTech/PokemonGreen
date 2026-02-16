# 20 - Animation Pipeline & BgEditor Architecture: Lessons Learned & Handoff

**Date:** 2026-02-16
**Scope:** OhanaCli DAE skeletal animation export (matrix animation, GARC animation merging), BgEditor multi-page architecture (routing, texture processing, export pipeline), Blender COLLADA import deep-dive
**Codebase:** OhanaCli (`DAE.cs`, `Program.cs`), BgEditor (`App.tsx`, `editorStore.ts`, `Sidebar.tsx`, `textureProcessor.ts`, routes), Blender source (`AnimationImporter.cpp`, `collada_internal.cpp`, `ArmatureImporter.cpp`)

---

## 1. What We Accomplished

### OhanaCli: Matrix Animation Export (DAE.cs)

**Switched from decomposed to matrix animation:**
Per-component animation (individual `<rotate sid="rotationX">`, `<translate sid="location">` channels targeting SIDs like `rotationX.ANGLE`, `location.X`) was silently ignored by Blender — the model would T-pose. After extensive analysis of Blender's OpenCOLLADA import code, we found that per-component SID resolution was failing. Switched to **matrix animation**: each bone gets a single `<matrix sid="transform">` element, animated with a stride-16 float4x4 output targeting `BoneName_bone_id/transform`. This is the same code path Blender's own exporter uses (`apply_matrix_curves`).

**New helper functions added to DAE.cs:**
- `buildLocalBoneMatrix(OBone)` — constructs a 4x4 local transform matrix from bone S/R/T values using the same composition order as `transformSkeleton`: `S * Rz * Ry * Rx * T` (OMatrix code order, which gives column-vector math `T * Rx * Ry * Rz * S`)
- `buildLocalMatrix(sx,sy,sz,rx,ry,rz,tx,ty,tz)` — same as above but takes individual float values for animation keyframe interpolation
- `sampleKeyframes(group, frame)` — linearly interpolates BCH animation keyframe values at a given frame number
- `exportAnimation(dae, mdl, anim)` — builds one `<animation>` per bone with stride-16 matrix output, sampling all 9 S/R/T channels at each unique keyframe time

**Matrix output format:**
Both `daeMatrix.set()` (for rest pose) and `exportAnimation()` write 16 floats per matrix in the same order: `m[j, i]` with outer loop `i` (row 0..3), inner loop `j` (col 0..3), where OMatrix uses `[col, row]` indexing. This produces COLLADA row-major output of the column-vector transform matrix.

### OhanaCli: GARC Animation Merging (Program.cs)

**Problem:** Pokemon Sun/Moon GARC containers store model geometry, textures, and animations in separate sequential entries. Entry 1 has the mesh+skeleton, entry 2 has textures, and the animation BCH file is a separate model entry with no meshes — just skeletal animation data.

**Solution:** Modified container processing to detect "model entries without meshes" as animation-only BCH files. Instead of starting a new model group, their skeletal animations and textures are merged into the preceding model group:
```
if (hasMeshes) → flush previous, start new model group
else if (currentModel != null) → merge animations + textures into current model
```

**`--limit` / `-n` option:** Added entry limit for faster iteration on large GARCs (the Pokemon GARC has 10,549 entries). Usage: `ohanacli convert file.garc -o out -n 10`

### BgEditor: Multi-Page Architecture

**React Router integration (App.tsx):**
Converted from single-page layout to multi-page with `react-router-dom`:
- `/` — Editor page (3D viewport, texture panel, color controls, animation panel)
- `/tools` — Tools page (texture processing utilities)
- `Sidebar.tsx` — persistent navigation sidebar

**Live texture processing (editorStore.ts, textureProcessor.ts):**
Texture adjustments (brightness, contrast, hue, saturation) now apply in real-time to the Three.js scene. `processTexture()` applies adjustments via canvas operations and updates the Three.js texture. `resetTextureToOriginal()` reverts to the source image. Previously adjustments were preview-only and didn't update the 3D viewport.

**Backend routes (index.ts, routes/):**
Added modular Fastify route plugins for manifest scanning and texture processing. Increased body limit to 100MB for texture upload. Moved manifest generation from a build-time script (`generate-manifests.ts`, deleted) to on-demand server endpoint.

---

## 2. What Work Remains

### Critical: Animation Distortion in Blender (THE #1 BLOCKER)

The matrix animation exports and IS recognized by Blender (no more T-posing), but the model **flips, twists, and distorts** during playback. The rest pose renders correctly — the distortion only appears when animation is active.

**Root cause analysis (in progress):** After line-by-line analysis of Blender's `AnimationImporter.cpp`, the matrix format and convention are confirmed correct. The most likely causes are:

1. **BCH animation segment types not fully handled** — The BCH format has THREE animation segment types per bone:
   - `segmentType.transform` — Euler rotation + translation keyframes (what our code handles)
   - `segmentType.transformQuaternion` — Quaternion rotation + scale + translation frames (`bone.isFrameFormat = true`)
   - `segmentType.transformMatrix` — Fully baked 4x4 matrices per frame (`bone.isFullBakedFormat = true`)

   Our `exportAnimation()` ONLY checks `bone.scaleX/Y/Z`, `bone.rotationX/Y/Z`, `bone.translationX/Y/Z` (Euler keyframe groups). If a bone uses quaternion or matrix format, these groups are empty, `hasAnyKeyframes = false`, and the bone is silently skipped. **The exported Bulbasaur DAE has 40 animation channels for 55 bones — 15 bones have no animation.** Some of these may be quaternion-format bones incorrectly skipped.

2. **Rest pose vs animation frame mismatch** — The Waist bone's rest pose matrix and first animation frame matrix are dramatically different (60+ degrees of rotation difference). For a breathing idle animation, this is unexpected. This suggests either:
   - The BCH animation values may need different interpretation (axis-angle via `isAxisAngle` flag?)
   - The animation was authored for a different base pose than the skeleton rest pose
   - The Euler angles in the animation use a different convention than the rest pose

3. **`isAxisAngle` flag ignored** — `OSkeletalAnimationBone.isAxisAngle` exists but our export code doesn't check it. If true, the rotation values may represent an axis-angle rotation vector, not Euler angles. Building a matrix from axis-angle values as if they were Euler angles would produce wildly wrong rotations.

### Moderate: BgEditor New Pages Need Content

The `/tools` page and export panel are scaffolded but need implementation. The texture processing service (`textureProcessor.ts`) handles canvas-based adjustments but needs export-to-file functionality.

### Minor: Pre-existing Build Errors

`PartyPokemon.Status` and `ItemDefinition.ParsedEffect` remain undefined in the game project. Unrelated to model/animation work.

---

## 3. Optimizations — Prime Suspects for Animation Fix

### 3.1 Handle All Three BCH Animation Segment Types (HIGHEST PRIORITY)

The `exportAnimation()` function must check `bone.isFrameFormat` and `bone.isFullBakedFormat` and handle quaternion/matrix animation:

**For `transformQuaternion` (isFrameFormat=true):**
- `bone.rotationQuaternion.vector` — List of `OVector4` quaternion frames (x,y,z,w)
- `bone.translation.vector` — List of `OVector4` translation frames (x,y,z,0)
- `bone.scale.vector` — List of `OVector4` scale frames (x,y,z,0)
- Convert quaternion to rotation matrix, combine with scale and translation using `buildLocalMatrix`-style composition
- Map frame indices using `startFrame`/`endFrame` from the `OAnimationFrame`

**For `transformMatrix` (isFullBakedFormat=true):**
- `bone.transform` — List of `OMatrix` per frame, already local transforms
- Output directly via `daeMatrix.set()` format (16 floats per frame)
- BCH reads these as: M11,M21,M31,M41, M12,M22,M32,M42, M13,M23,M33,M43 (column-by-column)

### 3.2 Check and Handle the `isAxisAngle` Flag

In `GfMotion.cs`, `bone.isAxisAngle = flags >> 31 == 0`. If this flag is true for BCH `segmentType.transform` bones, the rotationX/Y/Z values may represent an **axis-angle rotation vector** (rx, ry, rz) where the axis is `normalize(rx,ry,rz)` and the angle is `length(rx,ry,rz)`. Building Euler rotation matrices from axis-angle values would produce completely wrong results.

**Fix:** Before building the matrix in `exportAnimation`, check `bone.isAxisAngle`. If true, convert the axis-angle (rx,ry,rz) to a rotation matrix using Rodrigues' formula instead of `Rx * Ry * Rz`.

### 3.3 Verify Rotation Convention with Ohana3DS RenderEngine

The original Ohana3DS GUI (`D:\Projects\Ohana3DS-Rebirth\Ohana3DS Rebirth\Ohana\RenderEngine.cs`) contains the actual rendering code that applies skeletal animation to models. Reading this code would definitively answer:
- What composition order BCH expects (SRT vs TRS)
- How `isAxisAngle` is handled during animation playback
- Whether animation values are absolute or relative to rest pose
- How quaternion animation frames are interpolated and applied

### 3.4 Add Diagnostic Comparison Mode

Add a `--diag-anim` flag to the convert command that prints per-bone diagnostic info:
- Segment type (transform/quaternion/matrix)
- `isAxisAngle` flag value
- Number of keyframes per channel
- Frame 0 values vs rest pose values
- Whether the bone was exported or skipped

This would immediately reveal which bones are skipped and why.

---

## 4. Step-by-Step Approach to Get Animation Fully Working

### Phase 1: Diagnose (30 min)
1. Add diagnostic output to `exportAnimation()` that prints for EACH bone in `anim.bone`:
   - `bone.name`, `bone.isFrameFormat`, `bone.isFullBakedFormat`, `bone.isAxisAngle`
   - Whether Euler keyframes exist (`rotationX/Y/Z.exists`, `translationX/Y/Z.exists`)
   - Whether quaternion data exists (`rotationQuaternion.exists`, `translation.exists`, `scale.exists`)
   - Whether matrix data exists (`transform.Count > 0`)
2. Run: `ohanacli convert sun-moon-dump/a-0-9-4.garc -o test-output -n 10 --diag`
3. Identify which segment types are actually used

### Phase 2: Handle All Segment Types (1-2 hrs)
4. Add quaternion-to-matrix conversion in `exportAnimation()`:
   ```csharp
   if (bone.isFrameFormat && bone.rotationQuaternion.exists) {
       // Build matrix from quaternion + translation + scale per frame
       // Output as stride-16 matrix animation
   }
   ```
5. Add baked matrix output:
   ```csharp
   if (bone.isFullBakedFormat && bone.transform.Count > 0) {
       // Output bone.transform[i] directly as stride-16
   }
   ```
6. Check `isAxisAngle` for Euler bones and convert axis-angle to rotation matrix if needed

### Phase 3: Validate in Blender (30 min)
7. Re-export: `ohanacli convert sun-moon-dump/a-0-9-4.garc -o test-output -n 10 -a 0`
8. Import `test-output/entry_1/model.dae` in Blender
9. Check timeline — animation should play smoothly without distortion
10. Verify all 55 bones have animation (check Blender's Dope Sheet)

### Phase 4: Read Ohana3DS RenderEngine (if Phase 2-3 don't fix it)
11. Read `D:\Projects\Ohana3DS-Rebirth\Ohana3DS Rebirth\Ohana\RenderEngine.cs`
12. Find the skeletal animation evaluation code
13. Match the exact transform composition used there

### Phase 5: Full Re-export
14. Once animation works in Blender, re-export all 957 Pokemon models with animation
15. Update BgEditor's `colladaAnimationParser.ts` to handle matrix animation (currently only handles per-component Euler)

---

## 5. How to Start/Test the System

### OhanaCli (Model Converter)
```bash
# Build
cd D:/Projects/PokemonGreen
dotnet build src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj

# Convert a single GARC with animation (first 10 entries)
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App -- \
  convert "src/PokemonGreen.Tests/sun-moon-dump/a-0-9-4.garc" \
  -o "src/PokemonGreen.Tests/ohanacli-output" -a 0 -n 10

# Batch convert all Pokemon
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App -- \
  batch "src/PokemonGreen.Tests/sun-moon-dump" \
  -o "src/PokemonGreen.Assets/Pokemon3D" -a 0
```

### BgEditor (3D Viewer + Texture Editor)
```bash
# Terminal 1: Backend API
cd src/PokemonGreen.BgEditor/backend
npm install
npx tsx src/index.ts
# Serves on http://localhost:3001, static assets at /assets/

# Terminal 2: Frontend
cd src/PokemonGreen.BgEditor/frontend
npm install
npm run dev
# Opens on http://localhost:5173
# Navigate to / for editor, /tools for texture tools
```

### Blender Animation Test
1. Open Blender (tested with 4.x)
2. File → Import → Collada (.dae)
3. Select `src/PokemonGreen.Tests/ohanacli-output/entry_1/model.dae`
4. Open Timeline panel (drag up from bottom)
5. Press Space to play — observe animation behavior

---

## 6. Known Issues & Resolution Strategies

### Issue 1: Animation Distortion in Blender
**Symptom:** Model flips, twists, and distorts during animation playback. Rest pose is correct.
**Strategy A:** Handle all BCH segment types (quaternion + baked matrix). See Section 3.1.
**Strategy B:** Check `isAxisAngle` flag and use Rodrigues' rotation formula. See Section 3.2.
**Strategy C:** Read Ohana3DS RenderEngine.cs to get the authoritative transform recipe. See Section 3.3.
**Strategy D:** Export a "null animation" where every frame uses the rest pose values. If this still distorts, the issue is in Blender's bone-space conversion, not our values. If it doesn't distort, the issue is in our animation values.

### Issue 2: Missing 15 Bones in Animation Export
**Symptom:** 40/55 bones have animation channels. 15 bones silently skipped.
**Strategy:** Add `isFrameFormat`/`isFullBakedFormat` handling. Some of the 15 may be genuinely un-animated (root bones, skin helper bones), while others may use quaternion format.

### Issue 3: Linear Interpolation for Non-Linear Keyframes
**Symptom:** `sampleKeyframes()` always uses linear interpolation between keyframes.
**Strategy:** BCH keyframes can be hermite (with in/out tangents) or step. Check `frame.interpolation` and implement hermite interpolation using the tangent values from `OAnimationKeyFrame`. For COLLADA output, this matters less since we bake to matrix values at each keyframe time and let Blender interpolate between them.

### Issue 4: BgEditor Animation Parser Incompatibility
**Symptom:** BgEditor's `colladaAnimationParser.ts` was written for per-component Euler animation. The new matrix animation format won't be parsed.
**Strategy:** Update the parser to detect `<param name="TRANSFORM" type="float4x4">` in animation sources, read stride-16 matrix values, and decompose each matrix to Euler+translation+scale for Three.js bone tracks. Alternatively, switch to Three.js's built-in `ColladaLoader` animation support if it handles matrix animation.

---

## 7. New Architecture & Features

### OhanaCli Convert Pipeline (New)
```
GARC Container
  ├── Entry 1: Model BCH (mesh + skeleton)
  ├── Entry 2: Animation BCH (skeletal anim, no mesh) ← MERGED into Entry 1
  ├── Entry 3: Texture BCH                            ← MERGED into Entry 1
  ├── Entry 4: High-res textures                      ← MERGED into Entry 1
  └── Entry 5+: Next Pokemon...

  Detection: model entries with 0 meshes = animation-only → merge into preceding model
```

### DAE Animation Structure (New)
```xml
<animation id="anim_BoneName_transform">
  <source id="..._input">   <!-- TIME: float[], one per keyframe -->
  <source id="..._output">  <!-- TRANSFORM: float4x4[], stride=16, one matrix per keyframe -->
  <source id="..._interp">  <!-- INTERPOLATION: Name[], "LINEAR" -->
  <sampler> INPUT + OUTPUT + INTERPOLATION </sampler>
  <channel target="BoneName_bone_id/transform" />
</animation>
```

### BgEditor Multi-Page Architecture (New)
```
App.tsx
  ├── BrowserRouter
  │   ├── Sidebar (persistent nav)
  │   ├── / → EditorPage (viewport + panels)
  │   └── /tools → ToolsPage (texture utilities)
  └── Zustand Store
      ├── manifest: Manifest | null  (NEW: tracks loaded model source)
      ├── processTexture()           (NEW: real-time canvas adjustment)
      └── resetTextureToOriginal()   (NEW: proper texture revert)
```

### Quick Wins
1. **Diagnostic flag for animation** — Add `--diag-anim` to print segment types, skip reasons, value comparisons. ~20 lines, immediately clarifies the distortion root cause.
2. **Null animation test** — Export rest-pose-as-animation to isolate whether the issue is values vs convention. ~10 lines in `exportAnimation()`.
3. **Quaternion bone count** — Add a one-line counter in `exportAnimation()`: `Console.WriteLine($"Skipped {skippedQuaternion} quaternion bones, {skippedBaked} baked bones")`. Immediately shows if missing bones are the problem.
4. **BgEditor export button** — The `ExportPanel.tsx` component exists but needs wiring. Connect it to download adjusted textures as a ZIP. ~30 lines using JSZip.

---

## 8. Key Files Reference

| File | Purpose |
|------|---------|
| `OhanaCli.Formats/Models/GenericFormats/DAE.cs` | COLLADA exporter — skeleton, animation, mesh, materials |
| `OhanaCli.Formats/Models/BCH/BCH.cs:600-820` | BCH animation loader — all 3 segment types |
| `OhanaCli.Formats/Core/RenderBase.cs:430-720` | OMatrix class — `[col,row]` indexing, reversed multiply |
| `OhanaCli.Formats/Core/RenderBase.cs:1892-1982` | OSkeletalAnimationBone — isAxisAngle, isFrameFormat, isFullBakedFormat |
| `OhanaCli.App/Program.cs:182-240` | GARC container processing with animation merging |
| `Ohana3DS-Rebirth/.../RenderEngine.cs` | Original rendering code (UNREAD — key reference for animation fix) |
| `blender/source/.../AnimationImporter.cpp:1240-1379` | Blender's `add_bone_animation_sampled` — the bone-space conversion formula |
| `blender/source/.../collada_internal.cpp:65-74` | `dae_matrix_to_mat4_` — transposes COLLADA to Blender format |

### OMatrix Convention (Critical Understanding)
- **Indexer:** `this[col, row]` — first index is column, second is row
- **Multiply:** `c[i,j] = sum(a[i,k] * b[k,j])` — with `[col,row]` indexing, `a * b` in code = `B * A` in standard math
- **Effect:** OMatrix is column-vector convention. `m *= Scale; m *= RotZ; m *= RotY; m *= RotX; m *= Translate` gives math: `T * Rx * Ry * Rz * S`
- **COLLADA output:** `daeMatrix.set()` writes `m[j,i]` (row-major of column-vector matrix) — correct COLLADA format, no transpose needed
- **Blender import:** `dae_matrix_to_mat4_` transposes our output to Blender's `mat[col][row]` format. Translation ends up in `mat[3]` as expected.
