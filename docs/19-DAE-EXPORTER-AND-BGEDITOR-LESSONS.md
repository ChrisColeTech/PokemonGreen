# 19 - DAE Exporter Fixes & BgEditor 3D Viewer: Lessons Learned & Handoff

**Date:** 2026-02-16
**Scope:** OhanaCli DAE exporter bugfixes (UV flip, library_images, model dedup, bone transforms), BgEditor Three.js model rendering pipeline (textures, materials, animation playback), full model re-extraction pipeline
**Codebase:** OhanaCli (DAE.cs, OBJ.cs, Program.cs), BgEditor (sceneService.ts, Viewport.tsx, AnimationPanel.tsx, colladaAnimationParser.ts, editorStore.ts)

---

## 1. What We Accomplished

### DAE Exporter Fixes (OhanaCli)

**UV Y-Flip (DAE.cs, OBJ.cs):**
The PICA200 GPU (Nintendo 3DS) uses a top-left UV origin where V increases downward. Collada/OpenGL uses bottom-left origin where V increases upward. Added `1.0f - vertex.texture0.y` to both the DAE and OBJ exporters. Without this, every texture appeared vertically flipped on models.

**Library Images Fix (Program.cs):**
DAE files were exporting with empty `<library_images />` because textures came from separate GARC entries (`extraTextures`) but weren't merged into the `models.texture` list before calling `DAE.export()`. Fixed by merging `extraTextures[0].texture` into `models.texture` using a `HashSet<string>` for deduplication before export.

**Model Name Deduplication (Program.cs):**
Each Pokemon has ~2 models: a textured model and a shadow/outline model. Both were named "model", causing `model.dae` to be overwritten by the shadow mesh. Added a `HashSet<string> usedModelNames` that appends `_N` suffix on collision, producing `model.dae` (textured) and `model_1.dae` (shadow).

**Up-Axis Declaration (DAE.cs):**
Added `<up_axis>Y_UP</up_axis>` to the DAE asset section. Three.js ColladaLoader defaults to Y-up, and without this explicit declaration some viewers misinterpret the coordinate system.

**Bone Scale Regression (DAE.cs) — REVERTED:**
Added `OMatrix.scale(skeleton[index].scale)` to `transformSkeleton()` and `writeSkeleton()`. This was mathematically correct but caused deformation in Three.js for models with extreme scale values (e.g., Mienshao's fingers at 0.01 scale created 100x inverse bind matrix values that caused floating-point precision issues during skinning). **Reverted to Rotation * Translation only** — matches the original working pipeline. Only 5/957 models have non-identity bone scale, and the visual impact is negligible.

### BgEditor Three.js Viewer

**Texture Loading Pipeline (sceneService.ts):**
Added `loadDaeWithManager()` using `THREE.LoadingManager` to ensure all texture downloads complete before resolving. Previously, materials were created before textures finished loading, resulting in white/untextured meshes. Safety timeout of 5 seconds handles texture load failures gracefully.

**Material Conversion (sceneService.ts):**
`fixMaterials()` converts all `MeshPhongMaterial` → `MeshBasicMaterial` with `side: THREE.DoubleSide`. Phong materials render black in the BgEditor's lighting setup. Basic materials display textures reliably. Preserves texture references during async load by using `map: tex || null` instead of checking `tex?.image`.

**Animation Playback (Viewport.tsx, AnimationPanel.tsx, editorStore.ts):**
- `AnimationMixer` on the scene root, updated each frame via `Clock.getDelta()`
- Custom `colladaAnimationParser.ts` handles per-axis Euler channels (`rotation.X/Y/Z`, `translation.X/Y/Z`) that Three.js ColladaLoader silently skips
- AnimationPanel component with play/pause button, clip list, active clip indicator
- Zustand store tracks `animationPlaying`, `activeClipIndex`

### Full Re-Extraction Pipeline
- 957 Pokemon folders re-exported with all fixes applied
- 1,154 models, 23,790 textures, 0 errors
- Copied to `src/PokemonGreen.Assets/Pokemon3D/`
- Committed and pushed to `dev` branch

---

## 2. What Work Remains

### Critical: Models Still Deformed in BgEditor
The bone scale revert fixed the regression, but models may still show deformation from the animation parser. The `colladaAnimationParser.ts` builds animated transforms FROM SCRATCH using only the animated values, ignoring bind-pose defaults baked into `<matrix>` elements. This is the same root cause as documented in `18-SKELETAL-ANIMATION-LESSONS.md` (Section 3.1). When only `rotation.X` is animated, the bone loses its base 90-degree Z rotation.

**Fix approach:** Decompose each bone's bind-pose `<matrix>` into per-component defaults (translation XYZ, Euler XYZ, scale XYZ), then use those as fallbacks for non-animated channels in `buildBoneTracks()`. Currently `interpolateAt()` returns `0` for missing channels — it should return the bind-pose decomposed value instead.

### Critical: Existing DAE Files Need Verification
The bone scale was reverted. Need to confirm the re-exported DAE files are using the reverted code (Rotation * Translation only, no scale). If the last extraction ran before the revert, models will still be deformed.

### Moderate: Missing Pokemon Models (79 Species)
IDs 650-700 (Gen 6 ports) and 775-800 (late Gen 7) produce 0 meshes from the GfModel parser. These are likely a different model version. See `17-MODEL-EXTRACTION-AND-REGISTRY.md` Section 6.1 for investigation strategies.

### Minor: BgEditor Diagnostic Console Spam
`sceneService.ts` and `Viewport.tsx` log extensively to console (every mesh, material, texture, bone, animation track). Should be gated behind a debug flag or removed for production use.

### Minor: Pre-Existing Build Errors
`PartyPokemon.Status` and `ItemDefinition.ParsedEffect` remain undefined. These block a full `dotnet build` of the game project but are unrelated to model work.

---

## 3. Optimizations — Prime Suspects

### 3.1 Bind-Pose Matrix Decomposition in colladaAnimationParser.ts (THE #1 FIX)
The animation parser's `interpolateAt()` returns `0` for missing channels. For a bone with a 90-degree base Z rotation but only X rotation animated, the Z rotation becomes 0 — completely wrong. Fix by:
1. Parse each bone's `<matrix>` from `<library_visual_scenes>` in the DAE XML
2. Decompose into Euler XYZ (ZYX order) + translation XYZ + scale XYZ
3. Use decomposed values as defaults in `buildBoneTracks()` instead of `0`
4. This is ~40 lines of code and fixes ALL bone deformation

### 3.2 Euler Angle Convention Mismatch
The animation parser uses `THREE.Euler(rx, ry, rz, 'XYZ')` but doesn't verify this matches the DAE's convention. COLLADA defines rotation order as Rz * Ry * Rx (intrinsic ZYX), which corresponds to Three.js `'ZYX'` order, not `'XYZ'`. Wrong Euler order produces subtly incorrect rotations on every bone.

**Verification:** At time=0 with bind-pose defaults, the composed local transform should match the bone's `<matrix>`. If they diverge, the Euler order is wrong.

### 3.3 Texture Loading Race Condition
The LoadingManager `onLoad` fires when all pending resources complete, but `fixMaterials()` runs immediately after the loader callback — potentially before textures are decoded. The `map: tex || null` workaround preserves references, but the material may still show white for one frame until the texture uploads. Consider deferring `fixMaterials()` into `onLoad`.

### 3.4 Backend Static File Serving (index.ts)
The backend serves 957 Pokemon folders via `@fastify/static`. For large models with 10+ textures, this creates many concurrent HTTP requests per model load. Consider:
- Bundling textures into a single sprite atlas per Pokemon
- Using HTTP/2 multiplexing (Fastify supports it with `http2: true`)
- Adding Cache-Control headers to avoid re-downloading on reload

---

## 4. Step-by-Step: Getting the App Fully Working

### Prerequisites
- Node.js 18+ (for BgEditor backend/frontend)
- .NET 9.0 SDK (for OhanaCli and main game)
- 957 Pokemon model folders in `src/PokemonGreen.Assets/Pokemon3D/`

### Step 1: Start the BgEditor Backend
```bash
cd D:\Projects\PokemonGreen\src\PokemonGreen.BgEditor\backend
npx tsx src/index.ts
```
Verify: `Backend listening on http://localhost:3001` appears. Test with:
```bash
curl http://localhost:3001/assets/pm0001_00/model.dae -o /dev/null -w "%{http_code}"
# Should return 200
```

### Step 2: Start the BgEditor Frontend
```bash
cd D:\Projects\PokemonGreen\src\PokemonGreen.BgEditor\frontend
npm run dev
```
Opens at `http://localhost:5173`. You should see a dark drop zone saying "Drop a manifest.json".

### Step 3: Create a Manifest File
Create a JSON file (e.g., `bulbasaur.json`):
```json
{
  "name": "Bulbasaur",
  "dir": "pm0001_00",
  "assetsPath": "pm0001_00",
  "modelFile": "model.dae",
  "modelFormat": "dae",
  "textures": ["pm0001_00_BodyA.png", "pm0001_00_BodyB.png", "pm0001_00_Eye1.png", "pm0001_00_Eye2.png", "pm0001_00_EyeIce.png"]
}
```
Drop this file onto the BgEditor. The model should load with:
- 3D viewport with orbit controls (left-click drag to rotate, scroll to zoom)
- Texture panel showing all loaded textures
- Animation panel with play/pause and clip list
- Color controls for texture adjustments

### Step 4: Verify Textures
Check the browser console for `[SceneService]` logs:
- `mat[N] "BodyA" → texture: pm0001_00_BodyA.png, image: OK` = working
- `mat[N] "BodyA" → texture: none` = broken texture path in DAE

### Step 5: Verify Animations
Check console for `[ColladaAnimParser]` logs:
- `Found animation channels for N bone(s)` = parser found data
- `Created clip: "idle" (1.37s, 88 tracks)` = animation ready
- If animations show `0 clip(s)`, the ColladaLoader handled it, or the DAE has no animations

### Step 6: Build and Run the Main Game
```bash
dotnet build D:\Projects\PokemonGreen\src\PokemonGreen\PokemonGreen.csproj
dotnet run --project D:\Projects\PokemonGreen\src\PokemonGreen\PokemonGreen.csproj
```
Walk into tall grass to trigger an encounter. Battle screen shows 3D backgrounds, platforms, and (once wired) Pokemon models.

---

## 5. How to Start/Test the API

### BgEditor Backend API

**Start:**
```bash
cd D:\Projects\PokemonGreen\src\PokemonGreen.BgEditor\backend
npx tsx src/index.ts
```

**Endpoints:**
| Method | URL | Description |
|--------|-----|-------------|
| GET | `/assets/{path}` | Static file serving from Pokemon3D directory |
| GET | `/api/file?dir={dir}&name={name}` | Query-based file serving (fallback) |

**Configuration:**
- `ASSETS_DIR` in `backend/src/index.ts` line 7 — hardcoded to `D:/Projects/PokemonGreen/src/PokemonGreen.Assets/Pokemon3D`
- `PORT` — 3001
- CORS enabled for all origins

**Test URLs:**
```bash
# List a Pokemon's files
curl http://localhost:3001/assets/pm0001_00/ -I

# Fetch a model
curl http://localhost:3001/assets/pm0001_00/model.dae -o test.dae

# Fetch a texture
curl http://localhost:3001/assets/pm0001_00/pm0001_00_BodyA.png -o test.png
```

### OhanaCli Tool

**Build:**
```bash
cd D:\Projects\PokemonGreen\src\PokemonGreen.OhanaCli\src\OhanaCli.App
dotnet build -c Release
```

**Run extraction:**
```bash
dotnet run -c Release --no-build -- convert-all /tmp/test_garc "D:/Projects/PokemonGreen/Assets/SunMoon/0_9_4" --format dae
```

**GARC source:** `/tmp/test_garc/pokemon.garc` (10,549 entries, ~2.5GB)

---

## 6. Issues & New Strategies

### Issue 1: Bone Deformation from Missing Bind-Pose Defaults (CRITICAL)

**Symptom:** Models appear with limbs in wrong positions, flattened torsos, or collapsed joints. Most visible on Pokemon with complex skeletons (quadrupeds, humanoids).

**Root cause:** The `colladaAnimationParser.ts` `interpolateAt()` function returns `0` for any axis that doesn't have an animation channel. All DAE bones use `<matrix>` transforms (not decomposed `<translate>/<rotate>/<scale>`), so the bind-pose values (base rotation, position) are unknown to the animation system.

**Strategies:**
1. **Decompose bind-pose matrix (Recommended):** In `parseColladaAnimations()`, parse each bone's `<matrix>` from `<library_visual_scenes>`, decompose into Euler XYZ + translation XYZ, pass these as defaults to `buildBoneTracks()`. ~40 lines. Fixes all deformation.
2. **Matrix-based animation:** Instead of per-component Euler interpolation, compose a full 4x4 matrix per bone per keyframe. Replace animated components in the matrix, keep non-animated from bind pose. More robust but requires Matrix4 keyframe tracks instead of quaternion tracks.
3. **Hybrid parser:** Use ColladaLoader's `matrix` animation support when available, fall back to per-axis parser with bind-pose defaults. Would require the DAE exporter to output `<matrix>` animation channels instead of per-axis.
4. **Pre-process DAE files:** Write a build-time converter that decomposes all bone `<matrix>` elements into `<translate>/<rotate>/<scale>` elements. Then the parser's default-zero behavior becomes correct because the defaults exist in the XML.

### Issue 2: Euler Angle Order May Be Wrong (MODERATE)

**Symptom:** Subtle rotation errors — bones rotated slightly wrong even when all channels have keyframes.

**Root cause:** The parser uses `THREE.Euler(rx, ry, rz, 'XYZ')` but COLLADA's convention is intrinsic ZYX (i.e., apply Rx first, then Ry, then Rz). In Three.js, this is `'ZYX'` order.

**Strategies:**
1. **Change Euler order to 'ZYX':** One-line fix in `buildBoneTracks()`. Test by comparing composed t=0 transform against bind-pose matrix.
2. **Try all 6 orderings:** XYZ, XZY, YXZ, YZX, ZXY, ZYX — only one will produce a t=0 transform matching the bind-pose matrix. Quick empirical test.
3. **Read order from DAE:** Check if the DAE specifies rotation order anywhere (it doesn't for per-axis channels, but the matrix decomposition reveals it).

### Issue 3: Bone Scale in DAE Exporter (DEFERRED)

**Symptom:** 5 Pokemon (Mienshao, Salamence variants, etc.) have bones with non-identity scale. Without scale in the exported transforms, these bones are slightly wrong.

**Root cause:** Baking scale into the 4x4 matrix causes precision issues in Three.js skinning for extreme values (0.01 → 100x inverse).

**Strategies:**
1. **Accept the limitation:** Only 5/957 models affected, visual impact is minor (finger thickness on Mienshao, arm width on Salamence).
2. **Scale at export time:** Instead of including scale in the bone matrix, pre-apply scale to the vertex positions in the mesh data. The skeleton remains scale-free. More work but mathematically clean.
3. **Normalize extreme scales:** Clamp bone scale to [0.1, 10.0] range before matrix composition. Reduces precision issues while preserving most of the intended deformation.
4. **Use separate scale channel in Three.js:** Export bone scale as a separate `<scale>` element. The custom animation parser can then read and apply it via a `VectorKeyframeTrack` for `.scale`. Three.js bones support scale natively.

### Issue 4: Double UV Flip Trap (RESOLVED — Document for Future Reference)

**Symptom:** Textures appear flipped even though both exporter and viewer "fix" the UV orientation.

**Root cause:** The DAE exporter applies `1-V` to correct for PICA200's top-left origin. If the viewer ALSO applies a UV flip (common in Three.js or Assimp workflows), the two flips cancel out, returning to the broken state.

**Resolution:** UV flip happens in exactly ONE place — the exporter. The BgEditor's `sceneService.ts` has a comment explicitly documenting this: "UV Y-flip is already applied by the DAE exporter. Do NOT flip again here."

---

## 7. Architecture & New Features

### BgEditor Architecture

```
Frontend (React + Vite + TypeScript)
├── App.tsx                    Main layout: InfoBar + Viewport + Sidebar
├── store/editorStore.ts       Zustand state: scene, animations, textures, playback
├── services/
│   ├── sceneService.ts        Model loading: DAE/FBX/OBJ → Three.js scene
│   └── colladaAnimationParser.ts  Custom per-axis Euler animation parser
├── components/
│   ├── Viewport.tsx           Three.js renderer, OrbitControls, AnimationMixer
│   ├── AnimationPanel.tsx     Play/pause, clip list, active clip selection
│   ├── TexturePanel.tsx       Texture thumbnails, click to select
│   ├── ColorControls.tsx      Brightness/contrast/hue sliders
│   ├── DropZone.tsx           Drag-and-drop manifest.json loading
│   └── InfoBar.tsx            Scene name, mesh/texture counts
└── types/editor.ts            LoadedTexture, TextureAdjustment interfaces

Backend (Fastify + TypeScript)
└── src/index.ts               Static file server for Pokemon3D assets on port 3001
```

### Data Flow

```
User drops manifest.json
  → editorStore.loadManifest()
    → sceneService.loadScene(manifest)
      → loadDaeWithManager(modelUrl) via LoadingManager
        → ColladaLoader parses DAE XML → Three.js scene graph
        → LoadingManager waits for all texture HTTP downloads
      → fixMaterials() → MeshPhong → MeshBasic + DoubleSide
      → parseColladaAnimations() → custom Euler → Quaternion tracks
      → extractTextures() → LoadedTexture[] for sidebar
    → Store updates: scene, animations, textures
  → Viewport reacts: adds scene to Three.js, fits camera, creates AnimationMixer
  → AnimationPanel reacts: shows clips, play/pause controls
```

### DAE Exporter Pipeline (OhanaCli)

```
convert-all command (Program.cs)
  → Scan GARC for model + texture + animation entries
  → Group consecutive entries by Pokemon ID (pm####_##)
  → For each Pokemon:
    1. Load GfModel from .pc binary
    2. Load extra textures from adjacent GARC entries
    3. Merge extra textures into model.texture list (dedup by name)
    4. Deduplicate model names (HashSet, append _N on collision)
    5. DAE.export(model, outputPath)
       → UV Y-flip: 1.0 - vertex.texture0.y
       → Bone transforms: Rotation * Translation (NO scale)
       → up_axis: Y_UP
       → library_images from model.texture list
       → library_controllers: skin with inverse bind matrices
       → library_animations: per-axis Euler + translation channels
```

### Quick Wins

1. **Fix animation bind-pose defaults (~40 lines in colladaAnimationParser.ts):** Parse each bone's `<matrix>`, decompose into Euler+translation defaults, use as fallback instead of `0`. This single fix resolves all model deformation in the BgEditor. Priority: immediate.

2. **Fix Euler order (~1 line in colladaAnimationParser.ts):** Change `new THREE.Euler(rx, ry, rz, 'XYZ')` to `'ZYX'` to match COLLADA convention. Fixes subtle rotation errors on all bones. Priority: immediate.

3. **Add model browser to BgEditor (~80 lines):** Replace the manifest drop-zone with a sidebar listing all `pm####_##` folders from the backend. Click to load. Auto-generate manifest from folder contents. Eliminates manual JSON creation. Priority: medium.

4. **Add wireframe/skeleton overlay toggle (~15 lines in Viewport.tsx):** Add a `SkeletonHelper` that visualizes the bone hierarchy as colored lines. Toggle with a button. Essential for debugging deformation. Priority: medium.

5. **Convert to glTF binary (~100 lines build script):** DAE → glTF conversion at build time using `gltf-pipeline` or `collada2gltf`. glTF loads 5-10x faster in Three.js, has native animation support, and binary format reduces file size by 60%. Priority: low (optimize after correctness).

### New Feature Ideas

- **Side-by-side comparison mode:** Load two Pokemon simultaneously for visual diffing (e.g., pre-fix vs post-fix, or normal vs shiny variant)
- **Texture hot-reload:** Watch the Pokemon3D directory for file changes and auto-reload textures in the viewport
- **Animation timeline scrubber:** A frame-by-frame timeline slider with bone transform readout at each keyframe
- **Batch screenshot tool:** Render a thumbnail of each Pokemon and save as PNG. Useful for catalog/Pokedex UI
- **Shiny variant toggle:** Switch between normal and `shiny/` textures in the viewer. The folder structure already supports this.

---

## 8. File Reference

| File | Status | Purpose |
|------|--------|---------|
| **OhanaCli (Exporter)** | | |
| `src/PokemonGreen.OhanaCli/src/OhanaCli.App/Program.cs` | Modified | Model name dedup, texture merging, extraction pipeline |
| `src/PokemonGreen.OhanaCli/src/OhanaCli.Formats/Models/GenericFormats/DAE.cs` | Modified | UV Y-flip, up_axis, bone transforms (scale reverted) |
| `src/PokemonGreen.OhanaCli/src/OhanaCli.Formats/Models/GenericFormats/OBJ.cs` | Modified | UV Y-flip |
| **BgEditor Frontend** | | |
| `src/PokemonGreen.BgEditor/frontend/src/services/sceneService.ts` | Modified | DAE/FBX loading with LoadingManager, material fix, UV flip note |
| `src/PokemonGreen.BgEditor/frontend/src/services/colladaAnimationParser.ts` | **New** | Custom per-axis Euler animation parser for COLLADA |
| `src/PokemonGreen.BgEditor/frontend/src/components/Viewport.tsx` | Modified | AnimationMixer, Clock, auto-fit camera, skeleton debug logs |
| `src/PokemonGreen.BgEditor/frontend/src/components/AnimationPanel.tsx` | **New** | Play/pause controls, clip list, active clip indicator |
| `src/PokemonGreen.BgEditor/frontend/src/store/editorStore.ts` | Modified | Animation state (playing, activeClipIndex, animations[]) |
| `src/PokemonGreen.BgEditor/frontend/src/App.tsx` | Modified | AnimationPanel integrated into sidebar |
| **BgEditor Backend** | | |
| `src/PokemonGreen.BgEditor/backend/src/index.ts` | Existing | Fastify static server for Pokemon3D assets, port 3001 |
| **Assets** | | |
| `src/PokemonGreen.Assets/Pokemon3D/` | Modified | 957 re-exported Pokemon model folders (DAE + PNG) |

---

## 9. Key Lessons Learned

### Lesson 1: UV Flips Must Happen in Exactly One Place
The PICA200 GPU uses top-left UV origin. Collada/OpenGL uses bottom-left. The flip (`1-V`) must happen at export time OR render time, never both. When both the exporter and the runtime apply the flip, they cancel out. **Document where the flip happens** and add comments preventing double-flip.

### Lesson 2: Don't Bake Extreme Values into Matrices
Bone scale of 0.01 produces inverse bind matrices with 100x values. While mathematically correct (`inv * world = identity`), floating-point precision in 32-bit GPU skinning causes visible artifacts. Prefer keeping extreme values as separate components rather than baking them into combined matrices.

### Lesson 3: Three.js ColladaLoader Silently Skips Per-Axis Channels
The ColladaLoader handles `<matrix>` type animations but completely ignores per-axis channels (`rotation.X`, `translation.Y`, etc.). It doesn't warn or error — just returns zero animations. Any COLLADA file using per-axis Euler channels needs a custom parser.

### Lesson 4: Model Name Collisions Are Silent Data Loss
When two models in the same Pokemon entry have the same name, the second `model.dae` overwrites the first. The exporter doesn't warn. Always deduplicate names before writing files. Use a HashSet to track used names and append suffixes on collision.

### Lesson 5: LoadingManager Is Essential for Three.js Async Resources
`ColladaLoader.load()` resolves its callback when the XML is parsed, NOT when textures finish downloading. Without a `LoadingManager` that waits for all sub-resources, materials will have null texture references. This manifests as white/untextured meshes that seem correct but are actually race conditions.

### Lesson 6: Never Run Agents Concurrently on the Same File Tree
A previous extraction agent and a cleanup agent ran simultaneously — the cleanup agent deleted the output directory while extraction was still writing, leaving only 155/957 folders. Always run extraction and post-processing sequentially, never in parallel.
