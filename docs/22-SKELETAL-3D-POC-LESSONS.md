# 22 - Skeletal 3D POC Lessons & Handoff

## Summary

This document covers the skeletal animation work in `PokemonGreen.3D`, the proof-of-concept for rendering overworld characters with idle/walk/run animations, per-mesh textures, and a character select overlay. It captures what was accomplished, what remains broken, optimization targets, and new strategies for solving the outstanding face rendering issue.

---

## 1. What We Accomplished

### Overworld Character Pipeline
- **Exported overworld characters** from Sun/Moon GARC `a/2/0/0` using OhanaCli and Spica
  - 18 characters (tr0001_00_fi through tr0018_00_fi) with 16 animation clips each
  - Clip slots: Motion_0/anim_0 = idle, Motion_1/anim_1 = walk, Motion_2/anim_2 = run
  - Assets stored in `PokemonGreen.Assets/Pokemon3D/characters/overworld/`

### Skeletal Animation System (6 files in `Core/Rendering/Skeletal/`)
- **`SkinnedDaeModel.cs`** — CPU-skinned COLLADA model renderer
  - Per-mesh texture support via full COLLADA material chain parsing (triangles → bind_material → material → effect → surface → image → file)
  - Forced alpha=255 on texture load (3DS uses alpha test, not blend)
  - Face mesh depth ordering: Eye/Mouth batches render last with `CompareFunction.LessEqual`
  - Debug logging to `skinned_dae_log.txt` in app base directory
- **`ColladaSkeletalLoader.cs`** — Skeleton and animation clip loader from DAE files
  - Matrix transpose fix: COLLADA row-major column-vector → XNA row-vector convention
- **`SplitModelAnimationSet.cs`** — Manifest-driven model+clip loader
  - Supports both OhanaCli (PascalCase, per-model clips) and Spica (camelCase, flat clips) formats
  - Case-insensitive JSON deserialization with fallback property resolution
- **`SkeletalAnimator.cs`** — Animation playback with bind→local→world→skin pose chain
- **`SkeletonRig.cs`** — Bone hierarchy with dual name lookup
- **`SkeletalAnimationClip.cs`** — Keyframe tracks with interpolation

### 3D POC Features (`PokemonGreen.3D/Game1.cs`)
- Third-person camera with follow delay, pitch/yaw, zoom
- WASD movement (camera-relative), shift to run, space to jump
- Automatic animation clip switching (idle ↔ walk ↔ run)
- Character select overlay (Tab/Escape) — 3x2 grid with keyboard + mouse navigation
- Resizable window with dynamic viewport aspect ratio
- Reference grid floor with colored axis lines
- Removed stale local Content folder; models now resolve from Assets lib

### Key Bug Fixes
| Bug | Root Cause | Fix |
|-----|-----------|-----|
| Model distorted | COLLADA matrices not transposed for XNA | Added `Matrix.Transpose()` in `ParseMatrix()` |
| Model 2x giant | `ComputeSkinMatrix` started from `Matrix.Identity` | Changed to `default` (zero matrix) |
| Single texture on all meshes | No per-mesh material resolution | Added full COLLADA material chain parsing |
| Lower body invisible | Texture alpha < 255 with `BlendState.AlphaBlend` | Force all pixel alpha to 255 on load |
| Arm/body clipping | No depth buffer testing | Set `DepthStencilState.Default` before model draw |
| Dark lower body tint | `CullNone` renders back-faces with inverted lighting | Increased ambient light (0.6) to reduce contrast |
| Manifest format mismatch | Spica uses camelCase + flat clips, OhanaCli uses PascalCase + nested | Case-insensitive JSON + fallback property logic |

---

## 2. What Work Remains

### Critical: Face Not Rendering on _fi Characters
- The Spica-exported `tr0001_00` renders faces correctly
- OhanaCli-exported `_fi` characters (tr0002–tr0018) do NOT show face meshes
- The debug log confirms textures load and mesh vertex counts are healthy
- Current face depth ordering fix (Eye/Mouth rendered last with `LessEqual`) did not resolve it
- **This is the #1 blocker** — see Section 6 for new strategies

### Remaining Tasks
- Walk → run animation transition feels instant; needs crossfade/blend between clips
- No animation for jump (currently uses idle pose while airborne)
- Character select only shows 6 of 18 available characters
- No shadow casting or ambient occlusion
- No collision with environment (player walks through everything)
- VertexBuffer/IndexBuffer recreated every frame — needs optimization (see Section 3)

---

## 3. Optimizations — Prime Suspects

### 3.1 Buffer Allocation Every Frame
**Impact: High** — `RebuildBuffers()` creates new `VertexBuffer` and `IndexBuffer` every frame via `UpdatePose()`. Old buffers are never disposed, causing GC pressure and GPU memory churn.

**Fix:** Allocate `DynamicVertexBuffer` once during `Load()` with `BufferUsage.None`. In `UpdatePose()`, call `SetData()` on the existing buffer. Only reallocate if vertex count changes (it won't for the same model).

### 3.2 Per-Vertex Matrix Multiply in ComputeSkinMatrix
**Impact: Medium** — Each vertex does up to 4 matrix lookups and weighted additions. For ~2000+ vertices at 60fps, this is ~480K matrix operations/second.

**Fix:** Pre-compute the final skin matrices (inverseBindPose × worldPose) once per bone per frame (max 52 bones). Then each vertex just does the weighted blend. Currently the skin matrices are already pre-computed in `SkeletalAnimator.SkinPose`, so this is already optimized — but verify no redundant work exists.

### 3.3 Texture Loading on Character Switch
**Impact: Medium** — `LoadCharacterModel()` loads all textures from disk and forces alpha=255 via `GetData`/`SetData` pixel manipulation every time a character is selected.

**Fix:** Implement a texture cache keyed by file path. Cache textures across character switches since many characters share rim textures (`Chara_Rim_1_fi.png`, `Chara_Rim_Black_fi.png`).

### 3.4 Animation Clip Loading
**Impact: Low-Medium** — All 16 clips are loaded eagerly from DAE files on character switch. Most clips are rarely used (emotes, sitting, etc.).

**Fix:** Lazy-load clips on first play. The `SplitModelAnimationSet` already stores clip paths — defer actual DAE parsing until `SkeletalAnimator.Play()` requests a clip.

---

## 4. Step-by-Step: Getting the App Fully Working

### Prerequisites
- .NET 9.0 SDK
- MonoGame 3.8 (pulled automatically via NuGet)
- Character assets in `src/PokemonGreen.Assets/Pokemon3D/characters/overworld/`

### Build & Run
```bash
cd D:\Projects\PokemonGreen
dotnet build src/PokemonGreen.3D/PokemonGreen.3D.csproj
dotnet run --project src/PokemonGreen.3D/PokemonGreen.3D.csproj
```

### Controls
| Key | Action |
|-----|--------|
| WASD | Move character (camera-relative) |
| Mouse | Look around (pitch/yaw) |
| Scroll wheel | Zoom in/out |
| Shift (hold) | Run |
| Space | Jump |
| Tab / Escape | Open character select |
| Arrow keys / Mouse | Navigate character select |
| Enter / Z | Confirm selection |
| Escape / X | Cancel selection |

### Verify Working
1. App launches with default character on a grid floor
2. WASD moves character with idle → walk animation transition
3. Hold Shift while moving for run animation
4. Tab opens character select overlay; pick a character
5. Check `bin/Debug/net9.0/skinned_dae_log.txt` for texture loading diagnostics

---

## 5. How to Start/Test

### Running the 3D POC
```bash
dotnet run --project src/PokemonGreen.3D/PokemonGreen.3D.csproj
```

### Debug Texture Loading
Check `skinned_dae_log.txt` in the output directory after running. It logs every mesh with:
- Geometry ID, vertex count, triangle count
- Material symbol → material ID → image file → resolved path → exists?

Example output:
```
[SkinnedDae] Loading D:\...\tr0006_00_fi\tr0006_00_fi.dae
[SkinnedDae] Geometries: 5, Skins: 5, Materials: 4, Symbols: 4
[SkinnedDae] mesh_2_Eye_id: verts=360 tris=120 mat='Eye' matId=Eye_mat_id img=./tr0006_00_Eye.png path=D:\...\tr0006_00_Eye.png exists=True
```

### Running the 2D Game (main project)
```bash
dotnet run --project src/PokemonGreen/PokemonGreen.csproj
```

---

## 6. Known Issues & New Strategies for Face Rendering

### The Problem
Face meshes (Eye, Mouth) load correctly — textures found, vertex counts healthy, material chains resolve — but the face is visually absent on OhanaCli-exported `_fi` characters. The Spica-exported `tr0001_00` works fine.

### What We Already Tried
1. **Alpha blending** (`BlendState.AlphaBlend`) — Helped face on tr0001_00 but caused lower body to vanish
2. **Forced alpha=255** — Fixed lower body; face still missing on _fi models
3. **CullNone** — Both face sides render but doesn't fix visibility
4. **CullCounterClockwise** — Made face AND upper body vanish on _fi models (winding mismatch)
5. **Face depth ordering** (Eye/Mouth last with `LessEqual`) — No change

### Strategy 1: Compare Vertex Positions After Skinning
The face mesh vertices might collapse to a single point or move inside the head after the skin transform. Add debug logging in `RebuildBuffers()` to dump the bounding box of face mesh vertices after skinning:
```csharp
if (mesh.IsFace)
{
    Vector3 min = new(float.MaxValue), max = new(float.MinValue);
    // compute bounds of transformed face verts
    File.AppendAllText(logPath, $"Face bounds: {min} to {max}\n");
}
```
If the bounds are degenerate (zero volume or very small), the bone weights or bind pose transforms are wrong for the _fi export format.

### Strategy 2: Compare DAE Structure Between Working and Broken
The Spica export (`tr0001_00`) and OhanaCli export (`tr0001_00_fi`) are the SAME character from different exporters. Direct comparison would reveal structural differences:
- **Bind shape matrix** (`<bind_shape_matrix>`) — Does one have an identity and the other a transform?
- **Joint ordering** — Are bone names/IDs different between exporters?
- **Inverse bind matrices** — Do they match? Transposition applied consistently?
- **Vertex weight mapping** — Same control point count? Same bone assignments?

A script to dump and diff key matrices from both DAEs would pinpoint the discrepancy.

### Strategy 3: Render Face Mesh Without Skinning
Bypass the skeleton entirely for face meshes to test if it's a skinning issue:
```csharp
// In RebuildBuffers, for face meshes:
if (mesh.IsFace)
{
    // Use raw bind-pose positions (no skin transform)
    allVertices.Add(new VertexPositionNormalTexture(src.Position, src.Normal, src.Uv));
}
```
If the face appears (even in the wrong pose), the problem is confirmed as skin matrix computation specific to _fi bone data.

### Strategy 4: Apply Bind Shape Matrix
COLLADA `<skin>` elements can have a `<bind_shape_matrix>` that transforms vertices before skinning. The current `SkinnedDaeModel.cs` does NOT parse or apply this matrix. If the _fi models have a non-identity bind shape matrix, all skinned vertices would be in the wrong space.

Check for:
```xml
<skin source="#mesh_2_Eye_id">
  <bind_shape_matrix>1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</bind_shape_matrix>
```
If it's not identity, it must be applied to each vertex position BEFORE the bone weights:
```csharp
Vector3 pos = Vector3.Transform(src.Position, bindShapeMatrix);
pos = Vector3.Transform(pos, skinMatrix);
```
**This is the highest-probability fix** — Spica might bake the bind shape into vertex positions during export, while OhanaCli preserves the raw COLLADA data.

---

## 7. Architecture & New Features

### Current Architecture
```
PokemonGreen.3D/
  Game1.cs              — Main loop, input, camera, rendering

PokemonGreen.Core/Rendering/Skeletal/
  SkinnedDaeModel.cs    — COLLADA loader + CPU skinning + per-mesh rendering
  ColladaSkeletalLoader.cs  — Skeleton & clip DAE parser
  SplitModelAnimationSet.cs — Manifest-driven model+clip loader
  SkeletalAnimator.cs   — Clip playback, pose evaluation
  SkeletonRig.cs        — Bone hierarchy
  SkeletalAnimationClip.cs  — Keyframe data

PokemonGreen.Core/UI/Screens/
  CharacterSelectScreen.cs  — 3x2 grid character picker

PokemonGreen.Assets/Pokemon3D/characters/overworld/
  tr0001_00/            — Spica export (working reference)
  tr0001_00_fi/         — OhanaCli export
  tr0002_00_fi/ ... tr0018_00_fi/  — 17 OhanaCli characters
```

### Quick Wins
1. **Texture cache** — Cache loaded Texture2D by file path; instant character switching for shared textures
2. **DynamicVertexBuffer** — Allocate once, `SetData()` each frame instead of recreating
3. **Expand character select** — Show all 18 characters with scrollable grid or pagination
4. **Animation crossfade** — Blend between clips over 0.15s for smooth transitions (SkeletalAnimator already has pose data; interpolate between two poses)
5. **Dispose old model** — `SkinnedDaeModel` needs `IDisposable` to release GPU buffers on character switch

### Future Architecture Considerations
- **GPU skinning** — Move bone transforms to a vertex shader constant buffer (max 52 bones × 4x4 = 832 floats, well within limits). Eliminates per-frame CPU vertex transform and buffer upload.
- **Shared skeleton pool** — Overworld characters share similar bone structures. A shared rig could reduce clip loading overhead.
- **Environment rendering** — Map tiles as 3D geometry or 2.5D billboards around the character for a proper overworld scene.
- **Instanced NPCs** — Multiple characters on screen sharing the same vertex data but with different bone transforms.
