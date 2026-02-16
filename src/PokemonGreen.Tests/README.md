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

Key GARC files:

| Path | Size | Entries | Content |
|------|------|---------|---------|
| `a/0/9/4` | **1.3 GB** | 10,549 | **Pokemon 3D battle models**, textures, animations |
| `a/0/8/2` | 461 MB | 3,696 | Container (binary data) |
| `a/0/8/7` | 171 MB | 7,205 | Container (binary data) |
| `a/0/9/9` | 140 MB | - | Container |
| `a/1/7/4` | **110 MB** | 316 | **Battle character models** (trainers, player, objects — HD) |
| `a/0/8/6` | 74 MB | - | Container |
| `a/2/0/0` | **71 MB** | 604 | **Field character models** (trainers, player, Pokemon, objects — low-poly `_fi`) |
| `a/0/6/1` | 248 KB | 769 | Item sprites (2D BFLIM, not 3D models) |

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

When converted to DAE, each Pokemon entry produces:

- **model.anim_000.dae**, **model.anim_001.dae**, ... (and `model_1.anim_000.dae`, etc.) when `-a` is not specified and `--consolidate-animations` is not set:
  - One DAE per skeletal clip (default behavior)
  - Deterministic naming with zero-padded animation index
  - No silent animation omission when clips are present in the processed entry range
- **model.dae** / **model_1.dae** when `--consolidate-animations` is specified (without `-a`):
  - One DAE per model containing all skeletal clips consolidated in a single file
- **model.dae** / **model_1.dae** when `-a <index>` is specified:
  - Single-clip DAE export using the selected animation index
  - `-a` takes precedence over `--consolidate-animations`
- All DAE files include:
  - Multiple meshes (6-9 per model)
  - Full skeleton (40-65 bones)
  - Skeletal animation channels when clips are exported
  - Material references to textures
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

## Battle Character Models GARC

**File:** `sun-moon-dump/RomFS/a/1/7/4`
**Full path:** `D:/Projects/PokemonGreen/src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/1/7/4`

- Format: GARC container
- Size: 114,500,600 bytes (~109 MB)
- Entries: 316 total, 292 contain 3D models
- High-detail models used in battle scenes

### Content

- **Player base models** (entries 0-1): `p1_base.dae`, `p2_base.dae` (male/female)
- **Trainer battle models** (~126): `tr####_00.dae` — high-poly trainers shown during battle
  - e.g. `tr0001_00.dae` through `tr1010_00.dae`
  - Variants: `tr####_01.dae`, `tr####_02.dae` etc.
- **Object/prop models** (~73): `ob####_00.dae` — Pokeballs, battle props, NPC objects
  - e.g. `ob0004_00.dae` through `ob0301_00.dae`

### Naming Patterns

| Prefix | Meaning |
|--------|---------|
| `tr####_##` | Trainer class model + variant number |
| `ob####_##` | Object/prop model |
| `p1` / `p2` | Male / female player character |

## Field Character Models GARC

**File:** `sun-moon-dump/RomFS/a/2/0/0`
**Full path:** `D:/Projects/PokemonGreen/src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/2/0/0`

- Format: GARC container
- Size: 74,460,408 bytes (~71 MB)
- Entries: 604 total, 373 contain 3D models
- Low-poly overworld models (suffix `_fi` = field)

### Content

- **Player base models**: `p1_base_fi.dae`, `p2_base.dae`
- **Trainer field models** (~186): `tr####_00_fi.dae` — low-poly overworld trainers
  - Includes `tr0000_00_dummy_fi` through `tr1052_00_fi`
- **Object field models** (~73): `ob####_00_fi.dae` — overworld props
- **Pokemon field models** (~103): `pm####_##_fi.dae` — Pokemon visible in the overworld
  - e.g. `pm0006_00_fi` (Charizard ride), `pm0128_00_fi` (Tauros ride)
- **Terrain interaction models** (~8): `it####_00_*` — grass, sand, soil, water, ride rocks

### Player Customization GARCs

Character customization parts are split across many smaller GARCs by body part and gender:

**Player 1 (male) — HD battle:**
`a/1/7/3` (hair), `a/1/7/5` (backpacks), `a/1/7/6` (face), `a/1/7/7` (hairstyles), `a/1/7/8` (hats), `a/1/7/9` (bags), `a/1/8/1` (bottoms), `a/1/8/2` (caps), `a/1/8/3` (legs), `a/1/8/4` (shoes), `a/1/8/5` (ride), `a/1/8/6` (tops)

**Player 2 (female) — HD battle:**
`a/1/8/8` (face), `a/1/8/9` (hair), `a/1/9/2` (bags), `a/1/9/4` (bottoms), `a/1/9/5` (hats), `a/1/9/6` (legs), `a/1/9/7` (shoes), `a/1/9/8` (ride), `a/1/9/9` (tops)

Overworld customization variants follow in `a/2/0/X`, `a/2/1/X`, `a/2/2/X` with `_fi` suffixed models.

## Item Sprites

**There are no dedicated 3D item model GARCs.** In-game items (Potions, Pokeballs, TMs, etc.) are 2D sprites.

**File:** `sun-moon-dump/RomFS/a/0/6/1`
- Size: 254,012 bytes (~248 KB)
- Entries: 769 (one per item)
- Format: BFLIM (2D sprite images) — not exportable by OhanaCli
- Physical items in the overworld (e.g. Pokeball pickups) use `ob####` object models from `a/1/7/4` and `a/2/0/0`

## CLI Commands

All commands are run from the repository root (`D:/Projects/PokemonGreen`).

Current command surface:

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- --help
```

Commands:
- `info <file>`
- `convert <file>`
- `batch <inputDir>`
- `diagnose <file>`

### Info Command

Inspect a file and print detected type and summary:

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- info "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4"
```

Example output:

```
file=D:\Projects\PokemonGreen\src\PokemonGreen.Tests\sun-moon-dump\RomFS\a\0\9\4
detectedType=container
entries=10549
```

### Convert Command

Export models/textures from the GARC:

```bash
# Quick smoke (exports all skeletal clips by default for DAE)
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o "src/PokemonGreen.Tests/exports-allanims" -n 20

# Consolidated DAE (one DAE per model with all clips)
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o "src/PokemonGreen.Tests/exports-consolidated" -n 20 --consolidate-animations

# Single animation clip only (index 0)
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o "src/PokemonGreen.Tests/exports-singleanim" -n 20 -a 0

# Export OBJ instead of DAE
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o "src/PokemonGreen.Tests/exports-obj" -f obj -n 20

# Enable animation diagnostics (writes to stderr)
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- convert "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" -o "src/PokemonGreen.Tests/exports" -n 20 --diag-anim 2>&1
```

Useful options:
- `-o, --output` (required)
- `-f, --format` (`dae` or `obj`, default `dae`)
- `-a, --animation-index` (optional; selects one clip and overrides `--consolidate-animations`)
- `--consolidate-animations` (optional; DAE only; emits one DAE per model with all clips)
- `-n, --limit` (max container entries to inspect)
- `--diag-anim` (per-bone animation diagnostics)

Important behavior notes:
- DAE default (no `-a`, no `--consolidate-animations`): all skeletal clips are exported (`*.anim_###.dae`).
- DAE with `--consolidate-animations` (and no `-a`): one consolidated file per model (`model.dae`, `model_1.dae`, ...).
- DAE with `-a`: one clip is exported to the legacy name (`model.dae`, `model_1.dae`, ...), and `-a` overrides consolidation.
- OBJ: static-only export (no animation), textures still exported as PNG.
- If `--limit` is too small to include animation entries, the CLI emits an explicit warning telling you to raise/remove `--limit`.

Example summary output:

```
convert summary: groupsTotal=1 groupsSucceeded=1 groupsFailed=0 models=2 textures=11 clipsFound=1 clipsExported=1 clipsSkipped=0 out=D:\Projects\PokemonGreen\src\PokemonGreen.Tests\exports-allanims\4
```

### Diagnose Command

Inspect a range of GARC entries with per-entry type/model/mesh/texture details:

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- diagnose "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a/0/9/4" --start 0 --end 5
```

Output includes:
- Entry index and detected type
- `models`, `meshes`, `textures`
- `segmentSummary` (`euler/quaternion/matrix/axisAngle`) when animation data is present
- Final `diagnose summary` aggregate line

### Batch Command

Run conversion over every file in a directory tree:

```bash
dotnet run --project src/PokemonGreen.OhanaCli/src/OhanaCli.App/OhanaCli.App.csproj -- batch "src/PokemonGreen.Tests/sun-moon-dump/RomFS/a" -o "src/PokemonGreen.Tests/exports-batch" -f dae -n 20
```

Batch output ends with:

```
batch summary: totalFiles=... succeeded=... partial=... fatal=... failedTotal=...
```

Exit codes:
- `0` success
- `2` partial failure
- `1` fatal failure

## Exports Folder

The current converter writes into a folder named after the input file under the chosen output root.

Example for input `.../RomFS/a/0/9/4` and output root `exports/`:

```
exports/
  4/
    0000_model/
      model.anim_000.dae
      model_1.anim_000.dae
      pm0001_00_BodyA1.tga.png
      pm0001_00_BodyA2.tga.png
      ...
```

Notes:
- Group folders are generated from grouped container entries (`0000_*`, `0001_*`, ...).
- Textures are exported as `.png` files.
a/0/0/0 - 
a/0/0/1 - 
a/0/0/2 - 
a/0/0/3 - 
a/0/0/4 - 
a/0/0/5 - 
a/0/0/6 - 
a/0/0/7 - 
a/0/0/8 - 
a/0/0/9 - 
a/0/1/0 - 
a/0/1/1 - Move data
a/0/1/2 - Egg moves
a/0/1/3 - Level up moves
a/0/1/4 - Evolution table
a/0/1/5 - Mega evolution table
a/0/1/6 - 8 * 404 bytes
a/0/1/7 - Pokemon data
a/0/1/8 - 786 * 2bytes
a/0/1/9 - Item data
a/0/2/0 - 921 bytes (bitflags, used sparingly - items)
a/0/2/1 - 
a/0/2/2 - 
a/0/2/3 - 
a/0/2/4 - 
a/0/2/5 - Fonts
a/0/2/6 - White fade anim ALYT
a/0/2/7 - Map Fade (ALYT)
a/0/2/8 - 
a/0/2/9 - 
a/0/3/0 - Game text - Japanese
a/0/3/1 - Game text - Japanese Kanji
a/0/3/2 - Game text - English
a/0/3/3 - Game text - French
a/0/3/4 - Game text - Italian
a/0/3/5 - Game text - German
a/0/3/6 - Game text - Spanish
a/0/3/7 - Game text - Korean
a/0/3/8 - Game text - Simplified chinese
a/0/3/9 - Game text - Traditional Chinese
a/0/4/0 - Story Text - Japanise
a/0/4/1 - Story Text - Unused
a/0/4/2 - Story Text - English
a/0/4/3 - Story Text - French
a/0/4/4 - Story Text - Italian
a/0/4/5 - Story Text - German
a/0/4/6 - Story Text - Spanish
a/0/4/7 - Story Text - Korean
a/0/4/8 - Story Text - Simplified Chinese
a/0/4/9 - Story Text - Traditional Chinese
a/0/5/0 - 
a/0/5/1 - 
a/0/5/2 - 
a/0/5/3 - 
a/0/5/4 - 
a/0/5/5 - 
a/0/5/6 - 
a/0/5/7 - 
a/0/5/8 - 
a/0/5/9 - 
a/0/6/0 - the, An (language prefixes)
a/0/6/1 - Item sprites
a/0/6/2 - Pokemon sprites
a/0/6/3 - 
a/0/6/4 -
a/0/6/5 -
a/0/6/6 - Field Bag ALYT (bflim, bflan)
a/0/6/7 - Battle UI (multilang) lower screen? (includes battle video)
a/0/6/8 - Battle UI (multilang) upper screen sprites (primal, mega)
a/0/6/9 - Common UI elements
a/0/7/0 - Common UI elements …?
a/0/7/1 - Common UI elements …???
a/0/7/2 - Skybox Model
a/0/7/3 - CSEQ/CWAR
a/0/7/4 - Common UI Windows
a/0/7/5 - Common UI Finger icon
a/0/7/6 - ZONEDATA Master table
a/0/7/7 - 
a/0/7/8 - Battle plates
a/0/7/9 - More UI (ribbons)
a/0/8/0 - Battle Tree Models
a/0/8/1 - ?Encounter Data (Maps) [EA - two files, day/night], NPC models, Map textures, etc
a/0/8/2 - ?Encounter table (Sun)
a/0/8/3 - ?Encounter table (Moon)
a/0/8/4 - 
a/0/8/5 - Map Models
a/0/8/6 - Overworld Models
a/0/8/7 - Z-move visuals + trainer battle sprites + anims
a/0/8/8 -
a/0/8/9 -
a/0/9/0 -
a/0/9/1 -
a/0/9/2 -
a/0/9/3 -
a/0/9/4 - Pokemon battle models
a/0/9/5 -
a/0/9/6 -
a/0/9/7 -
a/0/9/8 - Cutscene models
a/0/9/9 -
a/1/0/0 - 
a/1/0/1 - ?186 * Trainer class (20byte, 0x02: Ball Used)
a/1/0/2 - ?8 * Trainer data (20byte)
a/1/0/3 - ?8 * trpoke (32byte, 0x00: Ability/Gender, 0x01: Nature, 0x02-0x07: EVs, 0x08-0x0B: IVs)
a/1/0/4 - ?Trainer class
a/1/0/5 - ?Trainer data
a/1/0/6 - ?Trainer's Pokemon table
a/1/0/7 - 
a/1/0/8 - 
a/1/0/9 - ride effects + ???
a/1/1/0 - 
a/1/1/1 - 
a/1/1/2 - 
a/1/1/3 - 
a/1/1/4 - 
a/1/1/5 - 
a/1/1/6 - 
a/1/1/7 - 
a/1/1/8 - 
a/1/1/9 - 
a/1/2/0 - 
a/1/2/1 - 
a/1/2/2 - Z-crystal sprites
a/1/2/3 - 
a/1/2/4 - 
a/1/2/5 - 
a/1/2/6 - 
a/1/2/7 - 
a/1/2/8 - 
a/1/2/9 - 
a/1/3/0 - 
a/1/3/1 - 
a/1/3/2 - 
a/1/3/3 - 
a/1/3/4 - 
a/1/3/5 - 
a/1/3/6 - 
a/1/3/7 - 
a/1/3/8 - 
a/1/3/9 - 
a/1/4/0 - 
a/1/4/1 - 
a/1/4/2 - 
a/1/4/3 - 
a/1/4/4 - 
a/1/4/5 - 
a/1/4/6 - 
a/1/4/7 - 
a/1/4/8 - 
a/1/4/9 - 
a/1/5/0 - 
a/1/5/1 - 
a/1/5/2 - 
a/1/5/3 - 
a/1/5/4 - NPC face sprites
a/1/5/5 - Static encounter table
a/1/5/6 - 
a/1/5/7 - 
a/1/5/8 - 
a/1/5/9 - Rotom map images
a/1/6/0 - 
a/1/6/1 - 
a/1/6/2 - Map models (memelee bridge?)
a/1/6/3 - 
a/1/6/4 - Map models (pelago?)
a/1/6/5 - 
a/1/6/6 - 
a/1/6/7 - 
a/1/6/8 - 
a/1/6/9 - grpfont_etc
a/1/7/0 - NPC models
a/1/7/1 - 
a/1/7/2 - 
a/1/7/3 - HD boys hair
a/1/7/4 - HD boys glasses
a/1/7/5 - HD boys backpacks
a/1/7/6 - HD boys z-braclet
a/1/7/7 - HD boys bottoms
a/1/7/8 - HD boys hats
a/1/7/9 - HD boys socks
a/1/8/0 - HD boys shoes
a/1/8/1 - Boy riding outfit
a/1/8/2 - HD boys shirts
a/1/8/3 - 
a/1/8/4 - 
a/1/8/5 - HD girls hair ('HD' used in changingroom, passport, etc)
a/1/8/6 - HD girls glasses
a/1/8/7 - HD girls accessories
a/1/8/8 - HD girls sachels
a/1/8/9 - HD girls z-braclet
a/1/9/0 - HD girls bottoms
a/1/9/1 - HD girls hats
a/1/9/2 - HD girls socks
a/1/9/3 - HD girls shoes
a/1/9/4 - Girl riding outfit
a/1/9/5 - HD girls shirts 
a/1/9/6 - 
a/1/9/7 - 
a/1/9/8 - Boys face
a/1/9/9 - OW boys hair
a/2/0/0 - OW boys glasses
a/2/0/1 - OW boys backpacks
a/2/0/2 - OW boys z-braclet
a/2/0/3 - OW boys bottoms
a/2/0/4 - OW boys hats
a/2/0/5 - OW boys socks
a/2/0/6 - OW boys shoes
a/2/0/7 - Boy riding outfit
a/2/0/8 - OW boys shirts
a/2/0/9 - 
a/2/1/0 -
a/2/6/9 - Pokemon sprites
a/2/7/7 - Battle Tree Pokemon (Normal)
a/2/7/8 - Battle Tree Trainer (Normal)
a/2/7/9 - Battle Tree Pokemon (Super)
a/2/8/0 - Battle Tree Trainer (Super)
a/2/8/1 - Ending credits image
