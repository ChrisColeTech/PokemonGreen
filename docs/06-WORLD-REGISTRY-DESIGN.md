# World Registry Design

**Date:** 2026-02-14
**Goal:** Organize maps into worlds (levels/cities/locations) and connect them with warps.

---

## The Problem (Plain English)

Right now the game only has one group of maps — `small_world` — five 16x16 maps arranged in a cross. They connect to each other because each map has a grid position (`worldX`, `worldY`) and the game automatically links neighbors when the player walks off an edge.

But there's no way to have **multiple groups of maps**. We want to build separate locations — a town, a forest, a cave, a gym interior — each with their own set of maps laid out on their own grid. Each location needs its own (0,0) center map. And we need a way to travel between locations: walk through a door in town and end up inside the gym, enter a cave mouth and appear in the cave dungeon.

The game has no concept of "this map belongs to this location." Every map is dumped into one flat list. The neighbor-finding code searches that flat list by grid position, so if two maps from different locations both sit at (0,0), the game can't tell them apart and picks the wrong one.

We need:
1. A way to group maps into **worlds** (locations/levels)
2. Each world gets its own local grid so maps don't conflict
3. A way to **connect worlds** to each other (doors, caves, warps)
4. A **registry** so the game knows what worlds exist and where the player starts

## Possible Solutions

### 1. Add a `worldId` tag to every map

Each map gets a `worldId` field (e.g. `"small_world"`, `"viridian_city"`). The neighbor-finding code only matches maps with the same `worldId`. Doors and warps can target any map in any world by map ID. Simple, minimal change — just one new field and a filter.

### 2. Namespace map IDs with a world prefix

Use naming convention: `small_world/test_map_center`, `viridian_city/plaza`. The slash makes the world implicit. No new field needed — parse it from the ID. Downsides: forces a naming convention, breaks existing map IDs.

### 3. Separate MapCatalogs per world

Instead of one global `MapCatalog`, each world gets its own catalog instance. The game loads only the current world's catalog. Clean isolation, but more complex — need to manage multiple catalogs, handle cross-world lookups for warps.

### 4. World definition files

A `world.json` file per world folder that lists its maps, defines the spawn point, and provides metadata. The game discovers worlds by scanning for these files. Maps don't need to know their world — the world file claims them. Downside: maps aren't self-describing.

### 5. Composite approach (recommended)

Combine #1 and #4: each map carries a `worldId` tag (self-describing, used at runtime for neighbor filtering), AND each world has a `world.json` with metadata (spawn point, display name). Best of both — maps know where they belong, and worlds have a clear definition.

---

## Design

### Core Concept

A **world** is a group of maps that share a local coordinate grid. Maps within a world connect via **edge transitions** (walk off one map, appear on the next). Maps between worlds connect via **warps** (doors, cave mouths, teleporters).

```
small_world                          viridian_city
+-------+-------+-------+           +-------+-------+
| map_b | center| map_d |           | plaza |  gym  |
+-------+---+---+-------+           +---+---+-------+
            |                           |
        | map_a |                   | market|
        +-------+                   +-------+
            |
        | map_c |    ---- warp (door tile) ---->  viridian_city/plaza
        +-------+
```

### File Layout

```
maps/
  small_world/
    world.json                    <-- world metadata
    test_map_center.map.json
    test_map_a_top.map.json
    test_map_b_left.map.json
    test_map_c_bottom.map.json
    test_map_d_right.map.json
  viridian_city/
    world.json
    viridian_plaza.map.json
    viridian_gym.map.json
    viridian_market.map.json
```

### world.json Schema

One per world folder. Defines identity and default spawn.

```json
{
  "id": "small_world",
  "name": "Small World",
  "spawnMapId": "test_map_center",
  "spawnX": 8,
  "spawnY": 8
}
```

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique world identifier, matches folder name |
| `name` | string | Display name shown in UI/menus |
| `spawnMapId` | string | Default map when entering this world |
| `spawnX` | int | Default spawn tile X on the spawn map |
| `spawnY` | int | Default spawn tile Y on the spawn map |

### Map Schema Change

Add `worldId` to each `.map.json`:

```json
{
  "schemaVersion": 2,
  "worldId": "small_world",
  "mapId": "test_map_center",
  "displayName": "Test Map Center",
  "tileSize": 16,
  "width": 16,
  "height": 16,
  "worldX": 0,
  "worldY": 0,
  "baseTiles": [...],
  "overlayTiles": [...],
  "warps": [...]
}
```

The `worldId` scopes the `worldX`/`worldY` grid to this world only.

---

## Connection Types

### 1. Edge Transitions (intra-world)

Automatic. Player walks off the edge of a map, the game loads the neighbor at the adjacent grid position **within the same world**.

```
MapCatalog.GetNeighbor(currentMap, MapEdge.North)
  -> finds map where worldId == currentMap.WorldId
     AND worldX == currentMap.WorldX
     AND worldY == currentMap.WorldY - 1
```

No data needed in the map file. Path tiles (2) at edges serve as visual indicators for the player.

### 2. Warps (intra-world or inter-world)

Explicit. Defined in the map's `warps` array. Can target any map in any world.

```json
"warps": [
  {
    "x": 5,
    "y": 15,
    "targetMapId": "viridian_plaza",
    "targetX": 8,
    "targetY": 0,
    "trigger": "interact"
  }
]
```

| Field | Type | Description |
|-------|------|-------------|
| `x`, `y` | int | Source tile position on this map |
| `targetMapId` | string | Destination map (any world) |
| `targetX`, `targetY` | int | Arrival tile on destination map |
| `trigger` | string | `"step"` = automatic, `"interact"` = press button facing tile |

**Use cases:**
- `"interact"` — doors, cave entrances, PCs, elevators
- `"step"` — warp panels, teleporters, falling holes

### 3. Signs / Visual Markers

Tile 23 (Sign) is already used in `small_world` to mark entrance points. This is purely cosmetic — the actual transition is handled by edge auto-connect or warps. No schema change needed.

---

## Runtime Changes

### MapDefinition

Add `WorldId` property:

```csharp
public abstract class MapDefinition
{
    public string WorldId { get; }    // NEW
    public string Id { get; }
    public string Name { get; }
    public int WorldX { get; }
    public int WorldY { get; }
    // ... existing properties
}
```

Constructor gains a `worldId` parameter (first position, before `id`).

### MapCatalog.GetNeighbor()

Current behavior returns the first map at the target grid position globally. New behavior filters by world:

```csharp
public static MapDefinition? GetNeighbor(MapDefinition from, MapEdge edge)
{
    var (dx, dy) = edge switch
    {
        MapEdge.North => (0, -1),
        MapEdge.South => (0, 1),
        MapEdge.East  => (1, 0),
        MapEdge.West  => (-1, 0),
    };

    int targetX = from.WorldX + dx;
    int targetY = from.WorldY + dy;

    return GetAllMaps().FirstOrDefault(m =>
        m.WorldId == from.WorldId &&
        m.WorldX == targetX &&
        m.WorldY == targetY);
}
```

### WorldRegistry (new)

Simple static registry for world metadata. Loaded from embedded `world.json` files.

```csharp
public static class WorldRegistry
{
    public static WorldDefinition? GetWorld(string id);
    public static IEnumerable<WorldDefinition> AllWorlds { get; }
    public static void Initialize();  // loads all world.json from assets
}

public record WorldDefinition(
    string Id,
    string Name,
    string SpawnMapId,
    int SpawnX,
    int SpawnY
);
```

### GameWorld Changes

- `CurrentWorldId` property tracks which world the player is in
- On warp transition: if target map is in a different world, update `CurrentWorldId`
- `LoadWorld(string worldId)` — loads a world at its default spawn point
- Game start: `LoadWorld("small_world")` instead of `LoadMap("test_map_center")`

---

## Code Generation Changes

The map editor's `generateMapClass()` in `codeGenService.ts` needs to emit the `worldId` in the constructor call:

```csharp
private TestMapCenter()
    : base("small_world", "test_map_center", "Test Map Center",
           16, 16, 16, BaseTileData, OverlayTileData, WalkableTileIds)
```

Template change — `worldId` becomes the first argument to `base(...)`.

---

## Map Editor Changes

### Editor State

- Add `worldId: string` to the editor store (defaults to `"default"`)
- Add `warps: WarpDefinition[]` to the editor store (empty by default)
- Include both in JSON and C# export

### Editor UI

- **World ID field** in the sidebar/properties panel (text input)
- **Warp editor** — click a tile, define target map/position/trigger
- **World folder export** — export all maps in the current session as a world folder with `world.json`

These are additive — existing single-map editing works unchanged.

---

## Migration

Existing `small_world` maps need `worldId: "small_world"` added. The generated `.g.cs` files need regeneration with the new constructor signature.

Steps:
1. Add `worldId` to `MapDefinition` constructor (with default `""` for backward compat)
2. Update `MapCatalog.GetNeighbor()` to filter by world
3. Add `worldId` field to map JSON schema
4. Update map editor export to include `worldId`
5. Update code generator template
6. Regenerate existing maps
7. Create `WorldRegistry` and `WorldDefinition`
8. Update `GameWorld` to track current world
9. Add `world.json` to `small_world/` folder

---

## Example: Adding a New World

1. Create folder `maps/viridian_city/`
2. Create `world.json`:
   ```json
   { "id": "viridian_city", "name": "Viridian City", "spawnMapId": "viridian_plaza", "spawnX": 8, "spawnY": 14 }
   ```
3. Design maps in editor with `worldId: "viridian_city"` and appropriate `worldX`/`worldY`
4. Export maps to the folder
5. Add a warp in `small_world/test_map_c_bottom.map.json`:
   ```json
   { "x": 8, "y": 15, "targetMapId": "viridian_plaza", "targetX": 8, "targetY": 0, "trigger": "step" }
   ```
6. Generate C# classes, build, play — walking onto tile (8,15) on map_c warps to Viridian City
