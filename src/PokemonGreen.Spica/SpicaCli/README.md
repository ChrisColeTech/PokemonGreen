# SpicaCli

General-purpose CLI for inspecting and converting Pokemon 3DS game assets. Reads GARC containers, GFPackage files, and individual model/texture/animation files, then exports to DAE (COLLADA) and PNG.

## Requirements

- .NET SDK 9+
- Depends on `Spica.Core` (project reference)

## Build

```bash
dotnet build src/PokemonGreen.Spica/SpicaCli/SpicaCli.csproj
```

## Commands

### `info`

Inspect a file and print detected format, model/texture/animation counts, and basic metadata.

```bash
dotnet run --project src/PokemonGreen.Spica/SpicaCli/SpicaCli.csproj -- info "<file>"
```

For GARC containers, shows entry count and the first 10 entries with sizes. For individual files, lists models (with mesh/bone counts), textures (with dimensions), and animation counts.

### `convert`

Convert a file or GARC container to DAE + PNG.

```bash
dotnet run --project src/PokemonGreen.Spica/SpicaCli/SpicaCli.csproj -- convert "<file>" -o "<output-dir>" [options]
```

Options:

- `-o <dir>` — output directory (required)
- `-n <count>` — limit processing to first N GARC entries
- `--anim <index>` — animation index to export (default: 0, use -1 for none)

The converter decompresses LZ11 entries, identifies formats, exports models as DAE with skeletal animation baked in, and writes textures as PNG. Skeleton data is tracked across entries so animations from later entries can reference earlier models.

## Supported formats

SpicaCli identifies formats by magic number or file extension and routes to the appropriate handler:

| Handler | Magic / Extension | Description |
|---|---|---|
| GFPkmnModel | `PC` | Pokemon models — high-poly, low-poly, shaders, textures, motion |
| GFPkmnSklAnim | `PK`, `PB` | Pokemon skeletal animation packs |
| GFBtlSklAnim | `BS` | Battle skeletal + material animations |
| GFCharaModel | `CM` | Character models with animations and textures |
| GFOWCharaModel | `MM` | Overworld character models (BCH format) |
| GFOWMapModel | `GR` | Overworld map models |
| GFL2OverWorld | `BG` | Map background model groups |
| GFPackedTexture | `PT`, `AD` | Packed BCH textures |
| GARC | `CRAG` | Game ARChive container |
| BCH | `0x00484342` | CTR H3D binary format |
| — | `.smd`, `.obj`, `.mbn`/`.bch` | Direct file import by extension |

## Project layout

```text
SpicaCli/
  Program.cs              CLI entry point (info + convert commands)
  SpicaCli.csproj
  Formats/
    FormatIdentifier.cs   Central format dispatcher
    GARC.cs               GARC container reader
    LZSS.cs               LZ11 decompression
    GFPackage.cs           Generic GF package reader
    GFPkmnModel.cs        Pokemon model handler
    GFPkmnSklAnim.cs      Skeletal animation handler
    GFBtlSklAnim.cs       Battle animation handler
    GFCharaModel.cs       Character model handler
    GFOWCharaModel.cs     Overworld character model handler
    GFOWMapModel.cs       Overworld map model handler
    GFL2OverWorld.cs       Map background handler
    GFPackedTexture.cs    Texture handler
```

## Typical workflow

```bash
# Inspect a GARC to see what it contains
dotnet run --project src/PokemonGreen.Spica/SpicaCli/SpicaCli.csproj -- \
  info "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4"

# Convert the first 20 entries to DAE + PNG
dotnet run --project src/PokemonGreen.Spica/SpicaCli/SpicaCli.csproj -- \
  convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o exports -n 20

# Convert a single file with a specific animation
dotnet run --project src/PokemonGreen.Spica/SpicaCli/SpicaCli.csproj -- \
  convert "model.bch" -o exports --anim 2
```

## Output

Models are written as `.dae` files with the selected animation baked in. Textures are exported as `.png` alongside the model files. For GARC containers, each entry gets its own subdirectory.
