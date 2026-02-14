# Lessons Learned & Handoff Document

**Date:** 2026-02-14
**Sprint:** Core Rewrite, Map Pipeline, Visual Rendering

---

## 1. What We Accomplished

### Core Engine Rewrite (Camera, Player, GameWorld)
- **Camera.cs** — Rebuilt from scratch. Follows player, clamps to map bounds, produces a transform matrix for SpriteBatch. Zoom scales proportionally with viewport height (`viewportHeight / (VisibleTilesY * TileSize)`) so tile count stays consistent at any window size.
- **Player.cs** — Intent-based movement system. `Move()` stores direction/speed intent, `Update()` applies with real deltaTime for frame-independent movement. Vertical jump with sine arc and horizontal momentum glide. Axis-aligned collision sliding (when diagonal is blocked, tries X-only then Y-only).
- **GameWorld.cs** — Slim 9-step update loop: read input → check interact warps → apply movement → update player → check step warps → check edge transitions → update camera. Fade transitions (black overlay) for map changes.

### Map System
- **MapDefinition** — Abstract base class with flat tile arrays, walkable set, warps, connections, world grid coordinates (worldX/worldY). Auto-registers in MapCatalog via constructor.
- **MapCatalog** — Static registry with `TryGetMap`, `GetNeighbor` (finds adjacent maps by grid position), `RegisterOrReplace`, `Clear`.
- **MapRegistry** — Reflection-based discovery. Scans assembly for MapDefinition subclasses, triggers their singleton `Instance` properties. No hardcoded map list — drop in a `.g.cs` file and it just works.
- **WarpConnection** — Step warps (trigger on walk-over) and Interact warps (trigger on E/Enter while facing tile). MapEdge/MapConnection for seamless edge transitions between adjacent maps.

### Map Pipeline (MapGen CLI)
- **generate** — Converts `.map.json` → `.g.cs` (C# singleton classes). Handles base tiles, overlay tiles, walkable IDs, warps, connections, worldX/worldY.
- **export** — Converts `.g.cs` → `.map.json` (round-trip). Parses generated C# via regex.
- **export-registry** — Exports TileRegistry to JSON matching the MapEditor's exact TypeScript schema (`{id, name, version, categories: [{id, label, showInPalette}], tiles: [{id, name, color, walkable, category}]}`).

### Small World (5 Maps)
- **test_map_center** (0,0) — Open hub area with grass, paths, water features, decorations. Connects to all 4 neighbors.
- **test_map_a** (0,-1) — North. Walled building in void, path exit at bottom toward center.
- **test_map_b** (-1,0) — West. Walled building in void, open right edge toward center.
- **test_map_c** (0,1) — South. Walled building in void, path exit at top toward center.
- **test_map_d** (1,0) — East. Walled building in void, open left edge toward center.

### Window Resize
- Deferred resize pattern: `OnClientSizeChanged` sets a `_windowResized` flag; `Update()` processes it. Avoids re-entrancy (ApplyChanges inside the event fires the event again) and NullReferenceException (event fires before GameWorld is constructed during Initialize).
- Zoom scales proportionally with viewport: `zoom = viewportHeight / (VisibleTilesY * TileSize)`. No discontinuous jumps on resize.

### Rendering Fixes
- **Premultiplied alpha** — `Texture2D.FromStream` loads PNGs with straight alpha, but `SpriteBatch + BlendState.AlphaBlend` expects premultiplied. Added conversion in `TextureStore.LoadFromFile` so transparent areas no longer bleed black.
- **Per-tile visual scale** — Trees (1.8x, canopy extends above), bushes (1.4x), boulders (1.3x) render larger; flowers (0.55x) render as small ground accents. Bottom-aligned, horizontally centered.

### Map Editor (React/TypeScript/Vite)
- Zustand store with slices (map, palette, history)
- Canvas grid rendering with mouse painting
- Hover preview and building placement overlay
- Map rotation (CW/CCW)
- Collapsible VS Code-style sidebar (Palette + Controls)
- Import/export `.map.json` with tile_registry.json

---

## 2. What Work Remains

### High Priority
- [ ] **Edge transitions not fully tested** — The auto-neighbor system (`MapCatalog.GetNeighbor`) uses worldX/worldY to find adjacent maps, but the actual player teleport on reaching a map edge needs play-testing. Edge tiles may not line up (center map has no void border, but surrounding maps have 2-row void padding).
- [ ] **Player spawn position on edge transition** — Currently the player lands at the mirrored edge tile. Needs validation that the target tile is actually walkable (void tiles 0 are not).
- [ ] **Interact warp (E key) untested** — The code path exists but no maps have interact warps defined in their JSON yet (no doors placed).
- [ ] **SoundManager** — File exists but is a skeleton. No BGM or SFX playback implemented.

### Medium Priority
- [ ] **Sprite consistency** — Most tile sprites are 32x32 but `tile_tree.png` and `tile_flower.png` are 64x64. Visual scale overrides work as a band-aid but proper 16x16 or 32x32 sprites would be cleaner.
- [ ] **Player animation frames** — PlayerRenderer reads `AnimationFrame` from Player, but the frame cycling logic in Player.cs needs review. Walk/run/jump may not advance frames correctly.
- [ ] **Overlay tile rendering** — Overlay layer exists in the data model and renderer but no maps currently use overlay tiles (all null). Needs a map with overlay content to verify rendering order.
- [ ] **Map Editor ↔ Game round-trip validation** — Edit a map in the editor, export, generate, run. The full pipeline works mechanically but hasn't been tested end-to-end for visual correctness.

### Low Priority
- [ ] **NPC/Entity rendering** — Tile IDs 48-71 (NPCs, trainers) are defined in the registry but have no sprites or behavior.
- [ ] **Interactive tile behaviors** — Doors (32), warps (33), switches (42), etc. have behavior strings in TileDefinition but no runtime handler.
- [ ] **Battle system** — TallGrass (72) has a "wild_encounter" behavior but there's no encounter/battle logic.
- [ ] **Save/Load** — No persistence at all.

---

## 3. Optimization Suspects

### 3.1 Premultiplied Alpha Conversion on Every Load
`TextureStore.LoadFromFile` reads all pixels, converts, and writes back for every PNG. For many sprites this is fine, but if sprite count grows:
- **Fix:** Pre-process sprites at build time (e.g., a content pipeline step that premultiplies alpha in the PNGs themselves). Or use MonoGame's Content Pipeline (`.mgcb`) which handles this automatically.

### 3.2 TileRenderer Creates Textures Per-Tile at Runtime
`GetTileTexture` creates a `Texture2D` (1x1 solid color fallback) per unique tile ID on first draw. This causes GC pressure and GPU allocations during gameplay.
- **Fix:** Pre-bake all fallback color textures in a single `Initialize()` call. Or batch all tiles into a texture atlas.

### 3.3 Dictionary Lookups Per Tile Per Frame
`DrawTile` calls `TileRegistry.GetTile(tileId)` and `_tileTextures.TryGetValue(tileId)` and `_visualScale.TryGetValue(tileId)` on every tile every frame. With a 16x16 map and ~7x5 visible tiles, that's ~105 lookups per layer per frame.
- **Fix:** Build a flat array indexed by tileId at load time: `Texture2D[] textureByTileId = new Texture2D[104]`. Single array index replaces three dictionary lookups.

### 3.4 No Culling Optimization for Large Maps
`DrawMap` culls tiles outside `camera.Bounds`, which is correct. But bounds calculation uses integer division that can over-include tiles. For larger maps (32x32+), consider spatial hashing or chunk-based rendering.
- **Fix:** Not needed yet at 16x16, but the architecture should support chunks when maps grow.

---

## 4. Step-by-Step: Get the App Fully Working

### Prerequisites
- .NET 9 SDK
- MonoGame 3.8.2+ (DesktopGL)
- Node.js 18+ (for MapEditor)

### Build & Run the Game
```bash
cd D:\Projects\PokemonGreen

# Build the full solution (Core + MapGen + Game)
dotnet build

# Run the game
dotnet run --project src/PokemonGreen
```

### Controls
| Key | Action |
|-----|--------|
| WASD / Arrow Keys | Move |
| Shift (hold) | Run |
| Space | Jump |
| E / Enter | Interact |
| Escape | Quit |

### Map Pipeline
```bash
# Generate C# from map JSON
dotnet run --project src/PokemonGreen.MapGen -- generate \
  --input maps/exported/small_world \
  --output src/PokemonGreen.Core/Maps/Generated

# Export C# maps back to JSON
dotnet run --project src/PokemonGreen.MapGen -- export \
  --input src/PokemonGreen.Core/Maps/Generated \
  --output maps

# Export tile registry for the map editor
dotnet run --project src/PokemonGreen.MapGen -- export-registry \
  --output maps/tile_registry.json
```

### Map Editor
```bash
cd src/PokemonGreen.MapEditor
npm install
npm run dev
# Opens at http://localhost:5173
# Load tile_registry.json, then import/export .map.json files
```

### Full End-to-End Workflow
1. Open MapEditor → load `maps/tile_registry.json`
2. Create or edit a map → export as `.map.json` to `maps/exported/small_world/`
3. Add `worldX`/`worldY` to the JSON for grid positioning
4. Run `generate` command to produce `.g.cs` in `Maps/Generated/`
5. `dotnet build` → `dotnet run --project src/PokemonGreen`
6. Walk to map edge → auto-transition to neighbor map via worldX/worldY

---

## 5. How to Start/Test

### Quick Smoke Test
```bash
dotnet build && dotnet run --project src/PokemonGreen
```
- Game should open an 800x600 window showing `test_map_center`
- Player spawns at center (8,8) on grass near a water feature
- WASD to walk, Shift+WASD to run, Space to jump
- Walk to any map edge → should fade-transition to the adjacent map
- Resize window → zoom scales proportionally, no black borders or jumps

### MapGen Smoke Test
```bash
# Round-trip test: export existing maps, re-generate, compare
dotnet run --project src/PokemonGreen.MapGen -- export \
  --input src/PokemonGreen.Core/Maps/Generated \
  --output maps/roundtrip-test

dotnet run --project src/PokemonGreen.MapGen -- generate \
  --input maps/roundtrip-test \
  --output src/PokemonGreen.Core/Maps/Generated

dotnet build  # should compile with no errors
```

### Unit Testing (Not Yet Set Up)
No test project exists yet. When added:
- `TileRegistry.GetTile(1)` should return Grass, walkable
- `MapCatalog.GetNeighbor(center, MapEdge.North)` should return test_map_a
- `Camera.CalculateZoom` at various viewport sizes should scale proportionally
- Player collision: can't walk into wall tiles, can walk on grass

---

## 6. Known Issues & Strategies

### Issue 1: Edge Transition Target May Land on Void Tile
Maps A/B/C/D have 2-row void (tile 0) borders. When transitioning from center (no void border) to a neighbor, the target Y/X might land on row 0 or 1 which is void/unwalkable.

**Strategies:**
1. **Walkability scan** — After computing the target position in `TryEdgeTransition`, scan inward from the edge until a walkable tile is found.
2. **Map-defined entry points** — Add an `entryPoints` array to `.map.json` specifying valid arrival coordinates per edge (North/South/East/West).
3. **Void border removal** — Redesign the surrounding maps to not use void borders. Use wall tiles (80) flush to the edge instead, with walkable tiles on the very edge row.

### Issue 2: Sprite Size Inconsistency
Tiles are 16x16 world units but sprite PNGs are a mix of 32x32 and 64x64. The visual scale dictionary is a manual override per tile ID.

**Strategies:**
1. **Normalize all sprites to 16x16** — Re-export or regenerate all tile sprites at 16x16 pixels (matching world TileSize). Removes the need for any scale overrides.
2. **Auto-scale from texture metadata** — Read the texture dimensions at load time and compute scale automatically: `scale = referenceSize / texture.Width`. No manual dictionary needed.
3. **Texture atlas with uniform cells** — Pack all tile sprites into a single atlas with 16x16 or 32x32 cells. The renderer samples from fixed-size cells, eliminating per-tile size variance.

### Issue 3: Zoom Level May Feel Wrong on Different Monitors
`VisibleTilesY = 5` is hardcoded. On a 4K monitor maximized, tiles appear huge (418px each). On a small laptop, they may feel too zoomed in.

**Strategies:**
1. **User-configurable zoom** — Add a settings file or in-game slider for VisibleTilesY (range 3-10). Store in a `settings.json`.
2. **DPI-aware scaling** — Query the OS DPI scaling factor and adjust VisibleTilesY accordingly. Higher DPI → show more tiles.
3. **Zoom key bindings** — Add +/- keys to adjust zoom at runtime. Clamp between 3 and 12 visible tiles.

### Issue 4: No Error Recovery on Missing Sprites or Maps
If a sprite PNG is missing, the renderer falls back to a solid color block silently. If a target map ID doesn't exist in MapCatalog, the warp fails silently.

**Strategies:**
1. **Debug overlay** — In debug builds, render missing sprites as magenta with the tile ID number drawn on top. Log warnings to console.
2. **Startup validation** — On game launch, iterate all tile IDs and log which ones have sprites vs. fallback colors. Flag any map warps that reference non-existent map IDs.
3. **Hot-reload watcher** — Watch the sprites directory for changes and reload textures without restarting. Speeds up art iteration.

---

## 7. Architecture Summary

```
PokemonGreen.sln
├── src/PokemonGreen.Core/          # Shared engine (no MonoGame dependency)
│   ├── GameWorld.cs                 # 9-step update loop, map transitions
│   ├── Maps/
│   │   ├── TileRegistry.cs          # 104 tile definitions (8 categories)
│   │   ├── TileMap.cs               # 2D grid with base + overlay layers
│   │   ├── MapDefinition.cs         # Abstract base, auto-registers in catalog
│   │   ├── MapCatalog.cs            # Runtime map lookup, neighbor resolution
│   │   ├── MapRegistry.cs           # Reflection-based map discovery
│   │   ├── WarpConnection.cs        # Step/Interact warps, edge connections
│   │   └── Generated/               # .g.cs map singletons (5 maps)
│   ├── Player/
│   │   ├── Player.cs                # Intent-based movement, jump, collision
│   │   ├── Direction.cs             # Up/Down/Left/Right + ToVector()
│   │   └── PlayerState.cs           # Idle/Walk/Run/Jump/Climb/Combat/Spellcast
│   └── Systems/
│       ├── Camera.cs                # Follow, clamp, transform matrix
│       ├── InputManager.cs          # WASD, Shift, Space, E/Enter
│       └── SoundManager.cs          # Skeleton (not implemented)
│
├── src/PokemonGreen/               # MonoGame DesktopGL executable
│   ├── Game1.cs                     # Init, deferred resize, update/draw
│   └── Rendering/
│       ├── TileRenderer.cs          # Sprite loading, animation, visual scale
│       ├── PlayerRenderer.cs        # Sprite sheet animation (64x64 frames)
│       └── TextureStore.cs          # PNG loading with premultiplied alpha
│
├── src/PokemonGreen.MapGen/        # CLI tool for map pipeline
│   ├── Commands/
│   │   ├── GenerateCommand.cs       # .map.json → .g.cs
│   │   ├── ExportCommand.cs         # .g.cs → .map.json
│   │   └── ExportRegistryCommand.cs # TileRegistry → JSON
│   ├── Models/MapJsonModel.cs       # JSON schema (v2)
│   └── Services/
│       ├── CodeGenerator.cs         # C# code generation with templates
│       ├── MapParser.cs             # JSON deserialization
│       └── RegistryExporter.cs      # Registry → editor-compatible JSON
│
├── src/PokemonGreen.MapEditor/     # React/TS/Vite web app
│   └── (Zustand store, Canvas, Palette, Sidebar, map I/O)
│
├── maps/                            # Map data files
│   ├── exported/small_world/        # 5 map JSONs with worldX/worldY
│   └── tile_registry.json           # Editor-compatible registry export
│
└── docs/
    ├── 00-REBUILD-PLAN.md           # Original architecture plan
    └── 01-LESSONS-LEARNED.md        # This document
```

---

## 8. Quick Wins & Feature Ideas

### Quick Wins (< 1 hour each)
1. **Add warp doors between maps** — Place door tiles (ID 32) in the map JSON with interact-trigger warps. The runtime handler already exists in GameWorld step 3.
2. **Animated water** — Already works! `tile_water_0.png` through `tile_water_3.png` exist and TileRenderer auto-detects numbered sequences. Just place water tiles (ID 0) on maps.
3. **Zoom key bindings** — Add +/- key handling in InputManager, expose a `ZoomDelta` property, and apply it in GameWorld to adjust VisibleTilesY at runtime.
4. **Debug tile ID overlay** — In a debug draw pass, render the tile ID number on each tile using a SpriteFont. Massively helps map debugging.

### Feature Ideas (1-4 hours each)
5. **NPC system** — Entity tiles (48-55) placed on maps spawn NPC instances. Simple state: face direction, optional patrol path, interact triggers dialogue text box.
6. **Dialogue box** — Fixed-height text box at screen bottom. Typewriter text reveal. Triggered by interact (E) on NPCs or signs.
7. **Encounter system** — Walking on TallGrass (72) rolls a random chance per step. If triggered, freeze player, flash screen, transition to a placeholder battle screen.
8. **Minimap** — Render the full TileMap at 1px per tile in a corner overlay. Highlight player position. Shows the world grid with all discovered maps.
9. **Map streaming** — Instead of fade transitions, load adjacent maps and render them seamlessly at the edges. Camera pans smoothly across map boundaries.

### Architectural Improvements
10. **Content Pipeline** — Move sprite loading from `Texture2D.FromStream` to MonoGame Content Pipeline (`.mgcb`). Handles premultiplied alpha, compression, and platform-specific formats automatically. Eliminates the manual alpha conversion.
11. **ECS Architecture** — The current Player/NPC model will get unwieldy with more entity types. Consider a lightweight entity-component-system (even just a `List<Entity>` with component bags) before adding trainers, items, and NPCs.
12. **Tile Behavior System** — Replace the string-based `OverlayBehavior` in TileDefinition with a proper `ITileBehavior` interface. Behaviors like "slippery", "slow", "wild_encounter" become composable strategy objects rather than switch-case strings.

---

## Key Lessons

1. **MonoGame's `Texture2D.FromStream` does not premultiply alpha.** This is the single most common cause of "black background on PNG sprites." Always convert after loading, or use the Content Pipeline.

2. **Window resize in MonoGame DesktopGL is re-entrant.** Calling `ApplyChanges()` inside `ClientSizeChanged` fires the event again. Always defer resize processing to the Update loop with a flag.

3. **Zoom should scale with viewport, not be fixed.** A fixed base zoom causes discontinuous jumps when fillZoom kicks in at certain window sizes. Proportional zoom (`vpH / (tiles * tileSize)`) eliminates all resize artifacts.

4. **Reflection-based map discovery beats hardcoded registries.** `MapRegistry.Initialize()` scans for MapDefinition subclasses automatically. Add or delete `.g.cs` files freely — no registration code to maintain.

5. **The MapEditor's tile registry schema is strict.** Version must be a string, categories need `{id, label, showInPalette}`, tiles go in a flat top-level array (not nested in categories). Mismatch = silent failure with no tiles showing.

6. **World grid coordinates (worldX/worldY) enable auto-neighbor resolution.** No need for explicit connection declarations between maps. `MapCatalog.GetNeighbor` just finds the map at the adjacent grid cell. Clean and scalable.
