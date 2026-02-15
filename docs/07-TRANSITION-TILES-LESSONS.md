# Lessons Learned & Handoff: Transition Tiles + World Registry

**Date:** 2026-02-15
**Sprint:** World Registry, Editor Cleanup, Transition Tile System

---

## 1. What We Accomplished

### World Registry System (Complete)
- Added `WorldId` as first parameter on `MapDefinition` constructor
- Created `WorldDefinition` record and `WorldRegistry` static class
- `MapCatalog.GetNeighbor()` now filters by `WorldId` so maps in different worlds don't conflict
- `GameWorld` tracks `CurrentWorldId`
- `Game1` startup uses `WorldRegistry` to find the default world and spawn map
- All 5 test maps tagged with `"small_world"` world ID

### Editor Cleanup (Complete)
- Removed JSON import/export from menu (C#-only workflow now)
- Removed Load Registry JSON menu item
- Added Export Registry C# (generates full `TileRegistry.cs`)
- Added World ID text input, Grid Position dropdown (Center/Up/Down/Left/Right), and X/Y number inputs to Sidebar
- Fixed C# parser (`parseCSharpMap`) to extract `worldId`, `worldX`, `worldY` so maps survive round-trips through the editor
- `codeGenService.ts` template includes `worldId`, `worldX`, `worldY` in generated constructor calls

### Transition Tile System (Partially Complete - Has Bugs)
- Added 4 new tile types to both registries (editor JSON + C# TileRegistry):
  - ID 112: Transition North (`#00cc88`)
  - ID 113: Transition South (`#00aaff`)
  - ID 114: Transition West (`#ff8800`)
  - ID 115: Transition East (`#cc44ff`)
- Category: `terrain` (goes to base layer in code gen)
- Added `Transition` to `TileCategory` enum (currently unused — tiles use `Terrain` category)
- Added `transition` to `codeGenService.ts` category mapping and `registryService.ts` category order
- Modified `TryEdgeTransition()` in `GameWorld.cs`:
  - Checks if player is on a transition tile matching the edge direction
  - Scans target map's opposite edge for receiving transition tile
  - Spawns player one tile inward from the edge
  - Falls back to position mirroring if no receiving tile found
- Placed transition tiles in all 5 generated `.g.cs` maps at path connection points

---

## 2. What Work Remains

### Bug 1: Transitions Not Triggering Consistently
Walking on or near the transition tile does not always fire the transition. Walking down the center of the tile or on either side of it sometimes fails to trigger. The player can walk onto the transition tile and nothing happens, or walks right past it off the map edge.

### Bug 2: Player Walks Off the Map
At non-transition edge tiles, the player walks to the very edge and off the map. There needs to be an invisible wall or boundary at non-transition edges to prevent this.

### Bug 3: Gap Next to Transition Tiles (from invisible wall attempt)
When invisible walls were added, one edge tile adjacent to each transition tile in every direction remained unblocked, letting the player slip through the gap next to the transition tile.

### Bug 4: Invisible Walls Extending Into Walkable Areas (from invisible wall attempt)
After spawning on a transition tile, turning left or right hit an invisible wall even though there was grass there. The invisible walls were blocking walkable tiles inside the map, not just preventing outward movement off the edge.

**Root cause of bugs 3 & 4:** The invisible wall implementation blocked entire edge tiles instead of just preventing outward movement. This both missed some tiles (gaps) and over-blocked others (trapping the player on the spawn tile). The invisible walls should prevent movement OFF the map without blocking any tiles ON the map — they must not take up tiles inside the map.

### Missing: Transition tile category in editor palette
The transition tiles are category `terrain` so they show up mixed in with grass/water/path. They should either be their own palette section or clearly visually distinct.

### Missing: TileCategory.Transition is defined but unused
The C# enum has `Transition` but the tile registry entries use `TileCategory.Terrain`. Need to decide: keep them as terrain (so code gen puts them in base layer) or use a separate category and update `isTerrainTile()` in code gen to treat transition as base-layer.

---

## 3. Optimizations — Where to Begin

### 3a. Fix the Invisible Wall Implementation
The invisible walls need to prevent the player from walking OFF the map at non-transition edges without blocking the edge tiles themselves. The fix is directional blocking: when the player is on a non-transition edge tile and tries to move outward (toward out-of-bounds), block that specific movement direction. The player can still walk freely along the edge and onto edge tiles — they just can't leave the map except through transition tiles. This can be done by adding a directional movement check in `GameWorld.Update()` before the player movement step, or by making `TileMap.IsWalkable()` context-aware (rejecting out-of-bounds movement from non-transition tiles).

### 3b. `isTerrainTile()` in Code Gen
Currently only `category === 'terrain'` goes to the base layer. Transition tiles are marked as terrain to force them into the base layer. A cleaner approach: add `'transition'` as a second base-layer category in `isTerrainTile()` so transition tiles can have their own category while still generating into `BaseTileData`.

### 3c. Transition Tile Scanning Performance
`TryFindReceivingTransition()` linearly scans the target map's edge every time a transition fires. For 16-tile maps this is fine. For larger maps, consider caching transition tile positions in `MapDefinition` at construction time (a dictionary of edge → position).

### 3d. Hardcoded Tile IDs in GameWorld
`TransitionNorthId = 112` etc. are hardcoded constants. If the registry changes, these break silently. Consider looking up transition tile IDs from `TileRegistry` at startup by category/name instead of hardcoding.

---

## 4. Step-by-Step to Get Fully Working

### Phase 1: Fix the invisible wall system
1. In `GameWorld.Update()`, add a directional check BEFORE `Player.Move()` — between reading input (step 2) and passing movement to the player (step 4)
2. The check: if the player is on a non-transition edge tile and the input direction points outward (off the map), cancel/nullify that movement direction so it never reaches `Player.Move()`
3. This fixes bug #1 (gap): Every non-transition edge tile is covered because the check applies to ALL edge positions — no tiles are skipped, no gaps possible
4. This fixes bug #2 (over-blocking): No tiles are blocked at all. The player can freely walk ON edge tiles and ALONG the edge. Only the outward direction is cancelled. No changes to `TileMap` needed — the invisible wall is purely a movement direction filter in `GameWorld.Update()`

### Phase 2: Clean up transition tile category
1. Update `isTerrainTile()` in `codeGenService.ts` to also return `true` for `'transition'` category
2. Change transition tiles in `default.json` from `category: "terrain"` to `category: "transition"`
3. Change transition tiles in `TileRegistry.cs` from `TileCategory.Terrain` to `TileCategory.Transition`
4. They'll still generate into `BaseTileData` but show in their own palette section

### Phase 3: Verify round-trip
1. Import a `.g.cs` map into the editor
2. Confirm transition tiles display correctly
3. Re-export and confirm the output matches the original
4. Run the game, test all 4 transitions from center map to each neighbor and back

### Phase 4: Remove dead code
1. Remove the `Transition` category from `TileCategory.cs` if going with terrain approach, OR update tile entries to use it (Phase 2)
2. Remove the fallback mirror-position code in `TryEdgeTransition()` if all maps will use transition tiles

---

## 5. How to Start/Test

### Run the Game
```bash
cd D:\Projects\PokemonGreen
dotnet run --project src/PokemonGreen/PokemonGreen.csproj
```
- Player spawns at (8, 8) on `test_map_center`
- Walk to a transition tile on any edge to test map transitions
- Transition tiles are at: North (8,0), South (8,15), West (0,9), East (15,9)

### Run the Map Editor
```bash
cd D:\Projects\PokemonGreen\src\PokemonGreen.MapEditor
npm run dev
```
- Open in browser at `http://localhost:5173`
- File > Import Map (C#) to load a `.g.cs` file
- File > Export Map (C#) to generate code
- Transition tiles appear in the Terrain palette section

### Verify Builds
```bash
# C# build
dotnet build D:\Projects\PokemonGreen\src\PokemonGreen\PokemonGreen.csproj

# TypeScript type check
cd D:\Projects\PokemonGreen\src\PokemonGreen.MapEditor && npx tsc --noEmit
```

Both must pass with 0 errors. Currently clean.

---

## 6. Known Issues + Strategies

### Issue 1: Transitions not triggering consistently
**Current state:** Walking on or near the transition tile does not always fire the transition. Walking down the center or on either side of the tile sometimes fails to trigger. The player can walk onto the transition tile and nothing happens.
**Strategy A — Widen the trigger zone:** The edge transition check in `GameWorld.Update()` only fires when `IsAtMapEdge()` is true AND the player is on the exact transition tile AND moving outward — all in the same frame. If the player's sub-tile position hasn't fully reached the edge row/column, the check doesn't fire. Consider triggering the transition as soon as the player steps onto a transition tile that is on an edge, regardless of whether they've reached the absolute edge pixel.
**Strategy B — Check transition tiles during Player.Update:** Instead of checking only in the edge-transition block at step 8, also check after `Player.Update()` if the player's tile changed to a transition tile on an edge. This catches cases where the player arrives at the tile mid-frame.
**Strategy C — Expand edge detection:** `IsAtMapEdge()` uses `Player.TileY <= 0` etc. If `TileY` is calculated from a rounded/truncated float position, the player might be visually on the edge tile but `TileY` hasn't updated yet. Verify that `TileY`/`TileX` update before the edge check runs.

### Issue 2: Player walks off the map at non-transition edges
**Current state:** At non-transition edge tiles, the player walks to the very edge and off the map. There is no boundary preventing this.
**Strategy A — Pre-movement direction cancellation:** In `GameWorld.Update()`, before calling `Player.Move()`, check if the player is on a non-transition edge tile and the input direction points outward. If so, cancel that movement direction. Player can still walk along the edge freely, just not outward. No tiles blocked, no interior impact. Cleanest approach.
**Strategy B — Edge tile position clamping:** After `Player.Update()`, if the player is on a non-transition edge tile, clamp their sub-tile position so they don't drift past the center of the tile toward the edge. Prevents the visual "walking off" without blocking any tile.
**Strategy C — Out-of-bounds walkability override:** Override `TileMap.IsWalkable()` to reject movement into out-of-bounds tiles from non-transition edge tiles. When `IsWalkable(x, y)` is called for an out-of-bounds coordinate, check if the source tile is a transition tile — if not, return false. Keeps all map tiles fully walkable. Requires passing source tile context into the walkability check.

### Issue 3: Transition tile category confusion
**Current state:** Transition tiles are `TileCategory.Terrain` in C# and `category: "terrain"` in JSON, mixed in with grass/water/path in the editor palette.
**Strategy:** Create a proper `transition` base-layer category. Update `isTerrainTile()` to treat transition as base-layer. Tiles get their own palette section.

### Issue 4: Hardcoded transition tile IDs
**Current state:** `GameWorld.cs` has `private const int TransitionNorthId = 112;` etc.
**Strategy:** At game startup, query `TileRegistry` for tiles with `TileCategory.Transition` and build the ID mapping dynamically. Or use a naming convention and look up by name.

### Issue 5: Single transition tile per edge
**Current state:** `TryFindReceivingTransition()` finds the FIRST matching tile and stops. Only supports one transition point per edge per map.
**Strategy:** Support multiple transition tiles per edge. Match source→target by relative position along the edge (e.g., 3rd transition tile on source's north edge maps to 3rd transition tile on target's south edge).

---

## 7. Architecture & Features

### Current Architecture
```
GameWorld.Update() loop:
  1. Read input
  2. Check interact warps (doors)
  3. Move player
  4. Update player position
  5. Check step warps
  6. Check edge transitions ← transition tile logic here
  7. Update camera

Map loading:
  MapDefinition → TileMap (runtime)
  MapCatalog: static map registry, auto-neighbor by worldX/worldY
  WorldRegistry: world metadata + spawn points

Edge transition flow:
  IsAtMapEdge() → TryEdgeTransition()
    → IsTransitionTileForEdge(baseTile, edge)
    → Find neighbor (MapConnection or auto-neighbor)
    → TryFindReceivingTransition(target, edge)
    → BeginTransition() → fade out → LoadMap → fade in
```

### Quick Wins
1. **Fix transition triggering** — Investigate why transitions don't fire consistently (Issue 1). Check if `IsAtMapEdge()` + `TileY`/`TileX` timing is causing missed triggers. May need to trigger on tile arrival rather than requiring all conditions in one frame.
2. **Fix edge boundaries** — Implement Strategy A from Issue 2 (pre-movement direction cancellation). ~15 lines in `GameWorld.Update()`. No TileMap changes needed. Player walks freely on edge tiles but can't move outward unless on a transition tile.
3. **Transition palette section** — Change `isTerrainTile()` to include `'transition'`, flip tile category to `"transition"`. ~5 lines changed, tiles get their own editor section.
4. **Remove fallback mirror code** — Delete the fallback position-mirroring in `TryEdgeTransition()`. All maps should use transition tiles. Simplifies the method.
5. **Cache transition positions** — In `MapDefinition` constructor, scan edges once and store transition tile positions in a dictionary. Eliminates per-frame scanning.

### New Feature Ideas
- **Bidirectional transition tiles** — A single "Border" tile type that works in any direction based on which edge it's placed on. Reduces 4 tile types to 1, with direction inferred from position.
- **Multi-tile transitions** — Place 2-3 transition tiles side by side for wider passage entry points. `TryFindReceivingTransition` would need to handle multiple matches.
- **Transition tile rendering** — Give transition tiles a directional arrow sprite so map designers can see which way they connect at a glance in-game.
- **World map view** — Editor feature showing all maps in a world arranged by their grid positions, with transition tiles highlighted and connections drawn between maps.

---

## 8. Files Modified This Sprint

| File | Changes |
|------|---------|
| `Core/GameWorld.cs` | Transition tile checking, spawn offset, edge helpers |
| `Core/Maps/TileCategory.cs` | Added `Transition` enum value |
| `Core/Maps/TileRegistry.cs` | Added 4 transition tile entries (112-115) |
| `Core/Maps/Generated/*.g.cs` | Transition tiles in BaseTileData at edge positions |
| `MapEditor/data/registries/default.json` | 4 transition tiles + transition category |
| `MapEditor/services/codeGenService.ts` | `transition: 'Transition'` mapping |
| `MapEditor/services/registryService.ts` | `'transition'` in category order, worldX/worldY parsing |
| `MapEditor/store/editorStore.ts` | worldX/worldY in importCSharpMap |
| `MapEditor/components/layout/Sidebar.tsx` | World ID input, grid position dropdown |
