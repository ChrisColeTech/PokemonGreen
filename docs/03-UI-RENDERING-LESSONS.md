# Lessons Learned & Handoff Document v3

**Date:** 2026-02-14
**Sprint:** UI Enhancements — Transparency Fix, Walk-Behind Rendering, Tall Grass Density

---

## 1. What We Accomplished

### Transparency Fix (Root Cause Found & Resolved)
The black backgrounds on transparent sprites (trees, flowers, etc.) were **NOT an alpha/blending issue**. The root cause was that non-terrain tiles (trees, rocks, flowers) were placed in the **base tile layer only**, with no terrain underneath. Transparent pixels had nothing behind them, so the `GraphicsDevice.Clear(Color.Black)` showed through.

**Fix:** Auto layer splitting in `CodeGenerator.cs`:
- Non-terrain tiles in the base layer are moved to the overlay layer
- The base layer gets filled with `Grass (ID 1)` as a default background
- `IsGroundTile()` checks `TileCategory.Terrain` — everything else becomes overlay

**Secondary fix:** Removed `ApplyBorderTransparencyFallbackIfNeeded()` call in `AssetLoader.cs`. This method was flood-filling uniform terrain sprites (path, water) to transparent, destroying them.

**Alpha pipeline:** Straight alpha + `BlendState.NonPremultiplied` is the correct combination. Premultiplied alpha was a dead end.

### Walk-Behind Rendering (Y-Sorted Overlays)
Split `TileRenderer.DrawMap()` into three methods for proper depth:
```
DrawBaseTiles()                    → all terrain
DrawOverlaysBehindPlayer()         → overlays at y <= playerTileY
[player draws here]
DrawOverlaysInFrontOfPlayer()      → overlays at y > playerTileY
```

**Result:** Player walks behind trees, flowers, and other overlays when moving north (higher Y tiles draw on top). Walking south, the player appears in front.

### Tall Grass SVG Redesign
Replaced original rectangular blob `<rect>` grass with `<polygon>` blade shapes:
- Two sets of blades per SVG: darker back set + brighter front set
- 30+ blades per tile with varying heights (y=7 to y=20)
- 5 SVG files: base + 4 animation frames with sway offsets
- Animation: blade tips shift +/- 1px per frame for wind effect

### SVG-to-PNG Pipeline Fix
`tools/convert_sprites.js` output path corrected from old `Content/sprites/` to `src/PokemonGreen.Assets/Sprites/`. Now skips `_v2` SVG files (experimental).

### Key Files Modified
| File | Change |
|------|--------|
| `CodeGenerator.cs` | Auto layer splitting — non-terrain base tiles → overlay |
| `AssetLoader.cs` | Removed destructive border transparency fallback |
| `TileRenderer.cs` | Split into 3 draw methods + 2x2 grass grid hack |
| `Game1.cs` | Interleaved player draw between overlay passes |
| `tile_tall_grass*.svg` (×5) | Redesigned with dual-layer polygon blades |
| `convert_sprites.js` | Fixed output path, added v2 skip filter |
| `*.g.cs` map files (×5) | Regenerated with proper base/overlay layers |

---

## 2. What Work Remains

### Tall Grass Density (IN PROGRESS)
The grass blades look good individually but there are visible gaps/rows when tiled across the map. Multiple approaches tried:

| Approach | Result |
|----------|--------|
| More blades in SVG (22→31) | Still sparse at rendered size |
| 2x2 sub-tile grid in renderer | Visible seams, doesn't close gaps |
| 3x3 sub-tile grid | Same issue, smaller but still visible |
| Offset second pass (left/up) | Y-sorting breaks for out-of-bounds sub-tiles |
| Compositing v1+v2 via sharp | Opaque base rect covers background layer |
| Dual blade sets in single SVG | Better density but gaps persist between tile rows |

**Core problem:** Polygon blades taper to points. At any Y above the base rect, there's transparent space between tips. When tiles are placed in a grid, this creates visible horizontal bands of sparse green alternating with dense green.

### Key Discovery: Tile Size vs Sprite Size
- `GameWorld.TileSize` is **16px** — this is the grid cell size, NOT the sprite size
- SVGs are 32×32 and get scaled down to fit 16×16 cells at runtime
- Changing the rendered sub-tile size (2x2, 3x3 grids) does NOT fix the gap because the gap is inherent to the SVG design — blades taper to points regardless of render scale
- The tile size is not the cause of the gaps; the SVG layout is

### Changing the Tile Size

**Global tile size** is set in `src/PokemonGreen.Core/GameWorld.cs`:
```csharp
public const int TileSize = 16; // line 11
```
This is used everywhere — rendering, collision, camera, player movement. Changing it affects ALL tiles (terrain, trees, water, grass, everything). There is no per-tile-type size setting in the engine.

**There is no way to change only the grass tile size.** The tile grid is uniform. Options considered:

1. **Reduce global TileSize (e.g. 16→8):** Would make grass denser but also makes ALL tiles tiny. Maps would need 4× more cells to cover the same area. All sprites, player movement speed, camera bounds, and collision would need retuning.

2. **Renderer sub-tile hack (tried):** Draw the grass sprite multiple times at smaller sizes within one cell. This is purely visual — the collision/map grid stays at 16px. Works by passing a smaller destination `Rectangle` to `spriteBatch.Draw()`, which scales the texture down:
   ```csharp
   // In TileRenderer.DrawTile(), special case for tileId == 72 (TallGrass):
   int half = tileSize / 2;  // 8px sub-tiles (use tileSize/3 for ~5px)
   for (int gx = 0; gx < 2; gx++)
       for (int gy = 0; gy < 2; gy++)
           spriteBatch.Draw(texture,
               new Rectangle(worldX + gx * half, worldY + gy * half, half, half),
               null, Color.White);
   return; // skip normal draw
   ```
   **Limitations:** Doesn't fully fix gaps (SVG blade design creates gaps at any scale). Sub-tiles that extend outside the cell boundary break Y-sorted walk-behind rendering. Multiplies draw calls (4× for 2×2, 9× for 3×3).

3. **Overlay sub-grid (not implemented):** Add a second finer grid (e.g. 8×8) just for the overlay layer. Would require significant refactoring of TileMap, TileRenderer, CodeGenerator, and all map files. Most invasive option but would properly solve the density problem for grass without affecting other tiles.

### Current Approach: Dual-Row SVG with Higher DPI
Latest SVG redesign uses two rows of blades within one tile:
- **Top row:** bases at y=20, tips reaching y=0-10 (8 wider blades, 4px bases)
- **Bottom row:** bases at y=32, tips reaching y=10-18 (8 blades, overlaps top row bases)
- Bottom row covers/hides the top row's base line, creating a seamless look
- Base rect reduced to y=28-32 (4px) to not dominate the tile
- `convert_sprites.js` density increased from 72 to 144 DPI for sharper rendering
- Fewer but wider blades (4px instead of 3px) fill more horizontal space

### Other Remaining Work
- [ ] Solve tall grass tile gap/density issue
- [ ] Verify walk-behind works correctly for all overlay types
- [ ] Clean up experimental v2 SVG files from `Assets/Sprites/`
- [ ] Remove the 2x2 grass grid hack from `TileRenderer.cs` (or replace with proper solution)
- [ ] Test edge-of-map rendering with the expanded bounds
- [ ] Commit clean state

---

## 3. Optimization Suspects

### 3.1 Grass Rendering: 2x2 Grid Multiplies Draw Calls
The current hack draws tall grass 4× per tile (2×2 grid). In a 50×50 visible area with 30% grass coverage, that's ~3000 extra draw calls per frame.
- **Fix:** Bake the density into the PNG at build time, not at runtime
- **Alternative:** Use a texture atlas with pre-composited dense grass tiles

### 3.2 Dictionary Lookups in Hot Path
`DrawTile()` performs `TileRegistry.GetTile()`, `_visualScale.TryGetValue()`, and `TextureStore.GetTileTexture()` per tile per frame. With ~2500 visible tiles, that's 7500+ dictionary lookups per frame.
- **Fix:** Pre-resolve tile textures into a flat `Texture2D[]` array indexed by tileId at initialization

### 3.3 Animated Tile Frame Discovery
`GetAnimatedFrame()` probes for frames on first access with a loop calling `TextureStore.GetAnimatedTileFrame()` until null. This happens once per tile type, but the lazy initialization pattern is fragile.
- **Fix:** Pre-discover all animated frames during `TextureStore.Initialize()`

### 3.4 No Sprite Batching/Atlasing
Each tile is a separate `Texture2D`. MonoGame's SpriteBatch breaks batches on texture changes, causing excessive GPU state switches.
- **Fix:** Pack all tile sprites into a single atlas. Use `sourceRectangle` parameter in `SpriteBatch.Draw()`

---

## 4. Step-by-Step: Get App Fully Working

### Prerequisites
- .NET 9 SDK
- Node.js (for SVG conversion)
- MonoGame 3.8.4+ (DesktopGL)

### 1. Convert SVGs to PNGs
```bash
cd D:\Projects\PokemonGreen
node tools/convert_sprites.js
```

### 2. Generate Map Code (if maps changed)
```bash
dotnet run --project src/PokemonGreen.MapGen
```

### 3. Build
```bash
dotnet build
```

### 4. Run
```bash
dotnet run --project src/PokemonGreen
```

### 5. Verify
- Window opens at 800×600
- Map renders with grass terrain, trees, flowers, water, paths
- WASD to move, Shift to run
- Trees/flowers should NOT have black backgrounds
- Walking north behind a tree should occlude the player
- Tall grass animates (blade sway)

---

## 5. How to Start/Test

### Quick Smoke Test
```bash
dotnet build && dotnet run --project src/PokemonGreen
```

### Verify Layer Splitting
Open any `*.g.cs` file in `src/PokemonGreen.Core/Maps/Generated/`. Check that:
- `BaseTiles` contains only terrain IDs (1=Grass, 2=Water, 3=Path, etc.)
- `OverlayTiles` contains decoration IDs (16=Tree, 18=Flower, 72=TallGrass, etc.)
- No non-terrain IDs appear in `BaseTiles`

### Verify Transparency
Run the game. Trees, flowers, and rocks should blend smoothly over the green grass terrain — no black rectangles, no harsh edges.

### Verify Walk-Behind
Walk the player north (W key) behind a tree. The tree should draw on top of the player sprite. Walk south — the player should appear in front of the tree.

---

## 6. Known Issues & New Strategies

### Issue 1: Tall Grass Tile Density Gaps (ACTIVE)
Visible horizontal banding in grass areas. Dense green bands (blade bases + rect) alternate with sparse bands (blade tips with transparent gaps).

#### Strategy A: Seamless Tiling SVG
Redesign the SVG so blades at the TOP of the tile visually connect with blades at the BOTTOM of the tile above. Use a tiling pattern where blade bases appear at both top and bottom edges. The tile becomes seamless when repeated.

#### Strategy B: Pixel Art Approach
Abandon SVG polygon blades. Hand-draw a 16×16 (or 32×32) pixel-art grass pattern in a paint tool that tiles seamlessly. Export as PNG directly. Classic Pokemon grass was a simple repeating pixel pattern, not individual blades.

#### Strategy C: Shader-Based Grass
Use a custom shader that procedurally generates grass density based on UV coordinates. Could use noise functions for natural variation. More complex but eliminates the tiling problem entirely.

#### Strategy D: Increase SVG Render Resolution
Render SVGs at 2× or 4× resolution (64×64 or 128×128) then downscale. This gives anti-aliasing between blades, partially filling transparent gaps with semi-transparent green pixels. Update `density` parameter in sharp: `{ density: 144 }` or `{ density: 288 }`.

### Issue 2: Y-Sort Boundary for Multi-Cell Sprites
Large sprites (trees at 1.8× scale) extend above their tile cell. The Y-sort comparison uses the tile's grid Y, but visually the sprite covers 2-3 rows. This can cause incorrect depth ordering at tile boundaries.
- **Strategy:** Use the sprite's visual bottom Y (not tile grid Y) for depth sorting

### Issue 3: Grass 2x2 Grid Hack in TileRenderer
The current 2×2 grid rendering for grass (tileId 72) is a temporary hack that should be removed once the density issue is properly solved. It multiplies draw calls and doesn't fully solve the gap problem.

---

## 7. Architecture & New Features

### Current Rendering Pipeline
```
Game1.Draw()
  ├── GraphicsDevice.Clear(Black)
  ├── SpriteBatch.Begin(NonPremultiplied, PointClamp, transformMatrix)
  ├── TileRenderer.DrawBaseTiles()           ← terrain layer
  ├── TileRenderer.DrawOverlaysBehindPlayer() ← overlays at y <= playerY
  ├── PlayerRenderer.Draw()                   ← player sprite
  ├── TileRenderer.DrawOverlaysInFrontOfPlayer() ← overlays at y > playerY
  ├── SpriteBatch.End()
  └── Fade overlay (if transitioning)
```

### Two-Layer Tile System
```
CodeGenerator (build time)
  ├── BaseTiles:    Terrain only (Grass, Water, Path, Sand, etc.)
  └── OverlayTiles: Everything else (Tree, Flower, Rock, TallGrass, Sign, etc.)
      Auto-split: if tile.Category != Terrain → move to overlay, fill base with Grass
```

### SVG → PNG Pipeline
```
Assets/Sprites/*.svg
  → node tools/convert_sprites.js
  → src/PokemonGreen.Assets/Sprites/*.png (embedded resources)
  → AssetLoader.LoadSprite() at runtime
  → Texture2D cached in TextureStore
```

### Quick Wins for Next Session
1. **Try Strategy D first** — increase SVG render density to 144 or 288 DPI in `convert_sprites.js`. Minimal code change, might solve gaps via anti-aliasing.
2. **Try Strategy A** — make grass tile seamless by adding blade bases at y=0 as well as y=32. One SVG edit, no code changes.
3. **Remove 2x2 hack** from TileRenderer after density is solved — net reduction in draw calls.
4. **Clean up v2 files** — delete `tile_tall_grass_v2*.svg` files that are no longer used.

---

## Key Lessons Learned

1. **Black backgrounds = missing base layer, not alpha bugs.** When transparent overlay sprites show black, check if there's actually something behind them. Don't chase alpha/blending settings.

2. **Auto layer splitting prevents manual errors.** Having `CodeGenerator` automatically separate terrain from decorations means map authors don't need to manually manage two layers.

3. **Y-sorted rendering only works within tile bounds.** Sub-tile offsets that push sprites outside the tile's grid cell break the Y-sort comparison. Any density hacks must stay within the tile's pixel boundary.

4. **SVG polygon blades don't tile seamlessly.** Triangle polygons growing from one edge create a repeating pattern of dense→sparse→gap. Seamless tiling or pixel art approaches may work better for small tile sizes.

5. **Don't confuse tile size with sprite size.** `GameWorld.TileSize` is 16px (the grid cell size). SVGs are 32×32 and get scaled down. The gap problem is about the SVG's internal layout, not the rendering grid.

6. **Compositing transparent layers requires removing opaque backgrounds.** Sharp compositing of two SVGs fails when both have an opaque base rect — the top layer's rect covers the bottom layer completely.
