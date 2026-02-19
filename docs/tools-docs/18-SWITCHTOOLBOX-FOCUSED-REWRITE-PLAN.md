# Switch-Toolbox Focused Rewrite Plan — DAE Export Pipeline

**Date**: 2026-02-18
**Status**: Planning — Awaiting Review
**Goal**: Build a minimal, headless .NET 8 CLI that opens Trinity-format game data (TRPAK archives containing TRMDL/TRMSH/TRMBF/TRSKL/TRMTR/TRANM) and exports models using the **split model + animation clip** pattern established in docs 08 (OhanaCli) and 09 (Spica).

> [!CAUTION]
> **This replaces all previous rewrite plans (docs 10, 11, 15, 16).** Those attempts failed because they built bottom-up from archives before getting any export working. This plan takes the opposite approach: **decode and export first, archive plumbing later.**

---

## 1. Why Previous Rewrites Failed

| Attempt | What Went Wrong |
|---------|-----------------|
| Doc 10 (V1) | Planned 8 phases of infrastructure before reaching export. Never produced a DAE. |
| Doc 11 (V2) | Same phased approach. Built GARC/NCSD/NCCH/RomFS/TRPAK/TRPFS parsers, 78 tests — **0 models exported**. |
| Doc 15 (Lessons) | Diagnosed: Trinity mesh decoder produces 0 vertices, animation decoder unimplemented, all 4,999 bundles stuck at `pending_conversion`. |
| Doc 16 (Parallel) | Added parallel extraction, Oodle fixes. Still **0 DAE** outputs. 178,109 raw `.bin` files with nothing usable. |

**Root cause**: Building bottom-up from archives instead of top-down from the export format. The team ported containers without porting the decode logic for the data they contain.

---

## 2. Split Model + Animation Clip Pattern

> Establishes parity with the split export patterns from [doc 08 (OhanaCli)](file:///D:/Projects/PokemonGreen/docs/tools-docs/08-OHANACLI-SPLIT-MODEL-ANIMATION-PLAN.md) and [doc 09 (Spica)](file:///D:/Projects/PokemonGreen/docs/tools-docs/09-SPICA-SPLIT-MODEL-ANIMATION-PLAN.md).

### 2.1 Problem

Consolidated DAE (model + all clips in one file) causes warping/skew in Blender. Per-clip DAE with duplicated mesh is wasteful. The game runtime needs **one model asset loaded once + animation clips swapped at runtime**.

### 2.2 Asset Split

| Asset | Contains | What's Excluded |
|-------|----------|-----------------|
| **Model DAE** | Meshes, skeleton, skin controllers, materials, texture references | No `<library_animations>` |
| **Clip DAE** (N files) | Skeleton hierarchy + `<library_animations>` with bone channels | No `<library_geometries>`, `<library_controllers>`, `<library_images>`, `<library_materials>`, `<library_effects>` |

### 2.3 DAE Export Modes

| Mode | Contains | Bone Format | Use Case |
|------|----------|-------------|----------|
| **Model-only** | Mesh + skeleton + skin + materials + textures | Matrix (bind pose) | Shared model asset |
| **Clip-only** | Skeleton + animation channels only | Matrix (animated per frame) | Per-animation clip |

### 2.4 Skeleton Consistency (Critical)

Both model and clip DAEs **must use identical bone node IDs and SIDs** so clips can bind to the model:

- Node: `id="{BoneName}_bone_id"`, `sid="{BoneName}"`, `type="JOINT"`
- Animation channel target: `{BoneName}_bone_id/transform`
- Both use matrix bone format for consistent binding

> [!IMPORTANT]
> Blender's COLLADA importer does NOT auto-bind clip animations to an existing armature. Importing a clip DAE creates a **second, disconnected armature** with its own Action. To use the clip on the original model, transfer the Action to the original armature and delete the extra one.

### 2.5 Output Folder Structure

```
<asset-name>/
  model.dae                       # Static model: mesh + skeleton + skin + materials + texture refs
  textures/
    <texture-name>.png            # PNG textures referenced by model.dae
  clips/
    clip_000.dae                  # Clip-only: skeleton + animation channels, NO geometry
    clip_001.dae
    ...
  manifest.json
```

### 2.6 Manifest Schema (v1)

```json
{
  "version": 1,
  "assetId": "pm0025_00",
  "model": {
    "file": "model.dae",
    "meshCount": 4,
    "boneCount": 72
  },
  "textures": [
    { "name": "pm0025_body", "file": "textures/pm0025_body.png", "width": 512, "height": 512 }
  ],
  "clips": [
    {
      "index": 0,
      "id": "clip_000",
      "sourceName": "wait_loop",
      "semanticName": "Idle",
      "file": "clips/clip_000.dae",
      "frameCount": 42,
      "fps": 30
    }
  ]
}
```

### 2.7 MonoGame Runtime Wiring

The engine does not auto-bind separate clips to a model. Runtime flow:

1. Load `model.dae` → build skeleton, mesh, skin weights, textures
2. Load each `clips/clip_NNN.dae` → extract keyframes per bone per frame
3. Map clip bone tracks to model bones **by name**
4. Store clips: `clips["Idle"]`, `clips["Walk"]`, `clips["Attack"]`
5. At runtime: sample active clip at time `t` → write local transforms → walk hierarchy → compute skin matrices

---

## 3. Actual File Formats in Our Dumps

Our game dumps use **Trinity engine formats** — NOT GFBMDL/GFBANIM (those are Sword/Shield era).

| Format | Role | Reference Decode |
|--------|------|------------------|
| **TRPAK** | Archive container (like a zip) | `GFPakSerializer.Deserialize()` |
| **TRMDL** | Model root (references mesh, material, skeleton by path) | `Model.Model()` constructor |
| **TRMSH** | Mesh declaration (vertex attributes, submesh parts) | `Model.ParseMesh()` |
| **TRMBF** | Mesh buffer (raw vertex + index data) | `Model.ParseMeshBuffer()` |
| **TRSKL** | Skeleton (bone hierarchy, transforms) | `Armature.Armature()` |
| **TRMTR** | Material (shader params, texture refs) | `Model.ParseMaterial()` |
| **BNTX** | Texture container (Nintendo binary textures) | Switch-Toolbox `BNTX.cs` |
| **TRANM** | Animation (bone keyframes, Catmull-Rom/Squad interp) | `Animation.Animation()` |

### File Resolution Pattern

Trinity uses **path-based** file references within the TRPAK:
- TRMDL → mesh path (e.g., `"pm0025/pm0025_00.trmsh"`)
- TRMSH → buffer path (e.g., `"pm0025/pm0025_00.trmbf"`)
- TRMDL → skeleton path, material paths

---

## 4. Reference Implementations

### gftool (Primary — Trinity decode)

Located at `D:\Projects\gftool`. Proven working implementation.

| Component | File | Lines |
|-----------|------|-------|
| TRPAK loader | `GFTool.Core/Serializers/GFLX/GFPakSerializer.cs` | 241 |
| FlatBuffer schemas | `GFTool.Core/Flatbuffers/TR/Model/*.cs` | 5 files |
| Model decode | `GFTool.Renderer/Scene/GraphicsObjects/Model.cs` | 1,932 |
| Armature decode | `GFTool.Renderer/Scene/GraphicsObjects/Armature.cs` | 877 |
| Animation decode | `GFTool.Renderer/Scene/GraphicsObjects/Animation.cs` | 530 |
| Material decode | `GFTool.Renderer/Scene/GraphicsObjects/Material.cs` | ~400 |
| GLTF export | `TrinityModelViewer/Export/GltfExporter.cs` | 1,208 |
| Oodle decompress | `GFTool.Core/Compression/Oodle.cs` | ~100 |

**Key dependency**: FlatSharp 6.3.5 NuGet.

### Switch-Toolbox (Secondary — DAE export)

Located at `D:\Projects\Switch-Toolbox`. Only the DAE writer is needed.

| Component | File | Lines |
|-----------|------|-------|
| ColladaWriter | `Switch_Toolbox_Library/FileFormats/DAE/ColladaWriter.cs` | 1,162 |
| DAE orchestrator | `Switch_Toolbox_Library/FileFormats/DAE/DAE.cs` | 689 |
| BNTX decoder | `File_Format_Library/FileFormats/Texture/BNTX.cs` | ~2,000 |

---

## 5. Solution Structure

```
D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\
├── SwitchToolboxCli.sln
├── src/
│   ├── SwitchToolboxCli.App/              # CLI entry point
│   │   ├── SwitchToolboxCli.App.csproj
│   │   └── Program.cs
│   │
│   └── SwitchToolboxCli.Core/             # Core library
│       ├── SwitchToolboxCli.Core.csproj
│       ├── Flatbuffers/                    # Copied from gftool (FlatSharp types)
│       │   ├── TR/Model/                   # TRMDL, TRMSH, TRMBF, TRSKL, TRMTR
│       │   ├── GF/Animation/              # GfAnimation (TRANM)
│       │   ├── Common/                     # Shared FlatBuffer types
│       │   └── Converters/                 # FlatBufferConverter
│       │
│       ├── Decode/                         # Ported from gftool (business logic)
│       │   ├── TrinityModelDecoder.cs      # From Model.cs — mesh + material parsing
│       │   ├── TrinityArmatureDecoder.cs   # From Armature.cs — skeleton
│       │   ├── TrinityAnimationDecoder.cs  # From Animation.cs — keyframes
│       │   └── TrinityMaterialDecoder.cs   # From Material.cs — texture refs
│       │
│       ├── Archive/                        # Ported from gftool (TRPAK)
│       │   ├── TrpakLoader.cs              # From GFPakSerializer.cs
│       │   └── OodleDecompressor.cs        # From Oodle.cs
│       │
│       ├── Export/                          # DAE output (from Switch-Toolbox)
│       │   ├── ColladaWriter.cs            # From Switch-Toolbox ColladaWriter.cs
│       │   └── DaeExporter.cs              # Export orchestration (model-only + clip-only modes)
│       │
│       └── Texture/                        # BNTX decode (from Switch-Toolbox)
│           └── BntxDecoder.cs
│
└── tests/
    └── SwitchToolboxCli.Tests/
        ├── SwitchToolboxCli.Tests.csproj
        ├── DecodeTests/                    # Test decoders with fixture data
        ├── ExportTests/                    # Test DAE output structure
        └── Fixtures/                       # Small extracted test files
```

---

## 6. Phased Implementation (Export-First)

### Phase 1: Copy gftool FlatBuffer Schemas + Core Types

Copy FlatSharp-generated Trinity types for deserializing TRMDL/TRMSH/TRMBF/TRSKL/TRMTR/TRANM.

**Source**: `GFTool.Core/Flatbuffers/` → `SwitchToolboxCli.Core/Flatbuffers/`

**Deliverable**: Types compile; can deserialize raw data into typed objects.

### Phase 2: Port Trinity Model Decoder (Mesh + Skeleton)

Port gftool's decode path, stripping OpenGL rendering, keeping data extraction.

**Key decode logic to preserve (from `Model.ParseMeshBuffer`):**
- Declaration-driven vertex attribute parsing: `TRVertexDeclaration` / `TRVertexUsage` / `TRVertexFormat`
- Multi-stream blend index/weight collapsing
- Index buffer parsing with `TRIndexFormat` (BYTE/SHORT/INT)

**Source → Target:**
- `Model.cs` → `TrinityModelDecoder.cs` (strip GL, keep `ParseMesh`/`ParseMeshBuffer`/`Read*`)
- `Armature.cs` → `TrinityArmatureDecoder.cs` (strip GL, keep bone hierarchy + inverse bind)
- `Material.cs` → `TrinityMaterialDecoder.cs` (strip GL, keep texture ref parsing)

**Deliverable**: Given raw bytes, produce structured data (positions, normals, UVs, indices, bones).

### Phase 3: DAE Exporter (Model-Only + Clip-Only)

Port Switch-Toolbox's `ColladaWriter.cs`, implementing both split export modes per docs 08/09:

- **Model-only**: `<library_geometries>` + `<library_controllers>` + `<library_visual_scenes>` with skeleton. NO `<library_animations>`.
- **Clip-only**: `<library_visual_scenes>` with skeleton + `<library_animations>` channels. NO geometry/controllers/materials.

**Adaptation**: Replace OpenTK → `System.Numerics`, remove WinForms dependencies, wire to Phase 2 data structures.

**Deliverable**: Test model → export `model.dae` + `clip.dae` → validate XML structure.

### Phase 4: Animation Decoder + Clip Export

Port gftool's `Animation.cs` to decode TRANM files into per-bone keyframes.

**Source**: `Animation.cs` → `TrinityAnimationDecoder.cs` (strip GL playback, keep Catmull-Rom / Squad quaternion interpolation).

**Deliverable**: Load TRANM → produce keyframes → export `clips/clip_NNN.dae`.

### Phase 5: TRPAK Archive Loader

Port gftool's `GFPakSerializer.cs` to load TRPAK archives.

**Source → Target:**
- `GFPakSerializer.cs` → `TrpakLoader.cs`
- `Oodle.cs` → `OodleDecompressor.cs`
- `GFPakHashCache.cs` → hash name resolution

**Deliverable**: Open TRPAK → list contents → resolve paths → extract decompressed files.

### Phase 6: Texture Pipeline (BNTX → PNG)

Port BNTX decode-to-RGBA path from Switch-Toolbox; use `SixLabors.ImageSharp` for PNG.

**Deliverable**: Extract textures → write PNGs → reference in model.dae `<library_images>`.

### Phase 7: CLI + End-to-End Pipeline + Manifest

Wire everything into a single CLI command:

```bash
switchtool export <trpak-or-trmdl> -o <outputDir>
```

Output: `model.dae` + `textures/*.png` + `clips/clip_NNN.dae` + `manifest.json`

---

## 7. Key Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| FlatBuffer library | **FlatSharp 6.3.5** | gftool uses it; schemas are pre-generated for this lib |
| Vector math | **OpenTK.Mathematics 4.8.2** | gftool Model.cs (1,932 lines) uses it throughout; avoids hundreds of manual type conversions |
| Oodle | **P/Invoke to `oo2core_8_win64.dll`** | Required for TRPAK decompression |
| Image output | **SixLabors.ImageSharp 3.1.0** | Cross-platform PNG; no GDI+ dependency |
| DAE writing | **XmlTextWriter** (from Switch-Toolbox ColladaWriter) | Proven COLLADA 1.4 output, already working |

---

## 8. File Sourcing Map

| New File | Source Project | Source File | Copy or Adapt |
|----------|--------------|------------|---------------|
| `Flatbuffers/TR/Model/*.cs` | gftool | `GFTool.Core/Flatbuffers/TR/Model/` | Copy + namespace |
| `Flatbuffers/GF/Animation/*` | gftool | `GFTool.Core/Flatbuffers/GF/Animation/` | Copy + namespace |
| `Flatbuffers/Common/*` | gftool | `GFTool.Core/Flatbuffers/Common/` | Copy + namespace |
| `Flatbuffers/Converters/*` | gftool | `GFTool.Core/Flatbuffers/Converters/` | Copy + namespace |
| `Decode/TrinityModelDecoder.cs` | gftool | `Model.cs` | Adapt (strip GL) |
| `Decode/TrinityArmatureDecoder.cs` | gftool | `Armature.cs` | Adapt (strip GL) |
| `Decode/TrinityAnimationDecoder.cs` | gftool | `Animation.cs` | Adapt (strip GL) |
| `Decode/TrinityMaterialDecoder.cs` | gftool | `Material.cs` | Adapt (strip GL) |
| `Archive/TrpakLoader.cs` | gftool | `GFPakSerializer.cs` | Adapt |
| `Archive/OodleDecompressor.cs` | gftool | `Oodle.cs` | Copy + adapt |
| `Export/ColladaWriter.cs` | Switch-Toolbox | `ColladaWriter.cs` | Adapt |
| `Export/DaeExporter.cs` | Switch-Toolbox | `DAE.cs` | Adapt |
| `Texture/BntxDecoder.cs` | Switch-Toolbox | `BNTX.cs` | Selective port |

---

## 9. Verification Plan

### 9.1 Build

```powershell
dotnet build D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\SwitchToolboxCli.sln
dotnet test D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\SwitchToolboxCli.sln
```

### 9.2 Export Checks

```powershell
dotnet run --project src/SwitchToolboxCli.App -- export <trpak-path> -o ./test-output
```

Verify:
- `model.dae` has `<library_geometries>`, `<library_controllers>`, `<library_visual_scenes>` — NO `<library_animations>`
- `clips/clip_000.dae` has `<library_visual_scenes>` (skeleton) + `<library_animations>` — NO `<library_geometries>`
- Bone IDs match between model and clip files (`{BoneName}_bone_id`)
- `manifest.json` references only existing files
- `textures/*.png` files exist

### 9.3 Blender Smoke Test

1. Import `model.dae` — model displays in T-pose, correct textures
2. Import `clips/clip_000.dae` — creates Action on new armature
3. Transfer Action to model armature — animation plays without warping
4. Switch between clips — all play correctly on the same model
5. No body distortion, no bone skew across clip changes

---

## 10. Acceptance Criteria

### Export Pipeline
- [ ] Load a TRPAK → find TRMDL → decode mesh/skeleton → export `model.dae` with vertices > 0
- [ ] Load TRANM → decode keyframes → export `clips/clip_NNN.dae` with animation channels
- [ ] Model DAE: has geometry + skeleton + skin controllers, NO `<library_animations>`
- [ ] Clip DAE: has skeleton + `<library_animations>`, NO `<library_geometries>` or `<library_controllers>`
- [ ] Both share identical bone IDs (`{BoneName}_bone_id`)
- [ ] Extract BNTX textures as PNG in `textures/` folder
- [ ] `manifest.json` links model, textures, and clips with metadata

### Blender Validation
- [ ] `model.dae` imports with visible mesh and skeleton
- [ ] Clip DAE animation transfers to model armature
- [ ] No body distortion across clip changes

### Build
- [ ] No WinForms, OpenGL rendering, or viewport dependencies at runtime
- [ ] `dotnet build` and `dotnet test` pass on .NET 8
