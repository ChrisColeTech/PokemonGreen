# Map export pipeline

## Editor export contract (schemaVersion = 1)

The web map editor exports `.map.json` with this structure:

- `schemaVersion`: integer, must be `1`
- `mapId`: stable lowercase id (`a-z`, `0-9`, `_`, `-`)
- `displayName`: user-facing map name
- `tileSize`: tile size used by runtime rendering
- `width`, `height`: map dimensions
- `tiles`: 2D tile id grid (`height` rows, each with `width` ids)
- `tileTypes`: tile metadata keyed by tile id string

## Generate runtime maps

Run from repository root:

```powershell
./tools/run-mapgen.ps1
```

Equivalent explicit command:

```bash
dotnet run --project src/PokemonGreen.MapGen/PokemonGreen.MapGen.csproj -- --input Assets/Maps --output src/PokemonGreen.Core/Maps
```

Generated map classes are emitted directly to:

- `src/PokemonGreen.Core/Maps/*.g.cs`

## Build sequence

1. Export or update `Assets/Maps/*.map.json` from the editor.
2. Run map generator.
3. Build solution.

## Tile coverage verification

`Assets/Maps/tile_coverage.map.json` is a fixture map that includes every editor tile id (`0..50`) at least once.

Run these checks from repository root:

```bash
dotnet run --project src/PokemonGreen.MapGen/PokemonGreen.MapGen.csproj -- --input Assets/Maps --output src/PokemonGreen.Core/Maps
dotnet build PokemonGreen.sln
```

Failure meaning:

- `tileTypes defines ids not renderable by runtime catalog` means a map exports tile ids that runtime cannot render.
- `Runtime tile render catalog is invalid` means runtime tile render mappings are missing expected ids.

Pass condition:

- Map generation succeeds and solution build succeeds with no tile id/render coverage errors.
