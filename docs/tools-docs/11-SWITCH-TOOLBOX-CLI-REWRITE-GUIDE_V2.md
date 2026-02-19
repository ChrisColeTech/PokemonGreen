# Switch-Toolbox to Modern C# CLI Rewrite Guide
> Goal: rebuild the extraction workflow as a **headless .NET 8/9 CLI** that loads RomFS folders and GARC archives, then exports model geometry + textures + animation clips in friendly formats (`.dae`, `.obj`, `.fbx` when available).
>
> Constraint: **no WinForms/UI/OpenGL code in the new tool**.
---
## Table of Contents
1. [Scope and Target Outcomes](#1-scope-and-target-outcomes)
2. [What Exists in Switch-Toolbox Today](#2-what-exists-in-switch-toolbox-today)
3. [CLI-Relevant Architecture Map](#3-cli-relevant-architecture-map)
4. [What We Can Reuse vs Rewrite](#4-what-we-can-reuse-vs-rewrite)
5. [Critical Gaps for Your Use Case](#5-critical-gaps-for-your-use-case)
6. [Phased Rewrite Plan](#6-phased-rewrite-plan)
7. [Proposed CLI Contract](#7-proposed-cli-contract)
8. [Risk Register](#8-risk-register)
9. [Execution Todo List](#9-execution-todo-list)
10. [Acceptance Criteria](#10-acceptance-criteria)
---
## 1. Scope and Target Outcomes

### 1.3 Current Implementation Status (2026-02-17)

- Active project root:
  - `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli`
- Legacy reference root:
  - `D:\Projects\Switch-Toolbox`
- Completed implementation blocks:
  - GARC parser + tests (CRAG/GARC, FATO/FATB/FIMB extraction flow).
  - 3DS archive chain + tests (`NCSD -> NCCH -> RomFS`).
  - CLI commands: `info`, `scan`, `extract-garc`, `batch`, `convert`.
  - Deterministic extraction to `extracted/`.
  - Model export to `models/` (pass-through `.obj`/`.dae` + JSON model bridge to OBJ/DAE).
  - Texture export to `textures/` using signature/extension heuristics.
  - Clip export to `clips/` using clip heuristics.
  - Core manifest models and deterministic manifest writer.
- Validation status:
  - `dotnet build` succeeds.
  - `dotnet test` succeeds with `35` passing tests.
### 1.1 In-Scope
- Headless CLI only (`dotnet` app, no dialogs/forms/viewport).
- Input discovery from:
  - RomFS files/containers (3DS NCSD/NCCH/RomFS path stack).
  - GARC archives (Pokemon-oriented workflow).
- Export pipeline for:
  - Models: mesh + skeleton + materials.
  - Textures: exported to image files.
  - Animation clips: extracted/exported as clip files.
- Easy batch usage for a whole folder/archive.
### 1.2 Explicitly Out of Scope
- Any UI editor workflow (TreeNodes, context menus, dialogs).
- Any OpenGL preview/rendering pipeline.
- Runtime editor features (material editing, transform tools, etc.).
---
## 2. What Exists in Switch-Toolbox Today
Switch-Toolbox is a .NET Framework 4.8 desktop app with a plugin-based format system:
- `Toolbox/` -> WinForms host and batch actions.
- `Switch_Toolbox_Library/` -> common interfaces, loader pipeline, exporters, plus lots of UI/runtime glue.
- `File_Format_Library/` -> most concrete parsers/loaders (archives, BFRES, ROM formats, etc.).
Key fact for this rewrite:
- The extraction logic exists, but it is intertwined with UI-oriented abstractions in many places.
- The old app does support RomFS traversal, generic archive traversal, model export, and some animation export.
---
## 3. CLI-Relevant Architecture Map
### 3.1 Format Detection and Load Pipeline
- `Switch_Toolbox_Library/IO/STFileLoader.cs`
  - Central entrypoint: detect compression, detect format, call `Load(stream)`.
  - Useful for CLI, but currently includes legacy/UI-era assumptions.
- `Switch_Toolbox_Library/Format Managers/FileManager.cs`
  - Builds format/compression registries from built-ins + plugins.
- `File_Format_Library/Main.cs`
  - Giant registration list of all plugin formats.
### 3.2 Archive + RomFS
- `File_Format_Library/FileFormats/Rom/3DS/RomFS.cs`
  - Parses IVFC RomFS metadata and exposes archive entries.
- `File_Format_Library/FileFormats/Rom/3DS/NCCH.cs`
  - Reads NCCH and mounts RomFS substream.
- `File_Format_Library/FileFormats/Rom/3DS/NCSD.cs`
  - Reads NCSD and routes to NCCH.
### 3.3 Model and Texture Export
- `Switch_Toolbox_Library/FileFormats/DAE/DAE.cs`
- `Switch_Toolbox_Library/FileFormats/DAE/ColladaWriter.cs`
- `Switch_Toolbox_Library/FileFormats/OBJ.cs`
- `Switch_Toolbox_Library/FileFormats/Fbx/FbxExporter.cs` (native DLL dependency)
### 3.4 Animation Export/Conversion
- `File_Format_Library/FileFormats/BFRES/.../FSKA.cs`
  - Exports BFSKA/JSON/CHR0/SMD/ANIM/SEANIM.
  - DAE/FBX animation import hooks are present but commented in places.
### 3.5 Existing Recursive Batch Logic (UI-hosted)
- `Toolbox/MainForm.cs` (`SearchFileFormat`, `SearchArchive`)
  - Already does recursive archive walking and model/texture export dispatch.
  - Must be reimplemented headlessly for CLI.
---
## 4. What We Can Reuse vs Rewrite
### 4.1 Reuse/Adapt (High Value)
- **Core contracts**
  - `Switch_Toolbox_Library/Interfaces/ModelData/IExportableModel.cs`
  - `Switch_Toolbox_Library/Interfaces/ModelData/IExportableModelContainer.cs`
- **Loader orchestration (adapt)**
  - `Switch_Toolbox_Library/IO/STFileLoader.cs`
  - `Switch_Toolbox_Library/Format Managers/FileManager.cs`
- **RomFS stack (copy/adapt)**
  - `File_Format_Library/FileFormats/Rom/3DS/RomFS.cs`
  - `File_Format_Library/FileFormats/Rom/3DS/NCCH.cs`
  - `File_Format_Library/FileFormats/Rom/3DS/NCSD.cs`
- **Export backends (surgical reuse)**
  - `Switch_Toolbox_Library/FileFormats/DAE/ColladaWriter.cs` (writer core)
  - `Switch_Toolbox_Library/FileFormats/OBJ.cs` (simple path)
  - selected logic from `FSKA.cs` for clip conversion/export
### 4.2 Rewrite Required (CLI Purity)
- `Toolbox/MainForm.cs` batch flow -> replace with command handlers/services.
- Any class under `Forms/`, any `MessageBox`, `OpenFileDialog`, `SaveFileDialog`, `STProgressBar` usage.
- Any `TreeNode` wrapper/editor plumbing in archive interfaces.
- Any runtime/global UI state dependencies (`Runtime`, viewport/editor assumptions).
### 4.3 Exclude from New CLI
- Entire OpenGL/preview/editor stack.
- Any model editing actions (normals/tangent tools, in-app transforms, etc.).
---
## 5. Critical Gaps for Your Use Case
1. **No dedicated Pokemon `.garc` parser was found in Switch-Toolbox source.**
   - Search for `GARC/CRAG/garc` found no concrete parser class.
   - Existing `GAR.cs` is Grezzo `ZAR/GAR`, not Pokemon GARC.
2. **Animation clip export exists, but DAE/FBX animation flow is not a clean standalone pipeline.**
   - `FSKA` supports clip outputs (`.smd`, `.anim`, `.seanim`) and BFSKA.
   - Need a normalized CLI-facing clip export strategy.
3. **FBX export is tied to native dependency**
   - `SwitchToolbox.FbxNative.dll` required.
   - Must decide: keep optional Windows backend or replace for cross-platform.
4. **Old framework and package model**
   - Current projects target .NET Framework 4.8 with legacy references.
---
## 6. Phased Rewrite Plan
### Phase 0: New Solution Scaffold (CLI-Only)
Create fresh SDK-style solution (no direct UI project migration):
- `SwitchToolbox.Cli` (entrypoint)
- `SwitchToolbox.Core` (format registry + pipeline abstractions)
- `SwitchToolbox.Formats` (ported parsers/exporters)
- `SwitchToolbox.Tests` (golden and integration tests)
Target: `net8.0` initially, keep `net9.0` readiness.

### Proposed Project Structure (Absolute Paths)

Primary new project root:

- `D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli`

Legacy source root (reference only):

- `D:\Projects\Switch-Toolbox`

Planning docs used by all contributors/agents:

- `D:\Projects\PokemonGreen\docs\tools-docs\10-SWITCH-TOOLBOX-CLI-REWRITE-GUIDE.md`
- `D:\Projects\PokemonGreen\docs\tools-docs\11-SWITCH-TOOLBOX-CLI-REWRITE-GUIDE_V2.md`

Target scaffold:

```text
D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\
  PokemonGreen.SwitchToolboxCli.sln

  src\
    PokemonGreen.SwitchToolboxCli.App\              # CLI commands + hosting
      Commands\
      Program.cs

    PokemonGreen.SwitchToolboxCli.Core\             # Pure domain abstractions/services
      Abstractions\
        IFileFormat.cs
        IArchiveFile.cs
        IExportableModel.cs
        IExportableModelContainer.cs
      Loading\
        FormatRegistry.cs
        FileLoader.cs
      Extraction\
        ArchiveWalker.cs
      Models\
        ExtractionManifest.cs

    PokemonGreen.SwitchToolboxCli.Formats\          # Ported/adapted format handlers
      Archives\
        Rom3DS\
          RomFS.cs
          NCCH.cs
          NCSD.cs
        Pokemon\
          GARC.cs                                    # new implementation
      Export\
        Dae\
          ColladaWriter.cs
          DaeExporter.cs
        Obj\
          ObjExporter.cs
      Animations\
        ClipExporter.cs

  tests\
    PokemonGreen.SwitchToolboxCli.Tests\
      LoaderTests\
      ArchiveTests\
      ExportTests\
      Fixtures\
```

Initial direct-copy candidates from legacy (copy then modify):

- `D:\Projects\Switch-Toolbox\Switch_Toolbox_Library\Interfaces\ModelData\IExportableModel.cs`
- `D:\Projects\Switch-Toolbox\Switch_Toolbox_Library\Interfaces\ModelData\IExportableModelContainer.cs`
- `D:\Projects\Switch-Toolbox\File_Format_Library\FileFormats\Rom\3DS\RomFS.cs`
- `D:\Projects\Switch-Toolbox\File_Format_Library\FileFormats\Rom\3DS\NCCH.cs`
- `D:\Projects\Switch-Toolbox\File_Format_Library\FileFormats\Rom\3DS\NCSD.cs`

Initial reference-only (do not copy as-is):

- `D:\Projects\Switch-Toolbox\Toolbox\MainForm.cs` (UI batch logic only)
- `D:\Projects\Switch-Toolbox\Switch_Toolbox_Library\FileFormats\DAE\DAE.cs` (contains UI state/progress)
- `D:\Projects\Switch-Toolbox\Switch_Toolbox_Library\FileFormats\Fbx\FbxExporter.cs` (native dependency, optional backend)
### Phase 1: Headless Loader Kernel
- Port/adapt file detection/compression flow from `STFileLoader.cs`.
- Remove all UI references and mutable global runtime dependencies.
- Add deterministic registry setup (explicit ordering, testable).
Deliverable: `Load(path|stream)` returns typed format object in CLI context.
### Phase 2: Archive Traversal + RomFS
- Port NCSD/NCCH/RomFS readers and expose recursive walker service.
- Rebuild `SearchFileFormat`/`SearchArchive` behavior as service methods.
Deliverable: `walk` command that discovers candidate model/texture/anim assets.
### Phase 3: GARC Implementation (Required)
- Implement Pokemon GARC parser (new module), plus tests with known sample archives.
- Support extraction and per-entry stream handoff to loader pipeline.
Deliverable: `extract garc` and `convert garc` command paths functional.
### Phase 4: Exporters (Mesh + Texture)
- Port DAE and OBJ export paths headlessly.
- Texture export path independent from UI state.
- Keep FBX optional behind capability check.
Deliverable: per-model output folder with geometry + textures.
### Phase 5: Animation Clip Pipeline
- Build CLI-facing animation extraction service using `FSKA` conversion logic.
- Define clip output formats (recommended: `seanim` + optional `smd`; optional `dae` clip mode later).
- Ensure model/clip association metadata is written.
Deliverable: `clips/` output + machine-readable manifest.
### Phase 6: Polished CLI UX + Batch Reliability
- Commands, options, progress, failures summary, resume-friendly logs.
- Add robust error accumulation and non-zero exit policy.
- Validate on real RomFS + GARC corpora.
---
## 7. Proposed CLI Contract
```bash
# inspect file/archive
switchtool info <input>
# convert one file/archive
switchtool convert <input> -o <outDir> --model-format dae [--extract true|false]
# recurse through romfs or folder
switchtool scan <inputDirOrRomFs> -o <report.json>
# extract pokemon garc
switchtool extract-garc <garcFile> -o <outDir>
# batch convert discovered assets
switchtool batch <input> -o <outDir> --model-format dae --clip-format seanim
# raw archive/bin extraction
switchtool extract-bins <input> -o <outDir>
# resume-safe bulk conversion over many trpak files
switchtool bulk-convert <inputDir> -o <outDir> --model-format dae --limit 200 --resume true
```

Current behavior notes:

- `extract-bins` is used for deterministic raw extraction only.
- `convert` now skips global manifest output when no model conversion completed.
- Trinity bin bundles are indexed and reported; true Trinity binary model assembly remains in-progress and currently records `pending_conversion` where applicable.
- `bulk-convert` persists `bulk-convert-checkpoint.json` and supports resume.
Recommended output shape per asset group:
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
## 8. Risk Register
1. **GARC uncertainty risk**
   - Mitigation: implement parser first with test vectors before broader pipeline.
2. **UI coupling regression risk**
   - Mitigation: strict project boundaries (`Core/Formats` cannot reference WinForms/OpenTK).
3. **FBX portability risk**
   - Mitigation: make FBX optional and non-blocking; DAE/OBJ remain baseline.
4. **Format-detection fragility risk**
   - Mitigation: deterministic registration order + unit tests for collisions.
5. **Animation compatibility risk**
   - Mitigation: start with proven clip outputs (`seanim`, `smd`) then extend.
---
## 9. Execution Todo List
### Phase Todos
- [ ] **P0** Create new .NET 8 CLI solution and projects (no UI references).
- [ ] **P1** Port headless file/compression detection pipeline.
- [ ] **P2** Port NCSD/NCCH/RomFS + recursive archive walker.
- [ ] **P3** Implement Pokemon GARC parser + test fixtures.
- [ ] **P4** Port DAE/OBJ exporters and texture dumping.
- [ ] **P5** Implement clip extraction/export + manifest writer.
- [ ] **P6** Implement CLI commands (`info`, `convert`, `scan`, `batch`, `extract-garc`).
- [ ] **P7** Add integration tests with sample RomFS/GARC corpora.
- [ ] **P8** Validate outputs in Blender/engine import workflows.
### Parallelizable Work
- GARC parser + tests can run in parallel with DAE/OBJ headless export cleanup.
- CLI command scaffolding can run in parallel with phase 2/4 service implementation.
---
## 10. Acceptance Criteria
- CLI can load RomFS path(s) and traverse recursively without UI dependencies.
- CLI can load GARC entries and identify model/texture/animation assets.
- CLI can export model mesh + textures to deterministic folder output.
- CLI can export animation clips in chosen clip format(s).
- All workflows run headless in terminal with clear logs and non-interactive behavior.
- Project builds in SDK-style .NET 8 with tests passing.
