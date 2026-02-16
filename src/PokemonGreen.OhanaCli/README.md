# PokemonGreen.OhanaCli

OhanaCli is the Pokemon-focused CLI rewrite of the Ohana3DS export pipeline used in this repo.

Its primary job is to read game assets (especially GARC containers), group model/texture/animation payloads correctly, and export:

- `DAE` (with textures and skeletal animations)
- `OBJ` (static mesh + textures)

This tool is built for high-parity conversion work where correctness matters more than speed.

## Scope and goals

The CLI is designed to:

- Load Pokemon model container data from split entries (model + textures + animation payloads)
- Preserve texture mapping/binding fidelity in exported DAE/OBJ
- Export skeletal animation in Blender-compatible COLLADA matrix channels
- Support both animation output modes:
  - per-clip DAE files (default)
  - consolidated all-clips-in-one DAE (`--consolidate-animations`)
- Provide diagnostics (`diagnose`, `--diag-anim`) for triage and parity checks

## Project layout

Inside `src/PokemonGreen.OhanaCli`:

- `OhanaCli.sln` - solution
- `src/OhanaCli.App` - CLI command surface (`info`, `convert`, `batch`, `diagnose`)
- `src/OhanaCli.Formats` - parser/loader/exporter pipeline (ported Ohana code)
- `tests/OhanaCli.App.Tests` - CLI convention/unit tests
- `scripts/Run-ManualParityChecks.ps1` - manual parity helper
- `PHASE4-VALIDATION.md` - quality-gate commands and notes

## Requirements

- Windows runtime (`net8.0-windows`)
- .NET SDK 8+

NuGet highlights:

- `System.CommandLine` `2.0.0-beta4.22272.1`
- `SixLabors.ImageSharp` `3.1.6`
- `System.Drawing.Common` `8.0.12`

## Build and test

From repo root (`D:/Projects/PokemonGreen`):

```bash
dotnet build src/PokemonGreen.OhanaCli/OhanaCli.sln
dotnet test src/PokemonGreen.OhanaCli/tests/OhanaCli.App.Tests/OhanaCli.App.Tests.csproj
```

Quick CLI smoke:

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- --help
```

## Commands

### `info`

Inspect one file and print detected type and basic counts.

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- info "<file>"
```

### `convert`

Convert one file or container.

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- convert "<file>" -o "<outDir>" [options]
```

Options:

- `-o, --output` (required): output directory
- `-f, --format`: `dae` or `obj` (default `dae`)
- `-a, --animation-index`: export one specific skeletal clip index
- `--consolidate-animations`: for DAE, export one model file containing all clips
- `-n, --limit`: max container entries to scan
- `--diag-anim`: verbose per-bone exporter diagnostics to stderr

### `batch`

Convert all files under a directory.

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- batch "<inputDir>" -o "<outDir>" [options]
```

`batch` supports the same format/animation options as `convert`.

### `diagnose`

Inspect container entries and summarize what each entry appears to contain.

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- diagnose "<containerFile>" --start 0 --end 20
```

## Animation export behavior (important)

For `DAE`:

1. **Default** (no `-a`, no `--consolidate-animations`)
   - Exports one DAE per animation clip per model
   - Example: `model.anim_000.dae`, `model.anim_001.dae`, ...

2. **Consolidated mode** (`--consolidate-animations`)
   - Exports one DAE per model containing all clips in one file
   - Example: `model.dae`, `model_1.dae`

3. **Single clip mode** (`-a <index>`)
   - Exports one selected clip per model
   - `-a` takes precedence over `--consolidate-animations`

For `OBJ`:

- OBJ exports static meshes only (no skeletal animation), with textures written as PNG.

## Typical workflows

### A) Per-clip DAE export (default)

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o "src/PokemonGreen.Tests/exports-dae-perclip" -n 20
```

### B) Consolidated DAE export (all clips in each model file)

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o "src/PokemonGreen.Tests/exports-dae-consolidated" -n 20 --consolidate-animations
```

### C) Explicit single clip export

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o "src/PokemonGreen.Tests/exports-dae-a0" -n 20 -a 0
```

### D) OBJ export

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o "src/PokemonGreen.Tests/exports-obj" -n 20 -f obj
```

### E) Deep animation diagnostics during export

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o "src/PokemonGreen.Tests/exports-diag" -n 20 -a 0 --diag-anim 2>&1
```

## Output layout

The converter writes into `output/<inputBaseName>/`.

For input `.../a/0/9/4`:

```text
<output>/4/
  0000_model/
  0001_model/
  0002_model/
```

Each group folder contains model files (`.dae` or `.obj`) plus exported textures (`.png`).

## Exit codes

- `0` = success
- `1` = fatal failure
- `2` = partial failure (mixed success/failure)

`batch` aggregates per-file outcomes and returns the appropriate aggregate code.

## Container grouping details

Pokemon assets are often split across consecutive container entries. The pipeline groups entries by merging:

- mesh-bearing model payloads
- animation-only payloads
- texture payloads

This is why `--limit` can materially affect animation output.

If limit is too low and clips are beyond the processed range, the tool emits a warning:

- `warning: no skeletal animations were included because --limit=... ended before animation entries...`

## Troubleshooting

### T-pose in Blender

Common causes:

- no animation exported (wrong flags or insufficient `--limit`)
- imported a static OBJ instead of DAE

Checks:

- run `diagnose` to locate animation-rich entry ranges
- run `convert` with `--diag-anim`
- verify DAE has non-empty `<library_animations>`

### Texture appears but mapping is wrong

The exporter now applies Pokemon material texture-coordinate transforms and binding fixes. If a model still maps incorrectly, capture:

- source file path
- exact command used
- output DAE path
- one screenshot

### File lock errors on large GARCs

Avoid running parallel conversions against the same source file simultaneously.

## Validation and parity docs

See:

- `src/PokemonGreen.OhanaCli/PHASE4-VALIDATION.md`
- `src/PokemonGreen.OhanaCli/scripts/Run-ManualParityChecks.ps1`

## Notes for integrators

- DAE animation timing is exported in seconds and sampled densely for Blender stability.
- COLLADA channels target bone matrix transforms (`<bone_id>/transform`) with `matrix sid="transform"`.
- Export file names are sanitized and de-duplicated to avoid collisions.
