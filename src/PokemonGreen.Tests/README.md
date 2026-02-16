# PokemonGreen.Tests - Sun/Moon ROM Dump & Export Guide

## Dump Directory Structure

The ROM dump is located at:
```
sun-moon-dump/
```

### Top-Level Layout

```
sun-moon-dump/
  ExeFS/                        # Executable filesystem
    banner.bin                  # Banner data (~586 KB)
    code.bin                    # Game executable (~5.6 MB)
    icon.bin                    # Icon data (~14 KB)
  ExHeader.bin                  # Extended header (2 KB)
  ExtractedBanner/              # Extracted banner assets
    banner.bcwav                # Banner audio
    banner.cbmd                 # Banner model descriptor
    banner.cgfx                 # Banner graphics
    banner1-16.bcmdl            # Banner 3D models (~97 KB each)
  HeaderExeFS.bin               # ExeFS header (512 bytes)
  HeaderNCCH0.bin               # NCCH header (512 bytes)
  LogoLZ.bin                    # Compressed logo (8 KB)
  RomFS/                        # Read-only filesystem (game data)
    .crr/
      static.crr                # Code relocation resource (32 KB)
    a/                          # GARC archive files (numbered)
      0/ through 3/             # 313 total files across 32 subdirectories
    data/
      sound/                    # Audio files
        niji_sound.bcsar        # Sound archive (~33 MB)
        bgm_*.bcstm             # Background music (bcstm streams)
        me_*.bcstm              # Music effects
        strm_*.bcstm            # Streaming audio
    m/                          # Movie files
      title_*.moflex            # Title screen movies by language (~19-20 MB each)
    *.cro                       # Code overlay modules (game subsystems)
    static.crs                  # Static code relocation (~380 KB)
```

### RomFS/a/ - GARC Archive Files

The `a/` directory contains numbered GARC container files organized as `a/{major}/{minor}/{index}`. Each subdirectory holds 10 files (except `a/3/3/` with 3). Total: 313 GARC files.

Key large GARC files by size:

| Path | Size | Entries | Content |
|------|------|---------|---------|
| `a/0/9/4` | **1.3 GB** | 10,549 | **Pokemon 3D models, textures, animations** |
| `a/0/8/2` | 461 MB | 3,696 | Container (binary data) |
| `a/0/8/7` | 171 MB | 7,205 | Container (binary data) |
| `a/0/9/9` | 140 MB | - | Container |
| `a/1/7/4` | 110 MB | - | Container |
| `a/0/8/6` | 74 MB | - | Container |
| `a/2/0/0` | 72 MB | - | Container |

## Pokemon Model GARC

**File:** `sun-moon-dump/RomFS/a/0/9/4`
**Full path:** `D:/Projects/PokemonGreen/src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4`

- Format: GARC container
- Size: 1,334,871,316 bytes (1.3 GB)
- Entries: 10,549
- Each Pokemon uses approximately 9 consecutive entries:
  - Entry group structure: model BCH, shiny textures BCH, high-res textures BCH, animation data, metadata
  - Entry 0: header/padding (small, ~5 KB)
  - Entry 1: Bulbasaur (pm0001_00) - first Pokemon model
  - Entry 10: Ivysaur (pm0002_00) - second Pokemon model
  - Entry 19: Venusaur (pm0003_00) - third Pokemon model
  - Pattern continues for all ~800+ Pokemon forms

### Exported File Types

When converted, each Pokemon entry produces:

- **model.dae** / **model_1.dae** - COLLADA 3D models with:
  - Multiple meshes (6-9 per model)
  - Full skeleton (40-65 bones)
  - Material references to textures
  - model.dae = standard resolution model
  - model_1.dae = alternate/battle model variant
- **pm{NNNN}_00_BodyA1.tga.png** - Body texture layer A, channel 1
- **pm{NNNN}_00_BodyA2.tga.png** - Body texture layer A, channel 2
- **pm{NNNN}_00_BodyANor.tga.png** - Body normal map A
- **pm{NNNN}_00_BodyB1.tga.png** - Body texture layer B, channel 1
- **pm{NNNN}_00_BodyB2.tga.png** - Body texture layer B, channel 2
- **pm{NNNN}_00_BodyBNor.tga.png** - Body normal map B
- **pm{NNNN}_00_Eye1.tga.png** - Eye texture channel 1
- **pm{NNNN}_00_Eye2.tga.png** - Eye texture channel 2
- **pm{NNNN}_00_EyeNor.tga.png** - Eye normal map
- **pm{NNNN}_00_Iris1.tga.png** - Iris texture channel 1
- **pm{NNNN}_00_Iris2.tga.png** - Iris texture channel 2

Textures are exported as PNG. Sizes vary: 64x64 (iris), 128x256 (eye/body detail), 256x256 (body main), 512x512 (high-res variants).

## CLI Commands

All commands are run from the repository root (`D:/Projects/PokemonGreen`).

### Info Command

Inspect a GARC file to see its format and entry count:

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- info "D:/Projects/PokemonGreen/src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" 2>&1
```

Output shows:
```
File: D:/Projects/.../RomFS/a/0/9/4
Size: 1,334,871,316 bytes
Format: container
  Entries: 10549
    [0] 5,576 bytes  .bin
    [1] 205,889 bytes  .bin
    ...
```

### Convert Command

Export Pokemon models and textures from the GARC:

```bash
# Export first 5 entries (quick test)
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- convert "D:/Projects/PokemonGreen/src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o "D:/Projects/PokemonGreen/src/PokemonGreen.Tests/exports" -n 5 2>&1

# Export first 20 entries (~2-3 Pokemon)
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- convert "D:/Projects/PokemonGreen/src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o "D:/Projects/PokemonGreen/src/PokemonGreen.Tests/exports" -n 20 2>&1

# Export all entries (WARNING: very large, ~1.3 GB source)
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- convert "D:/Projects/PokemonGreen/src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o "D:/Projects/PokemonGreen/src/PokemonGreen.Tests/exports" 2>&1
```

**Important:** Use `2>&1` to capture both stdout and stderr. Animation diagnostic info is written to stderr.

### Convert Output

Example output for `-n 20`:
```
Container with 10549 entries. Processing 20...
  Texture: .../entry_1/pm0001_00_BodyA1.tga.png (256x256)
  Texture: .../entry_1/pm0001_00_BodyA2.tga.png (256x256)
  ...
  Animation: 40 Euler, 0 Quaternion, 0 BakedMatrix (skipped), 0 unmatched
  Model: .../entry_1/model.dae (9 meshes, 55 bones, anim=0)
  Model: .../entry_1/model_1.dae (6 meshes, 55 bones, anim=0)
  ...
  Animation: 39 Euler, 0 Quaternion, 0 BakedMatrix (skipped), 0 unmatched
  Model: .../entry_10/model.dae (7 meshes, 45 bones, anim=0)
```

The animation diagnostic line shows:
- **Euler**: Number of bones with Euler rotation animations
- **Quaternion**: Number of bones with quaternion rotation animations
- **BakedMatrix**: Number of bones with baked matrix animations (currently skipped)
- **unmatched**: Animated bones that could not be matched to the skeleton

## Exports Folder

After running `-n 20`, the exports folder contains:

```
exports/
  entry_1/                      # Bulbasaur (pm0001_00)
    model.dae                   # 3D model (9 meshes, 55 bones)
    model_1.dae                 # Alternate model (6 meshes, 55 bones)
    pm0001_00_BodyA1.tga.png    # Body texture A channel 1 (256x256)
    pm0001_00_BodyA2.tga.png    # Body texture A channel 2
    pm0001_00_BodyANor.tga.png  # Body normal map A
    pm0001_00_BodyB1.tga.png    # Body texture B channel 1 (128x256)
    pm0001_00_BodyB2.tga.png    # Body texture B channel 2
    pm0001_00_BodyBNor.tga.png  # Body normal map B
    pm0001_00_Eye1.tga.png      # Eye texture channel 1
    pm0001_00_Eye2.tga.png      # Eye texture channel 2
    pm0001_00_EyeNor.tga.png    # Eye normal map
    pm0001_00_Iris1.tga.png     # Iris texture channel 1
    pm0001_00_Iris2.tga.png     # Iris texture channel 2
  entry_10/                     # Ivysaur (pm0002_00)
    model.dae                   # 3D model (7 meshes, 45 bones)
    model_1.dae                 # Alternate model (4 meshes, 45 bones)
    pm0002_00_BodyA1.tga.png    # + 11 more textures
    ...
  entry_19/                     # Venusaur (pm0003_00)
    model.dae                   # 3D model (7 meshes, 63 bones)
    model_1.dae                 # Alternate model (5 meshes, 63 bones)
```

Total: 28 files across 3 Pokemon entries (13 + 13 + 2 files).
