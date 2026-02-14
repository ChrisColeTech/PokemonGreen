# PokemonGreen Map Generator

CLI tool for converting between map editor JSON and C# map classes used by the game runtime.

## Usage

Run from the repository root:

```bash
dotnet run --project src/PokemonGreen.MapGen -- <command> [options]
```

### Commands

#### `generate` — JSON to C#

Converts `.map.json` files into generated `.g.cs` map classes.

```bash
dotnet run --project src/PokemonGreen.MapGen -- generate --input Assets/Maps [--output src/PokemonGreen.Core/Maps]
```

- `--input, -i` — Folder containing `.map.json` files (required)
- `--output, -o` — Output folder for `.g.cs` files (defaults to `PokemonGreen.Core/Maps/`)

#### `export` — C# to JSON

Converts generated `.g.cs` map files back to `.map.json` format.

```bash
dotnet run --project src/PokemonGreen.MapGen -- export --input src/PokemonGreen.Core/Maps [--output maps/exported]
```

- `--input, -i` — Folder containing `*Map.g.cs` files (required)
- `--output, -o` — Output folder for `.map.json` files (defaults to input folder)

#### `export-registry` — Tile registry to JSON

Exports the C# `TileRegistry` as JSON for the map editor.

```bash
dotnet run --project src/PokemonGreen.MapGen -- export-registry --output src/PokemonGreen.MapEditor/src/data/registries/default.json
```

- `--output, -o` — Output file path (required)

## Build Pipeline

```
Editor (.map.json)  ──generate──►  C# (.g.cs)  ──dotnet build──►  Game runtime
Game C# (.g.cs)     ──export────►  JSON (.map.json)  ──import──►  Editor
```

1. Design maps in the editor, export as `.map.json`
2. Run `generate` to produce `.g.cs` map classes
3. Build the solution — generated maps are compiled into the game
4. To edit existing maps, run `export` to convert `.g.cs` back to `.map.json`

## File Formats

### Input: Schema v2 JSON (`.map.json`)

```json
{
  "schemaVersion": 2,
  "mapId": "test_two_layer_map",
  "displayName": "Test Two Layer Map",
  "tileSize": 32,
  "width": 8,
  "height": 6,
  "baseTiles": [[1, 1, ...], ...],
  "overlayTiles": [[null, 3, ...], ...]
}
```

### Output: Generated C# (`.g.cs`)

```csharp
public sealed class TestTwoLayerMap : MapDefinition
{
    private static readonly int[] BaseTileData = [ ... ];
    private static readonly int?[] OverlayTileData = [ ... ];
    private static readonly int[] WalkableTileIds = [ ... ];

    public static TestTwoLayerMap Instance { get; } = new();

    private TestTwoLayerMap()
        : base("test_two_layer_map", "Test Two Layer Map", 8, 6, 32,
               BaseTileData, OverlayTileData, WalkableTileIds)
    { }
}
```

- `BaseTileData` — Flat row-major array of tile IDs (`width * height` elements)
- `OverlayTileData` — Flat row-major array of nullable tile IDs (null = no overlay)
- `WalkableTileIds` — Unique set of walkable tile IDs used in this map (derived from `TileRegistry`)

## Project Structure

```
PokemonGreen.MapGen/
  Program.cs                         — CLI entry point and command dispatcher
  Commands/
    GenerateCommand.cs               — JSON → C# conversion
    ExportCommand.cs                 — C# → JSON conversion
    ExportRegistryCommand.cs         — TileRegistry → JSON export
  Models/
    MapJsonModel.cs                  — JSON deserialization model
  Services/
    MapParser.cs                     — .map.json file parser
    CodeGenerator.cs                 — C# code emitter
    RegistryExporter.cs              — TileRegistry JSON serializer
  PokemonGreen.MapGen.csproj         — .NET 9 console app, references PokemonGreen.Core
```

## Dependencies

- .NET 9.0
- `PokemonGreen.Core` (project reference — provides `MapDefinition`, `TileRegistry`, `TileMap`)
