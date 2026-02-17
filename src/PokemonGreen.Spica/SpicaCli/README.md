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
- `--anim <index>` — animation index to bake into the model DAE (default: 0, use -1 for none)
- `--split-model-anims` — split export: static model DAE + separate clip DAEs + textures + manifest (incompatible with `--anim`)

#### Default mode (individual export)

Each GARC entry is exported independently. Models get a single animation baked in (controlled by `--anim`). This is the original behavior, useful for quick inspection or when you only need one animation per model.

#### Split mode (`--split-model-anims`)

Groups GARC entries by Pokemon (model entry starts a group, subsequent texture/animation entries attach to it) and exports each group as:

```
<output>/
  pm0001_00/
    model.dae              Static skeletal mesh (no baked animation, animIdx=-1)
    model_lowpoly.dae      Low-poly variant (if present)
    textures/
      pm0001_00_BodyA1.png
      pm0001_00_Eye1.png
      ...
    clips/
      clip_000.dae         Animation clip 0 (skeleton + matrix keyframes, no mesh)
      clip_001.dae         Animation clip 1
      ...
    manifest.json          Registry of all models, textures, and clips
```

Pokemon IDs are extracted from material texture names (pattern: `pm0001_00`). The manifest contains metadata for each clip (index, name, file path, frame count, fps).

**Clip DAE format**: Each clip DAE contains only the skeleton hierarchy and `<library_animations>` with matrix channels. Channel targets use the format `{BoneName}_bone_id/transform` with 4x4 matrix output (16 floats per keyframe, COLLADA row-major). No mesh geometry is included.

**Manifest schema**:

```json
{
  "version": 1,
  "pokemonId": "pm0001_00",
  "models": [
    { "file": "model.dae", "meshCount": 5, "boneCount": 51 }
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

# Default: export entries individually with baked animation
dotnet run --project src/PokemonGreen.Spica/SpicaCli/SpicaCli.csproj -- \
  convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o exports -n 20

# Split mode: per-Pokemon folders with static model + separate clip DAEs
dotnet run --project src/PokemonGreen.Spica/SpicaCli/SpicaCli.csproj -- \
  convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o exports-split --split-model-anims -n 20

# Convert a single file with a specific animation baked in
dotnet run --project src/PokemonGreen.Spica/SpicaCli/SpicaCli.csproj -- \
  convert "model.bch" -o exports --anim 2

# Convert a single file with no animation (static mesh only)
dotnet run --project src/PokemonGreen.Spica/SpicaCli/SpicaCli.csproj -- \
  convert "model.bch" -o exports --anim -1
```

## Output

**Default mode**: Models are written as `.dae` files with the selected animation baked in. Textures are exported as `.png` alongside the model files.

**Split mode**: Per-Pokemon directories with `model.dae` (static), `clips/clip_NNN.dae` (animation-only), `textures/*.png`, and `manifest.json`. This is the format consumed by the game engine's `PokemonModelLoader`, which reads the manifest and lazy-loads clips on demand.
