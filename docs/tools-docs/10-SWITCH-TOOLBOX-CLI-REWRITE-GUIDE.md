# Switch-Toolbox to Modern C# CLI Rewrite Guide

> Goal: rebuild this into a **headless .NET 8/9 CLI** that loads RomFS folders and GARC archives, then exports models (mesh + skeleton), textures, and animation clips.
>
> Constraint: **no UI/OpenGL components** in the new implementation.

---

## 1. Scope and Target Outcomes

### 1.3 Current Implementation Status (2026-02-17)

- Completed foundation in `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli`:
  - CLI commands: `info`, `scan`, `extract-garc`, `batch`, `convert`.
  - Archive support: NCSD, NCCH, RomFS, and practical GARC parsing (FATO/FATB/FIMB workflow).
  - Recursive archive walking and deterministic extraction services.
  - Headless exporter foundations: OBJ + DAE.
  - Texture export pipeline (signature/extension heuristics) to `textures/`.
  - Clip extraction pipeline (heuristics) to `clips/`.
  - Model export pipeline to `models/`:
    - pass-through for `.obj`/`.dae`
    - JSON bridge conversion into requested `obj|dae`.
  - Core manifest models + deterministic JSON writer.
- Validation status: solution builds and tests pass (`35` passing tests).

- Build a terminal-only tool (no WinForms dialogs, no viewport).
- Support discovery and extraction from RomFS and GARC.
- Export model data to `dae` / `obj` (and `fbx` when backend is available).
- Export animation clips in CLI-friendly formats (`seanim`, `smd`, optional DAE clip mode later).
- Support single-file and batch archive traversal workflows.

---

## 2. Current Switch-Toolbox Architecture (CLI-Relevant)

- `Toolbox/` -> WinForms host app.
- `Switch_Toolbox_Library/` -> loader pipeline, core interfaces, exporters, plus a lot of UI/runtime coupling.
- `File_Format_Library/` -> concrete format handlers and archive readers.

### Core load/detect flow

- `Switch_Toolbox_Library/IO/STFileLoader.cs`
  - Detects compression, then identifies format via registered `IFileFormat` types.

- `Switch_Toolbox_Library/Format Managers/FileManager.cs`
  - Builds format/compression lists (built-ins + plugins).

- `File_Format_Library/Main.cs`
  - Registers concrete format classes for the plugin.

### Archive and RomFS

- `File_Format_Library/FileFormats/Rom/3DS/RomFS.cs`
- `File_Format_Library/FileFormats/Rom/3DS/NCCH.cs`
- `File_Format_Library/FileFormats/Rom/3DS/NCSD.cs`

### Exporters / conversion points

- `Switch_Toolbox_Library/FileFormats/DAE/DAE.cs`
- `Switch_Toolbox_Library/FileFormats/DAE/ColladaWriter.cs`
- `Switch_Toolbox_Library/FileFormats/OBJ.cs`
- `Switch_Toolbox_Library/FileFormats/Fbx/FbxExporter.cs`
- `File_Format_Library/FileFormats/BFRES/Bfres Structs/SubFiles/FSKA.cs`

### Existing batch recursion logic (UI-hosted)

- `Toolbox/MainForm.cs` (`SearchFileFormat`, `SearchArchive`)
  - Good behavioral reference for recursive extraction.

---

## 3. Reuse vs Rewrite Matrix

### Reuse / adapt

- `Switch_Toolbox_Library/Interfaces/ModelData/IExportableModel.cs`
- `Switch_Toolbox_Library/Interfaces/ModelData/IExportableModelContainer.cs`
- `Switch_Toolbox_Library/IO/STFileLoader.cs` (adapt to pure headless)
- `Switch_Toolbox_Library/Format Managers/FileManager.cs` (adapt)
- `File_Format_Library/FileFormats/Rom/3DS/*` (copy/adapt)
- `Switch_Toolbox_Library/FileFormats/DAE/ColladaWriter.cs` (writer core)
- `Switch_Toolbox_Library/FileFormats/OBJ.cs` (baseline exporter)
- animation conversion logic from `FSKA.cs` (selectively port)

### Full rewrite / exclude

- `Toolbox/MainForm.cs` batch/export UX logic -> rewrite as command handlers.
- UI wrappers and dialogs in archive/form/editor layers.
- TreeView/TreeNode/archive wrapper UI plumbing.
- OpenGL/rendering/viewport systems.
- Any code path requiring `MessageBox`, `OpenFileDialog`, `SaveFileDialog`, `STProgressBar`.

---

## 4. Critical Gaps for the Target Workflow

1. **No dedicated Pokemon GARC parser located in this repo.**
   - `GAR.cs` exists, but that is Grezzo `ZAR/GAR`, not Pokemon `.garc`.
2. **Animation clip pipeline exists but is not a clean standalone CLI service.**
   - `FSKA` supports several exports, but with editor/runtime assumptions around skeleton context.
3. **FBX export depends on native DLL (`SwitchToolbox.FbxNative.dll`).**
   - Must be optional or replaced for portability.
4. **Legacy framework/project style.**
   - Current solution is .NET Framework 4.8 and plugin-era loading assumptions.

---

## 5. Phased Rewrite Plan

### Phase 0 - Scaffold

- Create SDK-style solution:
  - `SwitchToolbox.Cli`
  - `SwitchToolbox.Core`
  - `SwitchToolbox.Formats`
  - `SwitchToolbox.Tests`
- Target `net8.0` (keep `net9.0` compatible style).

### Phase 1 - Headless Loader Kernel

- Port/adapt `STFileLoader` + registry behavior.
- Remove all UI/runtime global coupling.
- Add deterministic format registration order and tests.

### Phase 2 - Archive Traversal

- Port NCSD/NCCH/RomFS readers.
- Build recursive walker service equivalent to `SearchArchive`/`SearchFileFormat` behavior.

### Phase 3 - GARC Support (required)

- Implement Pokemon GARC parser module.
- Add fixture-driven tests for entry parsing and extraction.

### Phase 4 - Model + Texture Export

- Port DAE and OBJ export headlessly.
- Add texture dump service and naming policy.
- Keep FBX optional via capability check.

### Phase 5 - Animation Clip Export

- Port clip conversion logic from FSKA path into standalone services.
- Export clips (`seanim` baseline, optional `smd`).
- Write `manifest.json` linking model/textures/clips.

### Phase 6 - CLI Commands + Hardening

- Implement commands (`info`, `scan`, `convert`, `batch`, `extract-garc`).
- Add robust error accumulation and non-zero exit semantics.
- Validate output on real RomFS + GARC datasets.

---

## 6. Proposed CLI Contract

```bash
switchtool info <input>
switchtool scan <input> -o <report.json>
switchtool convert <input> -o <outDir> --model-format dae [--extract true|false]
switchtool extract-garc <garcFile> -o <outDir>
switchtool batch <input> -o <outDir> --model-format dae --clip-format seanim
switchtool extract-bins <input> -o <outDir>
switchtool bulk-convert <inputDir> -o <outDir> --model-format dae --limit 200 --resume true
```

Current behavior notes:

- `extract-bins` is the canonical command for raw TRPFS/TRPAK bin extraction.
- `convert` writes `manifest.json` only when at least one model output is completed.
- `convert --extract false` avoids creating raw extraction trees.
- `bulk-convert` writes `bulk-convert-checkpoint.json` for resume-safe processing.

Output layout per asset group:

```text
<asset-id>/
  model.dae
  textures/
    *.png
  clips/
    clip_000.seanim
    clip_001.seanim
  manifest.json
```

---

## 7. Risks and Mitigations

- **GARC parser uncertainty** -> implement/test this first.
- **UI coupling regressions** -> enforce project boundaries, no WinForms refs.
- **FBX backend fragility** -> optional backend; DAE/OBJ always available.
- **Format-detection collisions** -> deterministic registration + unit tests.
- **Animation mapping edge cases** -> start with proven clip formats and expand.

---

## 8. Execution Todo List

- [ ] P0: Scaffold new .NET 8 CLI solution/projects.
- [ ] P1: Port headless loader + compression pipeline.
- [ ] P2: Port RomFS/NCCH/NCSD + archive walker.
- [ ] P3: Build Pokemon GARC parser + fixtures.
- [ ] P4: Port DAE/OBJ + texture exporters.
- [ ] P5: Implement clip export + manifest generation.
- [ ] P6: Implement CLI command surface.
- [ ] P7: Add integration tests and sample corpus validation.
- [ ] P8: Validate outputs in Blender/engine pipeline.

---

## 9. Acceptance Criteria

- CLI loads and traverses RomFS inputs headlessly.
- CLI loads and iterates GARC entries.
- CLI exports model mesh + textures deterministically.
- CLI exports animation clips and writes manifest metadata.
- No UI dependencies are required at runtime.
- Builds/tests pass under SDK-style .NET 8.
