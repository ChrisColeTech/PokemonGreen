# PokemonGreen Map Editor

React + TypeScript editor for PokemonGreen `.map.json` maps.

It supports a dynamic, registry-driven palette so tile/building options come from exported game data instead of hardcoded UI lists.

## Quick Start

```bash
npm install
npm run dev
```

Useful scripts:

```bash
npm run test
npm run build
npm run lint
```

## Map Workflow

1. Open map-editor and create/edit a map.
2. Save/export `.map.json`.
3. Run MapGen to convert JSON -> C# map classes.
4. Run the game.

MapGen command example:

```bash
# from src/PokemonGreen.MapGen
dotnet run -- --input ../../Assets/Maps --output ../PokemonGreen.Core/Maps
```

## Registry Workflow (MapGen -> Editor)

The editor default registry file is:

`src/data/registries/default.json`

Generate/update it from MapGen:

```bash
# from src/PokemonGreen.MapGen
dotnet run -- export-registry --output ../../tools/map-editor/src/data/registries/default.json
```

If `--output` is omitted, MapGen writes to this default path automatically.

## Dynamic Registry Behavior

When the active registry changes, the existing UI updates in place:

- Palette category tabs come from `registry.categories` where `showInPalette` is `true`.
- Tile buttons come from `registry.tiles` filtered by selected category.
- Building buttons come from `registry.buildings`.
- Building preview/placement size is derived from each building footprint matrix (`tiles`).
- Palette/canvas colors come from registry tile colors through `tileColorService` (including Distinct mode).

Fallback rules on registry switch:

- Missing category -> first visible category.
- Missing tile -> first walkable terrain-like tile, then first walkable tile, then first tile.
- Missing building -> building selection cleared.

## Registry JSON Contract

```json
{
  "metadata": {
    "id": "default",
    "name": "PokemonGreen Default",
    "version": "1.0.0"
  },
  "categories": [
    { "id": "terrain", "label": "Terrain", "showInPalette": true }
  ],
  "tiles": [
    {
      "id": 1,
      "name": "Grass",
      "color": "#2d5a27",
      "walkable": true,
      "category": "terrain"
    }
  ],
  "buildings": [
    {
      "id": "house-small",
      "name": "House Small",
      "tiles": [[3, 3, 3], [3, 4, 3], [6, 4, 6]]
    }
  ]
}
```

Validation rejects malformed payloads, duplicate IDs, unknown category references, and buildings that reference unknown tile IDs.

## Important Notes

- This project intentionally keeps the existing map-editor layout; registry integration is data-driven, not a UI redesign.
- Building footprints are map data templates (placement logic), while visuals come from tile art at render time.
- `PokemonGreen.Core` tile definitions are the source of truth; regenerate the registry after tile changes.
