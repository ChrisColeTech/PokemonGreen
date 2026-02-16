# OhanaCli — Pokemon 3DS Model Converter

CLI tool for extracting and converting 3D models from Pokemon Sun/Moon (and X/Y, OR/AS) ROM dumps. Ported from [Ohana3DS-Rebirth](https://github.com/gdkchan/Ohana3DS-Rebirth) and modernized to .NET 8.

## What It Does

Reads GARC archives from a decrypted 3DS ROM dump, decompresses the entries (LZSS), parses the Game Freak / BCH model formats, and exports:
- **DAE** (Collada) or **OBJ** 3D model files
- **PNG textures** decoded from PICA200 GPU formats (ETC1, ETC1A4, RGBA8, etc.)

## Supported Formats

| Format | Magic | Description |
|--------|-------|-------------|
| GARC | `CRAG` | Nintendo archive container (holds all other formats) |
| GfModel | `0x00010000` | Game Freak model (Sun/Moon) |
| BCH | `BCH\0` | CTR model (X/Y, OR/AS) |
| PC | `PC` | Pokemon Container |
| GfTexture | `0x15041213` | Game Freak texture |
| AD, CM, CP, GR, MM, PT | 2-byte magic | Various Sun/Moon sub-formats |

## Commands

### `convert-all` — Bulk extract from ROM dump (recommended)

Recursively scans a directory for GARC archives and converts all models found.

```bash
dotnet run -- convert-all <input-dir> <output-dir> [--format dae|obj]
```

**Example — extract everything from a Sun/Moon dump:**
```bash
dotnet run -- convert-all "D:/Ohana3DS-Rebirth/sun-moon-dump/RomFS/a/" "D:/PokemonGreen/Assets/SunMoon/" --format dae
```

**Features:**
- Automatically finds all GARC archives by magic bytes
- Groups model entries with their following texture entries into single folders
- Names folders by Pokemon ID extracted from texture filenames (e.g. `pm0001_00/` for Bulbasaur)
- Separates shiny/variant textures into subfolders (`shiny/`, `variant_2/`)
- Writes a `report.txt` summary to the output directory

### `info` — Inspect a file

```bash
dotnet run -- info <file>
```

Dumps metadata about a model file or GARC container (entry count, mesh count, materials, textures).

### `convert` — Convert a single file

```bash
dotnet run -- convert <input> <output-dir> [--format dae|obj]
```

Converts a single model file or GARC archive. Does not group model+texture entries.

### `batch` — Convert all files in a directory

```bash
dotnet run -- batch <input-dir> <output-dir> [--format dae|obj]
```

Runs `convert` on every file in a directory tree.

## Sun/Moon ROM Structure

A decrypted Sun/Moon ROM dump has this structure:

```
sun-moon-dump/
  RomFS/
    a/
      0/9/4    ← 1.3GB GARC, ~10,500 entries: all Pokemon models + textures
      0/8/7    ← Pokemon alternate forms (Megas, Alolan, etc.)
      0/9/8    ← More Pokemon variants
      1/1/3    ← Map/field object models
      1/7/4    ← Overworld character models
      2/0/0    ← Field item models
      ...      ← 268 total GARC archives
```

### GARC Entry Grouping (Pokemon archive `a/0/9/4`)

Each Pokemon occupies ~9 consecutive GARC entries:

| Offset | Content |
|--------|---------|
| +0 | Config data (not a model) |
| +1 | **3D model** (meshes, skeleton, materials) |
| +2 | **Hi-res textures** (color, normals) — primary palette |
| +3 | **Hi-res textures** — shiny palette |
| +4 | **Low-res textures** (battle minimap) |
| +5 to +8 | Animations, additional data |

The `convert-all` command detects this pattern and groups entries automatically.

## Output Structure

```
Assets/SunMoon/
  0_9_4/                    ← GARC a/0/9/4 (main Pokemon)
    pm0001_00/              ← Bulbasaur
      model.dae             ← 3D mesh + skeleton
      pm0001_00_BodyA1.tga.png
      pm0001_00_Eye1.tga.png
      pm0001_00_Iris1.tga.png
      ...
      shiny/                ← Shiny color textures
        pm0001_00_BodyA1.tga.png
        ...
      variant_2/            ← Low-res textures
        pm0001_00_BodyA1.tga.png
        ...
    pm0006_51/              ← Mega Charizard X
      ...
  0_8_7/                    ← Battle effect models
    ...
  report.txt                ← Conversion summary
```

## Building

Requires .NET 8 SDK.

```bash
cd src/PokemonGreen.OhanaCli
dotnet build OhanaCli.sln
```

## Project Structure

```
OhanaCli.sln
src/
  OhanaCli.App/             ← CLI entry point (System.CommandLine)
    Program.cs              ← Commands: info, convert, batch, convert-all
  OhanaCli.Formats/         ← Format parsers (ported from Ohana3DS-Rebirth)
    Core/
      FileIO.cs             ← Master format dispatcher (magic byte detection)
      RenderBase.cs         ← Internal data structures (OModelGroup, OModel, OMesh, etc.)
      IOUtils.cs            ← Binary read helpers
      PatriciaTree.cs       ← PATRICIA tree for BCH name tables
    Models/
      BCH/BCH.cs            ← BCH format loader (X/Y, OR/AS)
      PocketMonsters/       ← GfModel, PC, CM, CP, GR, MM, AD loaders (Sun/Moon)
      PICA200/              ← GPU command buffer parsers
      GenericFormats/       ← DAE (Collada) and OBJ exporters
      Mesh/MeshUtils.cs     ← Vertex processing
    Textures/
      Codecs/TextureCodec.cs  ← All PICA200 texture decoders (14 formats)
      TextureUtils.cs       ← Tiling/swizzle helpers
      PocketMonsters/       ← GfTexture, AD, PT texture loaders
    Containers/
      GARC.cs               ← GARC archive reader
      OContainer.cs         ← Container data structure
      PkmnContainer.cs      ← Generic Pokemon container
    Compressions/
      LZSS_Ninty.cs         ← Nintendo LZSS decompression
      LZSS.cs, BLZ.cs       ← Other compression variants
```

## Known Issues

- **DAE SID uniqueness**: The Collada exporter uses the same `sid` for all material image surfaces, which can cause some importers to only apply the last texture. Needs unique SIDs per material.
- **Arithmetic overflow errors**: ~11K entries across all GARCs fail to parse — these are non-model data (animations, scripts, sound) that the parser attempts to read. They are skipped and logged.
- **No animation export**: Animation formats (GfMotion) are not yet implemented.
- **Texture naming**: Exported PNGs keep the original `.tga.png` double extension from the internal texture names.

## Dependencies

- [System.CommandLine](https://www.nuget.org/packages/System.CommandLine) — CLI framework
- [SixLabors.ImageSharp](https://www.nuget.org/packages/SixLabors.ImageSharp) — Image processing
- [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common) — Bitmap support (legacy, from original Ohana code)
