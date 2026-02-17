# Spica.Registry

Scans Pokemon GARC archives, classifies entries (models, textures, animations), builds a per-Pokemon registry, and bulk-exports assets to DAE + PNG.

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

Export assets from a GARC using a previously generated registry.

```bash
dotnet run --project src/PokemonGreen.Spica/Spica.Registry/Spica.Registry.csproj -- export "<registry.json>" "<garc-file>" -o "<output-dir>"
```

Creates per-Pokemon directories containing:

- Model DAE files (high-poly and low-poly when available)
- Textures as PNG
- One DAE per skeletal animation clip (e.g., `anim_000_GFMotion.dae`)

## Project layout

| File | Purpose |
|------|---------|
| `Program.cs` | CLI entry point — `scan` and `export` commands |
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

## Output layout

```text
<output>/
  pm0001_00/
    model_0.dae
    model_1.dae
    texture_BodyA1.png
    anim_000_GFMotion.dae
    anim_001_GFMotion.dae
  pm0004_00/
    ...
```

## Typical workflow

```bash
# 1. Scan a Pokemon model GARC
dotnet run --project src/PokemonGreen.Spica/Spica.Registry/Spica.Registry.csproj -- \
  scan "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o registry.json -n 50

# 2. Export all detected Pokemon
dotnet run --project src/PokemonGreen.Spica/Spica.Registry/Spica.Registry.csproj -- \
  export registry.json "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o exports
```
