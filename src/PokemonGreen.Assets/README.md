# PokemonGreen.Assets

Game asset library: sprites, 3D models, battle backgrounds, fonts, encounter data, and the loaders that serve them at runtime.

## Directory layout

```
PokemonGreen.Assets/
  BattleBG/              Battle background images (copied to build output)
  Content/
    Data/Encounters/     Wild encounter JSON tables
    Fonts/Kerm/          Bitmap font files (.kermfont)
  Data/
    species.json         Species registry (Pokedex ID, name, stats, model folder)
  Items/                 Item definitions
  NPCs/                  NPC definitions
  Player/                Player sprite sheets (embedded resource)
  Pokemon3D/             3D models, textures, and animation clips (NOT copied to build)
  Sprites/               Tile and UI sprite sheets (embedded resource)
  scripts/               Asset extraction/conversion scripts
```

## Pokemon3D models

Each species or character has a folder under `Pokemon3D/` following this structure:

```
Pokemon3D/
  pm0001_00/             Bulbasaur
    model.dae            Static skeletal mesh (no baked animation)
    model_1.dae          Low-poly variant (if present)
    manifest.json        Clip registry: file paths, frame counts, fps
    clips/
      model/
        clip_000.dae     Animation clip 0 (skeleton + keyframes, no mesh)
        clip_001.dae     Animation clip 1
        ...
      model_1/           Clips for the low-poly variant
        clip_000.dae
        ...
    pm0001_00_BodyA1.png Diffuse textures
    pm0001_00_Eye1.png
    ...
  characters/
    player/p1_base/      Player character model + clips
    trainers/tr0001_00/  Trainer NPC models + clips
```

### Manifest format

```json
{
  "version": 1,
  "mode": "split-model-anims",
  "textures": ["pm0001_00_BodyA1.png", ...],
  "models": [
    {
      "name": "model",
      "modelFile": "model.dae",
      "clips": [
        { "index": 0, "name": "anim_0", "file": "clips/model/clip_000.dae", "frameCount": 42, "fps": 30 },
        { "index": 1, "name": "anim_0", "file": "clips/model/clip_001.dae", "frameCount": 44, "fps": 30 }
      ]
    }
  ]
}
```

Clip names are numeric IDs from the original 3DS game data (`anim_0`, `anim_1`, etc. for overworld; `Motion_0`, `Motion_1`, etc. for battle models). These correspond to animation slots in the original engine. Clip index 0 is typically idle.

### How models are extracted

3DS ROM assets (BCH/GARC format) are converted using the SPICA-based tools in the `PokemonGreen.Spica` solution:

1. **SpicaCli** (`SpicaCli convert <garc> -o <dir> --split-model-anims`) extracts GARC containers into per-species folders with split model + clips
2. **Spica.Registry** does the same for the overworld character GARCs

Both produce the folder structure above. See `docs/tools-docs/09-SPICA-SPLIT-MODEL-ANIMATION-PLAN.md` for the full extraction pipeline.

## Asset loading architecture

### Problem

Pokemon3D contains hundreds of species folders, each with dozens of animation clip DAEs. Copying all of this into build output on every `dotnet build` would be slow and wasteful.

### Solution: external asset path with lazy loading

```
dotnet build   -> fast, no Pokemon3D copying
dotnet publish -> MSBuild target copies Pokemon3D/ into publish output
```

**Development** (`dotnet build`):
- The csproj declares `<None Include="Pokemon3D\**\*.*" />` so files are visible in the IDE but not copied to `bin/`
- `PokemonModelLoader.InitializeDevPaths()` resolves the source `Pokemon3D/` directory by walking from the Assets assembly's bin location back to the project root
- Game1 calls this once at startup

**Production** (`dotnet publish`):
- `InitializeDevPaths()` can't find the source directory (no project root), so `ExternalAssetsPath` stays null
- The loader falls back to `{exe dir}/Pokemon3D/`
- A custom MSBuild target in `PokemonGreen.csproj` copies the assets into the publish output automatically

**Override**: Set `PokemonModelLoader.ExternalAssetsPath` manually to load from any arbitrary directory.

### Lazy clip loading

Models can have 30-140 animation clips each. Loading all clips upfront would be wasteful since most are never used in a given scene.

- `PokemonModelLoader` reads `manifest.json` and calls `RegisterClip(name, path)` for each clip, storing only the file path
- The first clip (idle) is activated immediately via `Play(clipName)`, which triggers its DAE to be parsed on demand
- Other clips are only parsed when `Play()` or `PlayIndex()` is called for them
- Parsed clips are cached in memory for the lifetime of the model

### LRU model cache

`PokemonModelLoader` maintains an LRU cache (default 16 models). When a new model is loaded and the cache is full, the least-recently-used model is evicted and its GPU resources are freed.

## Key classes

| Class | Purpose |
|---|---|
| `PokemonModelLoader` | Entry point: loads species by folder name, manages cache + external path |
| `SkeletalModelData` | Skeletal model with CPU skinning, multi-clip animation, lazy clip loading |
| `AnimationClip` | Single clip: duration, ticks-per-second, bone keyframe channels |
| `BattleModelLoader` | Static (non-animated) model loader for battle backgrounds/platforms |
| `AssetLoader` | Embedded resource loader for sprites and tile sheets |

## Build integration

In `PokemonGreen.Assets.csproj`:
```xml
<!-- Visible in IDE, not copied to build output -->
<None Include="Pokemon3D\**\*.*" />
```

In `PokemonGreen.csproj` (executable):
```xml
<!-- Copies Pokemon3D/ into publish output only -->
<Target Name="PublishPokemon3D" AfterTargets="Publish">
  <ItemGroup>
    <Pokemon3DFiles Include="..\PokemonGreen.Assets\Pokemon3D\**\*.*" />
  </ItemGroup>
  <Copy SourceFiles="@(Pokemon3DFiles)"
        DestinationFiles="@(Pokemon3DFiles->'$(PublishDir)Pokemon3D\%(RecursiveDir)%(Filename)%(Extension)')"
        SkipUnchangedFiles="true" />
</Target>
```
