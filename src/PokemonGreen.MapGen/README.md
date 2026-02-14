# PokemonGreen Map Generator

A .NET tool for generating C# map classes from JSON and exporting C# maps back to JSON for editing.

## Commands

### Generate (JSON → C#)

Converts JSON map files to C# classes that the game runtime uses.

```bash
dotnet run -- --input <json-folder> --output <cs-output-folder>
```

**Example:**
```bash
dotnet run -- --input ../../maps --output ../PokemonGreen.Core/Maps
```

**Input format:** `.map.json` files matching schema v1 or v2

**Output:**
- `{ClassName}.g.cs` - Generated map class
- `GeneratedMapCatalog.g.cs` - Registry of all maps

### Export (C# → JSON)

Converts C# map classes back to JSON for editing in the map-editor.

```bash
dotnet run -- export --input <cs-folder> --output <json-folder>
```

**Example:**
```bash
dotnet run -- export --input ../PokemonGreen.Core/Maps --output ../../maps/exported
```

**Input:** `.g.cs` files (generated map classes)

**Output:** `.map.json` files compatible with the map-editor

### Export Registry (Tile Registry -> JSON)

Exports the registry JSON consumed by `tools/map-editor`.

```bash
dotnet run -- export-registry --output <json-file-path>
```

**Example:**
```bash
dotnet run -- export-registry --output ../../tools/map-editor/src/data/registries/default.json
```

**Default output (when omitted):**

`tools/map-editor/src/data/registries/default.json`

**Output payload includes:**
- Registry metadata under `metadata` (`id`, `name`, `version`)
- Categories (`id`, `label`, `showInPalette`)
- Tiles from `PokemonGreen.Core/Maps/TileRegistry.cs`
- Building templates (`id`, `name`, `tiles` matrix)

Notes:
- Building templates are currently mirrored from `tools/map-editor/src/data/buildings.ts` to keep Phase 1 parity and are treated as a temporary source.
- Tile/category definitions remain sourced from `PokemonGreen.Core/Maps/TileRegistry.cs`.
- Export ordering is deterministic (stable category, tile, and building ordering).

### Registry Export Workflow into Map Editor

```bash
# 1) From src/PokemonGreen.MapGen
dotnet run -- export-registry --output ../../tools/map-editor/src/data/registries/default.json

# 2) Run map-editor tests/build
cd ../../tools/map-editor
npm run test
npm run build

# 3) Start editor
npm run dev
```

Map editor consumes this registry to drive categories, tiles, buildings, building footprint sizes, and distinct tile color rendering without a sidebar/layout redesign.

## Typical Workflow

```
1. Design map in map-editor
   ↓
2. Export as JSON (map-editor)
   ↓
3. Generate C# class (dotnet run --)
   ↓
4. Game loads map at runtime
   ↓
5. Need to edit? Export to JSON (dotnet run -- export)
   ↓
6. Edit in map-editor, repeat from step 2
```

## File Structure

```
PokemonGreen.MapGen/
├── Commands/
│   ├── ExportCommand.cs      # Export C# → JSON
│   └── ExportRegistryCommand.cs # Export tile registry JSON
├── Models/
│   ├── MapData.cs            # Internal map representation
│   ├── MapJsonPayload.cs     # JSON output format
│   ├── RegistryJsonPayload.cs # Registry JSON output format
│   └── Result.cs             # Result type
├── Services/
│   ├── MapClassParser.cs     # Parse C# map files
│   └── MapExporter.cs        # Export to JSON
└── Program.cs                # Entry point + command routing
```

## Dependencies

- `PokemonGreen.Core` - Uses `TileRegistry` for tile definitions

## JSON Schema (v2)

```json
{
  "schemaVersion": 2,
  "mapId": "my_map",
  "displayName": "My Map",
  "width": 10,
  "height": 8,
  "tileSize": 32,
  "baseTiles": [[1, 1, 2, ...], ...],
  "overlayTiles": [[null, 3, null, ...], ...],
  "tileTypes": {
    "1": {"name": "Grass", "walkable": true, "category": "terrain"},
    ...
  }
}
```

## Tile System

### Base Tiles
Ground/terrain tiles (grass, water, path, etc.). These are always rendered.

### Overlay Tiles
Decoration/structure tiles (trees, rocks, flowers, items, NPCs). Rendered on top of base tiles. Can be `null` for empty cells.

### Tile Categories
- `terrain` - Ground tiles (grass, water, path)
- `decoration` - Decorative overlays (trees, rocks, flowers)
- `interactive` - Interactable objects (doors, signs, warps)
- `entity` - NPCs, items, trainers
- `trainer` - Trainer types (gym leaders, rivals)
- `encounter` - Encounter tiles (tall grass)
- `structure` - Structural tiles (walls)
- `item` - Key items (pokeballs, potions, stones)

## Tile Registry

All tile definitions live in `PokemonGreen.Core/Maps/TileRegistry.cs`. This is the **single source of truth** for:
- Tile IDs
- Names
- Categories
- Walkability
- Visual properties
- Colors

When adding new tiles, edit `TileRegistry.cs` only. The generator and game will use it automatically.

## Related Tools

- **Map Editor:** `tools/map-editor` - Visual map editing (React/TypeScript)
- **Core Library:** `src/PokemonGreen.Core` - Game runtime and tile registry

## Examples

### Export Current Maps for Editing

```bash
# From PokemonGreen.MapGen directory
dotnet run -- export --input ../PokemonGreen.Core/Maps --output ../../maps/exported

# Open in map-editor
cd ../../tools/map-editor
npm run dev
# Import maps/exported/test_two_layer_map.map.json
```

### Generate Maps from JSON

```bash
# After editing in map-editor, export JSON to maps/
# Then generate C# classes
dotnet run -- --input ../../maps --output ../PokemonGreen.Core/Maps

# Build and run game
cd ../PokemonGreen
dotnet run
```
