# Spica.Registry

Scans Pokemon GARC archives, classifies entries (models, textures, animations), builds a per-Pokemon registry, and bulk-exports assets in split format (static model + separate animation clips + textures + manifest).

## Requirements

- .NET SDK 9+
- Depends on `Spica.Core` (project reference)

## Build

```bash
dotnet build src/PokemonGreen.Spica/Spica.Registry/Spica.Registry.csproj
```

## Commands

### `scan`

Analyze a GARC file and produce a JSON registry mapping Pokemon IDs to their model/texture/animation entries.

```bash
dotnet run --project src/PokemonGreen.Spica/Spica.Registry/Spica.Registry.csproj -- scan "<garc-file>" [-o registry.json] [-n count]
```

Options:

- `-o, --output` — output JSON path (default: `registry.json`)
- `-n, --limit` — max entries to scan

The scanner reads each GARC entry, decompresses LZ11 if needed, identifies the format by magic number, and classifies it. Pokemon IDs are extracted from texture names in materials (pattern: `pm0001_00`). Model entries start new groups; subsequent texture and animation entries attach to the most recent model.

### `export`

Export assets from a GARC using a previously generated registry. Always produces split output (static model + separate clip DAEs).

```bash
dotnet run --project src/PokemonGreen.Spica/Spica.Registry/Spica.Registry.csproj -- export "<registry.json>" "<garc-file>" -o "<output-dir>"
```

For each Pokemon group in the registry, the exporter:

1. Loads the model entry to get the skeleton
2. Merges in texture entries
3. Merges in animation entries (using the skeleton for GFMotion decoding)
4. Writes textures to `textures/` as PNG
5. Writes static model DAE(s) with no baked animation (`animIdx=-1`)
6. Writes each skeletal animation as a clip-only DAE to `clips/`
7. Writes `manifest.json` with metadata for all models, textures, and clips

### `diag`

Diagnostic dump of the first Pokemon in a GARC. Prints skeleton bone hierarchy, animation element details, and mismatched bone/animation names. Useful for debugging export issues.

```bash
dotnet run --project src/PokemonGreen.Spica/Spica.Registry/Spica.Registry.csproj -- diag "<garc-file>"
```

## Output layout

```text
<output>/
  pm0001_00/
    model.dae                Static mesh + skeleton (no animation)
    model_lowpoly.dae        Low-poly variant (if present)
    textures/
      pm0001_00_BodyA1.png
      pm0001_00_Eye1.png
      ...
    clips/
      clip_000.dae           Clip-only DAE (skeleton + matrix keyframes, no mesh)
      clip_001.dae
      ...
    manifest.json
  pm0004_00/
    ...
```

### Manifest format

```json
{
  "version": 1,
  "pokemonId": "pm0001_00",
  "models": [
    { "file": "model.dae", "meshCount": 5, "boneCount": 51 },
    { "file": "model_lowpoly.dae", "meshCount": 3, "boneCount": 51 }
  ],
  "textures": [
    { "name": "pm0001_00_BodyA1", "file": "textures/pm0001_00_BodyA1.png", "width": 128, "height": 128 }
  ],
  "clips": [
    { "index": 0, "name": "Motion_0", "file": "clips/clip_000.dae", "frameCount": 42, "fps": 30 },
    { "index": 1, "name": "Motion_1", "file": "clips/clip_001.dae", "frameCount": 44, "fps": 30 }
  ]
}
```

Clip names come from the original 3DS animation data (`Motion_N` for battle models, `anim_N` for overworld). These are numeric slot IDs from the game engine, not semantic names.

### Clip DAE format

Each clip DAE contains:
- `<library_visual_scenes>` with the full bone hierarchy (no mesh geometry)
- `<library_animations>` with matrix channels targeting `{BoneName}_bone_id/transform`
- Each keyframe is a 4x4 matrix (16 floats, COLLADA row-major order)

This is the format consumed by:
- The game engine's `SkeletalModelData.LoadClip()` / `RegisterClip()`, which parses matrix channels and decomposes into translation/rotation/scale for interpolation
- Blender's COLLADA importer (see `tools/blender_import_clips.py` for batch import)

## Project layout

| File | Purpose |
|------|---------|
| `Program.cs` | CLI entry point — `scan`, `export`, and `diag` commands |
| `EntryClassifier.cs` | Identifies entry types by magic number, extracts Pokemon IDs, groups entries |
| `GARC.cs` | Reads GARC (Game ARChive) containers — parses FATO/FATB sections |
| `LZSS.cs` | Nintendo LZ11 decompression |

## Recognized formats

| Magic / Code | Type |
|---|---|
| `0x15122117` | GFModel (single model) |
| `0x15041213` | GFTexture |
| `0x00060000` | GFMotion (skeletal animation) |
| `0x00010000` | GFModelPack (multiple models) |
| `0x00484342` | BCH |
| `PC`, `PK`, `PB`, `BS`, `AD`, `PT`, `CM`, `MM`, `GR`, `BG` | GFPackage sub-types |

## Typical workflow

```bash
# 1. Scan a Pokemon model GARC to build the registry
dotnet run --project src/PokemonGreen.Spica/Spica.Registry/Spica.Registry.csproj -- \
  scan "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o registry.json -n 50

# 2. Export all detected Pokemon as split model + clips
dotnet run --project src/PokemonGreen.Spica/Spica.Registry/Spica.Registry.csproj -- \
  export registry.json "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o exports

# 3. Diagnose bone/animation issues for the first Pokemon
dotnet run --project src/PokemonGreen.Spica/Spica.Registry/Spica.Registry.csproj -- \
  diag "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4"
```

## Differences from SpicaCli

Both tools can export from GARC files, but serve different purposes:

| | SpicaCli | Spica.Registry |
|---|---|---|
| **Primary use** | Quick inspect/convert of any file | Bulk Pokemon extraction with registry |
| **Grouping** | Per-entry (default) or auto-grouped (`--split-model-anims`) | Registry-driven (scan then export) |
| **Split export** | Optional flag | Always split |
| **Registry** | None | JSON registry maps entries to Pokemon groups |
| **Diagnostics** | `info` command | `diag` command (bone/animation detail) |
| **Formats** | All GFPackage types + BCH + raw files | Pokemon-focused (PC packages, GFModel, GFTexture, GFMotion) |
