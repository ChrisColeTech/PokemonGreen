# SwitchToolboxCli

A modern .NET 8 CLI tool for extracting and converting game assets from Nintendo Switch and 3DS formats. Designed as a lightweight, scriptable alternative to Switch Toolbox desktop application.

## Features

- **Archive Extraction**: Extract files from GARC, NCSD, NCCH, RomFS, TRPAK, and TRPFS archives
- **Model Export**: Convert 3D models to OBJ or DAE (Collada) format
- **Texture Export**: Extract texture files (BNTX, etc.)
- **Animation Export**: Extract animation clips as DAE or raw formats
- **Trinity Format Support**: Parse Pokemon Scarlet/Violet Trinity format files (TRPAK, TRPFS, TRPFD, TRMDL, TRMSH, TRMBF, TRSKL, TRANM, etc.)
- **Memory Efficient**: Streaming architecture handles multi-gigabyte archives without OOM

## Requirements

- .NET 8 SDK
- **Oodle DLL** (required for TRPAK decompression): Set `PG_OO2CORE_PATH` environment variable to the path of `oo2core_8_win64.dll`

## Installation

```bash
cd D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli
dotnet build -c Release
```

The executable will be at `src/PokemonGreen.SwitchToolboxCli.App/bin/Release/net8.0/PokemonGreen.SwitchToolboxCli.App.exe`

## Commands

### info

Display information about a file's format structure.

```bash
dotnet run -- info <input>
```

### scan

Scan an archive and generate a JSON report of detected formats.

```bash
dotnet run -- scan <input> -o <report.json>
```

### extract-garc

Extract all files from a GARC archive.

```bash
dotnet run -- extract-garc <input.garc> -o <outputDir>
```

### extract-bins

Extract all binary files from any supported archive format.

```bash
dotnet run -- extract-bins <input> -o <outputDir>
```

### bulk-extract-bins

Extract binaries from multiple archives in a directory.

```bash
dotnet run -- bulk-extract-bins <inputDir> -o <outputDir> [--limit N] [--resume true|false]
```

Options:
- `--limit N`: Process only the first N archives
- `--resume true|false`: Skip already processed archives (default: false)

### batch

Full extraction pipeline: extract binaries, textures, models, and clips.

```bash
dotnet run -- batch <input> -o <outputDir> [--model-format obj|dae]
```

### convert

Convert archive contents to usable formats (models, textures, clips).

```bash
dotnet run -- convert <input> -o <outputDir> --model-format obj|dae [--extract true|false]
```

Options:
- `--model-format obj|dae`: Output format for models (required)
- `--extract true|false`: Also extract raw binaries (default: false)

### bulk-convert

Convert multiple archives in a directory.

```bash
dotnet run -- bulk-convert <inputDir> -o <outputDir> --model-format obj|dae [--limit N] [--resume true|false]
```

Options:
- `--model-format obj|dae`: Output format for models (required)
- `--limit N`: Process only the first N archives
- `--resume true|false`: Skip already processed archives (default: false)

## Supported Formats

### Archives
| Format | Extension | Description |
|--------|-----------|-------------|
| GARC | `.garc` | Pokemon game archives |
| NCSD | `.3ds`, `.cci` | Nintendo 3DS card images |
| NCCH | `.cxi`, `.cfa` | Nintendo 3DS content |
| RomFS | `.romfs` | Read-only filesystem |
| TRPAK | `.trpak` | Trinity package (Pokemon S/V) |
| TRPFS | `.trpfs`, `.trpfd` | Trinity filesystem (Pokemon S/V) |

### Trinity Components
| Format | Extension | Description |
|--------|-----------|-------------|
| TRMDL | `.trmdl` | Model definition |
| TRMMT | `.trmmt` | Model container |
| TRMDT | `.trmdt` | Model data container |
| TRMSH | `.trmsh` | Mesh data |
| TRMBF | `.trmbf` | Blend shape buffer |
| TRSKL | `.trskl` | Skeleton |
| TRMTR | `.trmtr` | Material |
| BNTX | `.bntx` | Texture |
| TRANM | `.tranm` | Animation clip |
| TRAEF* | `.traef`, `.tracm`, `.tracs`, `.tracl`, `.tracr`, `.tracp` | Control/effect clips |

### Export Formats
- **Models**: OBJ, DAE (Collada)
- **Animations**: DAE (Collada), raw binary
- **Textures**: Raw BNTX

## Output Structure

When running `convert`, the output directory structure is:

```
output/
├── manifest.json          # Complete extraction manifest
├── extracted/             # Raw binary files (if --extract true)
├── models/                # Converted models
│   └── <archive>/<bundle>/
│       ├── model.dae
│       └── ...
├── textures/              # Extracted textures
│   └── <archive>/<bundle>/
│       └── texture.bntx
└── clips/                 # Animation clips
    └── <archive>/<bundle>/
        └── animation.dae
```

## Examples

### Extract from Pokemon Scarlet/Violet

```bash
# Set Oodle path
export PG_OO2CORE_PATH=/path/to/oo2core_8_win64.dll

# Convert data.trpfs to DAE models
dotnet run -- convert data.trpfs -o ./output --model-format dae

# Full extraction with raw binaries
dotnet run -- convert data.trpfs -o ./output --model-format dae --extract true
```

### Process 3DS ROM

```bash
# Extract NCSD card image
dotnet run -- extract-bins game.3ds -o ./extracted

# Convert models
dotnet run -- convert game.3ds -o ./output --model-format obj
```

### Bulk Processing

```bash
# Process all archives in a directory with resume support
dotnet run -- bulk-convert ./archives -o ./output --model-format dae --resume true
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `PG_OO2CORE_PATH` | Path to Oodle DLL for TRPAK decompression |
| `PG_TRINITY_CORPUS` | Path to test corpus (for integration tests) |

## Architecture

```
PokemonGreen.SwitchToolboxCli/
├── src/
│   ├── PokemonGreen.SwitchToolboxCli.App/          # CLI entry point, commands
│   ├── PokemonGreen.SwitchToolboxCli.Core/         # Core abstractions, services
│   └── PokemonGreen.SwitchToolboxCli.Formats/      # Format parsers, exporters
└── tests/
    └── PokemonGreen.SwitchToolboxCli.Tests/        # Unit tests
```

### Key Components

- **FormatRegistry**: Maps file extensions and magic bytes to parsers
- **FileLoader**: Detects and loads files using registered formats
- **ArchiveWalker**: Recursively walks nested archives
- **DiscoveryService**: Orchestrates format detection
- **ArchiveExtractionService**: Extracts entries to disk
- **TrinityModelAssemblyService**: Assembles Trinity bundles into exportable models

## Testing

```bash
dotnet test
```

## License

MIT
