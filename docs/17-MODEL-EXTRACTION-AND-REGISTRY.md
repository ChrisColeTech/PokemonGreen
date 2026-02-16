# 17 - 3D Model Extraction, Species Registry & Asset Pipeline: Lessons Learned & Handoff

**Date:** 2026-02-16
**Scope:** OhanaCli model extraction from Ultra Sun ROM, PokeAPI species data, SpeciesRegistry rewrite, Pokemon3D asset pipeline
**Codebase:** OhanaCli (8 format files + Program.cs), PokemonGreen.Core (SpeciesRegistry, SpeciesData), PokemonGreen.Assets (PokemonModelLoader, Pokemon3D/)

---

## 1. What We Accomplished

### OhanaCli - Pokemon 3DS Model Extraction Tool
- **Ported Ohana3DS-Rebirth** from `D:\Projects\Ohana3DS-Rebirth` into a modern .NET 8 CLI tool at `src/PokemonGreen.OhanaCli/`
- **27 format files** modernized from the original C# codebase (made classes public, fixed access modifiers)
- **Commands:** `info`, `convert`, `batch`, `convert-all`
- **`convert-all` command** recursively finds all GARC archives in a directory tree and batch-converts everything
- **Streaming single-pass grouping**: Each Pokemon in Sun/Moon packs as ~9 consecutive GARC entries (config, model, hi-res textures, shiny textures, low-res textures, animations). The converter groups model + trailing texture entries, names folders by Pokemon ID extracted from texture filenames (`pm####_##` regex), and separates shiny/variant textures into subfolders.

### Extraction Results
- **268 GARCs** processed across the full Ultra Sun ROM dump at `D:\Projects\Ohana3DS-Rebirth\sun-moon-dump\RomFS\a\`
- **3,570 models** + **32,280 textures** exported to `Assets/SunMoon/`
- **Main Pokemon GARC (a/0/9/4):** 10,549 entries → 1,154 models + 23,790 textures, 0 errors
- **957 Pokemon model folders** extracted with proper naming (`pm0001_00/` = Bulbasaur)
- **Output structure per Pokemon:** `model.dae` + `*.png` textures + `shiny/` + `variant_N/` subfolders
- **Report:** `Assets/SunMoon/report.txt` (320 lines, clean aggregated summary)

### Species Registry - Data-Driven Rewrite
- **Fetched all 802 Gen 1-7 species** from PokeAPI's GitHub CSV files (5 HTTP requests, not 1600+)
  - Script: `src/PokemonGreen.Assets/scripts/fetch-species.py`
  - Output: `src/PokemonGreen.Assets/Data/species.json` — names, base stats, types, growth rates, catch rates
- **Rewrote SpeciesRegistry** from 18 hardcoded entries to JSON-driven loading of all 802 species
- **Added `ModelFolder` property** to `SpeciesData` — links each species to its `Pokemon3D/pm####_00/` folder
- **Convention:** Every species maps to `pm{id:D4}_00`. Folders on disk match this naming.

### Asset Pipeline
- **957 model folders** copied into `src/PokemonGreen.Assets/Pokemon3D/`
- **Content includes** in .csproj copy Pokemon3D + species.json to build output
- **PokemonModelLoader** — loads Pokemon DAE models via Assimp with caching, returns `BattleModelData` for GPU rendering
- Follows same pattern as existing `BattleModelLoader` for battle backgrounds

### Bug Fixes Along the Way
- **GARC lazy loading:** Added `ReadEntryData` helper to handle `loadFromDisk=true` entries that keep data on disk
- **LZSS decompression:** Used `LZSS_Ninty.decompress(byte[])` overload which handles header parsing internally
- **DAE file handle leak:** Wrapped FileStream + XmlWriter in `using` statements in DAE.cs
- **GARC magic endianness:** Fixed constant from `0x43524147` to `0x47415243` for little-endian BitConverter
- **Empty folder creation:** Deferred `Directory.CreateDirectory` to only when content exists
- **Report verbosity:** Removed per-magic-byte and per-file-size tracking from skip reasons (19K lines → 320)
- **Folder naming:** Renamed 38 variant folders (`_11` → `_00`) where base form was missing

---

## 2. What Work Remains

### Critical: 79 Missing Pokemon Models
- **IDs 650-700** (Gen 6: Chespin → Sylveon) and **775-800** (late Gen 7: Komala → Necrozma) are in the GARC but produce 0 meshes
- The `.pc` files exist with real sizes (100KB-400KB) but the Ohana parser's mesh extraction silently fails
- These are likely a different GfModel version or encoding — the parser handles the container but not the mesh data within
- **Investigation in progress** — examining the GfModel/PC parser to find where mesh extraction diverges

### Battle Rendering
- `PokemonModelLoader` is wired but **no Pokemon models are rendered on the battle platforms yet**
- `Game1.cs` draws backgrounds + platforms but doesn't call `PokemonModelLoader.Load()` during battle
- Need to: load ally + foe models in `EnterBattle()`, position them on platforms, render in `DrawBattle3D()`

### Pre-existing Build Errors (Not From This Work)
- `PartyPokemon.Status` — referenced in PartyScreen.cs and SaveManager.cs but not defined
- `ItemDefinition.ParsedEffect` — referenced in ItemUseHandler.cs but not defined
- These block a full game build but are unrelated to the model/registry work

---

## 3. Optimizations - Prime Suspects

### 3.1 Pokemon3D Build Copy Time
The .csproj copies 957 folders (~thousands of files) to the build output directory on every build via `<Content Include="Pokemon3D\**\*.*">`. This could slow incremental builds significantly.
- **Fix:** Use `PreserveNewest` (already set) and consider moving Pokemon3D outside the project as a runtime asset directory that doesn't participate in MSBuild at all. The game can reference it by absolute path or a config setting.

### 3.2 Model Loading Memory
Loading all 957 Pokemon models into GPU memory simultaneously is not feasible. `PokemonModelLoader` has a cache but no eviction.
- **Fix:** Add LRU eviction or load only the 2 models needed per battle (ally + foe), unload on battle exit.

### 3.3 DAE Export Quality
The Ohana exporter writes Collada XML which Assimp then re-parses at runtime. This double-conversion (binary → DAE → GPU) is wasteful.
- **Fix:** Convert DAE files to a faster binary format (e.g. glTF binary, or a custom vertex buffer dump) as a build-time step. Load directly into GPU buffers without Assimp parsing.

### 3.4 PokeAPI Script Could Be Build-Integrated
The fetch script runs manually. If species data needs updating (new gens, corrections), there's no automated path.
- **Fix:** Add an MSBuild target or npm script that regenerates species.json only when the script changes.

---

## 4. Step by Step: Getting the App Fully Working

### Prerequisites
- .NET 9.0 SDK
- Python 3.x (any venv with stdlib — no pip packages needed)
- Ultra Sun ROM dump at `D:\Projects\Ohana3DS-Rebirth\sun-moon-dump\RomFS\a\`

### Steps

1. **Fix pre-existing build errors** (Status/ParsedEffect — unrelated to this work):
   ```
   src/PokemonGreen.Core/UI/Screens/PartyScreen.cs — add Status property to PartyPokemon
   src/PokemonGreen.Core/Items/ItemUseHandler.cs — add ParsedEffect property to ItemDefinition
   ```

2. **Verify species data exists:**
   ```
   ls src/PokemonGreen.Assets/Data/species.json  # 802 entries
   ```
   If missing, regenerate:
   ```
   python src/PokemonGreen.Assets/scripts/fetch-species.py
   ```

3. **Verify Pokemon3D models exist:**
   ```
   ls src/PokemonGreen.Assets/Pokemon3D/ | wc -l  # should be 957
   ```
   If missing, copy from extraction:
   ```
   cp -r Assets/SunMoon/0_9_4/* src/PokemonGreen.Assets/Pokemon3D/
   ```

4. **Build:**
   ```
   dotnet build src/PokemonGreen/PokemonGreen.csproj
   ```

5. **Verify build output:**
   ```
   ls bin/Debug/net9.0/Data/species.json         # species data
   ls bin/Debug/net9.0/Pokemon3D/ | wc -l        # 957 model folders
   ```

6. **Run the game:**
   ```
   dotnet run --project src/PokemonGreen/PokemonGreen.csproj
   ```
   The SpeciesRegistry loads 802 species on startup. Trigger a wild encounter — the battle scene renders backgrounds/platforms (Pokemon model rendering is next step).

---

## 5. How to Run the OhanaCli Tool

### Build
```bash
cd src/PokemonGreen.OhanaCli
dotnet build src/OhanaCli.App/OhanaCli.App.csproj -c Release
```

### Commands
```bash
# Inspect a file
dotnet run --project src/OhanaCli.App -- info <file>

# Convert a single file
dotnet run --project src/OhanaCli.App -- convert <input-file> <output-dir>

# Bulk convert all GARCs in a directory tree
dotnet run --project src/OhanaCli.App -- convert-all <rom-dir> <output-dir> --format dae
```

### Full extraction example
```bash
dotnet run -c Release --project src/OhanaCli.App -- convert-all \
  "D:/Projects/Ohana3DS-Rebirth/sun-moon-dump/RomFS/a/" \
  "D:/Projects/PokemonGreen/Assets/SunMoon/" \
  --format dae
```
This produces `report.txt` in the output directory with full statistics.

---

## 6. Known Issues & Strategies

### Issue 1: 79 Pokemon Models Parse With 0 Meshes
**Symptom:** .pc files for IDs 650-700 and 775-800 are loaded as `formatType.model` but `OModelGroup` has 0 meshes.
**Root cause:** Likely a GfModel version difference — Gen 6 Pokemon ported into Gen 7 data may use a slightly different mesh encoding.

**Strategies:**
1. **Diff the binary headers** — Compare a working .pc file (e.g. Bulbasaur #1) against a failing one (e.g. Chespin #650) byte-by-byte. Look for version fields, different magic bytes, or offset table differences that cause the mesh parser to skip entries.
2. **Add debug logging to GfModel parser** — Instrument the mesh extraction loop in the GfModel/PC parser to log exactly where it bails out (e.g. mesh count reads as 0, or a section offset points to invalid data).
3. **Cross-reference with pk3DS or other tools** — The pk3DS tool (https://github.com/kwsch/pk3DS) handles all Gen 6/7 model variants. Compare its parsing logic for the mesh section against Ohana's to find the divergence.
4. **Try the other GARC archives** — The 0_8_7 GARC had 877 models with 3,494 errors. These might be the same Pokemon in a different format that the parser handles differently. Check if the missing IDs exist there.

### Issue 2: 11,641 Errors Across All GARCs (Non-Model Data)
**Symptom:** "Arithmetic operation resulted in an overflow" on .bin files.
**Root cause:** The parser tries to interpret animation/script/sound data as model data and overflows on size calculations.
**Strategy:** These are not model files — the parser correctly errors and skips them. No fix needed unless we want to extract animations too.

### Issue 3: Pre-existing Build Errors Block Full Game Build
**Symptom:** `PartyPokemon.Status` and `ItemDefinition.ParsedEffect` are undefined.
**Strategy:** These are from incomplete features in a previous session. Add the missing properties or stub them out. They're unrelated to the model/registry work.

### Issue 4: Build Performance With 957 Model Folders
**Symptom:** MSBuild evaluates `Pokemon3D\**\*.*` glob on every build.
**Strategy:** Move Pokemon3D to a runtime-only directory outside the .csproj, or use a post-build copy script that only runs when the source changes.

---

## 7. Architecture & New Features

### New Architecture Components

```
Game1.Initialize()
  → SpeciesRegistry.Initialize()
      → loads Data/species.json (802 species)
      → each SpeciesData gets ModelFolder = "pm{id:D4}_00"

Battle entry:
  → SpeciesRegistry.GetSpecies(speciesId)
      → SpeciesData { Name, Stats, Types, ModelFolder }
  → PokemonModelLoader.Load(species.ModelFolder, graphicsDevice)
      → Pokemon3D/{modelFolder}/model.dae
      → BattleModelLoader.Load() (Assimp)
      → BattleModelData (GPU buffers + textures)
```

### Quick Wins

1. **Render Pokemon on battle platforms** — `PokemonModelLoader.Load()` returns `BattleModelData` which already has a `.Draw()` method. Add 2 calls in `DrawBattle3D()` to draw ally model at the ally platform position and foe model at the foe platform position. ~20 lines of code.

2. **Pokemon model viewer CLI** — Add a `view` command to OhanaCli that opens a MonoGame window and renders a single Pokemon model with orbit camera. Useful for debugging the 79 missing models.

3. **Species count on startup** — Log `SpeciesRegistry.Count` at startup to confirm all 802 species loaded. One line.

4. **Shiny texture support** — The extraction already separates shiny textures into `shiny/` subfolders. `PokemonModelLoader` could accept a `bool shiny` parameter and load textures from the shiny subfolder instead.

---

## 8. File Reference

| File | Purpose |
|------|---------|
| `src/PokemonGreen.Assets/Pokemon3D/pm####_##/` | 957 Pokemon model folders (DAE + PNG) |
| `src/PokemonGreen.Assets/Data/species.json` | 802 species: names, stats, types, catch rates |
| `src/PokemonGreen.Assets/PokemonModelLoader.cs` | Load Pokemon models by folder name, with cache |
| `src/PokemonGreen.Assets/BattleModelLoader.cs` | Assimp DAE loader (used by PokemonModelLoader) |
| `src/PokemonGreen.Assets/scripts/fetch-species.py` | PokeAPI CSV fetcher (5 requests, generates species.json) |
| `src/PokemonGreen.Core/Pokemon/SpeciesData.cs` | Species definition (stats, types, ModelFolder) |
| `src/PokemonGreen.Core/Pokemon/SpeciesRegistry.cs` | JSON-driven species registry (802 entries) |
| `src/PokemonGreen.OhanaCli/` | CLI tool for extracting 3DS model files |
| `Assets/SunMoon/report.txt` | Extraction report (268 GARCs, summary stats) |
| `src/PokemonGreen.OhanaCli/README.md` | OhanaCli documentation |
