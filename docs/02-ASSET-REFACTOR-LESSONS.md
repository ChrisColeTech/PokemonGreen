# Lessons Learned & Handoff Document v2

**Date:** 2026-02-14
**Sprint:** Asset Architecture Refactor, Rendering Pipeline Debug

---

## 1. What We Accomplished

### Asset Architecture Refactor
- **PokemonGreen.Assets** — New class library project for all game assets
  - Sprites embedded as resources (no more Content folder file copying)
  - `AssetLoader.cs` handles loading from embedded resources via `GetManifestResourceStream`
  - MemoryStream copy required before `Texture2D.FromStream` (stream must be seekable)
- **Rendering moved to Core** — `TileRenderer.cs`, `PlayerRenderer.cs`, `TextureStore.cs` now live in `PokemonGreen.Core/Rendering/`
- **Deleted old Rendering folder** from main game project — Game1.cs is now a thin shell

### Resource Loading Pipeline
```
Embedded PNG → AssetLoader.GetSpriteStream() → MemoryStream → Texture2D.FromStream → PremultiplyAlpha → Cached Texture2D
```

### Verified PNG Transparency
- `tile_tree.png` is 64x64 pixels
- Corner pixels (0,0), (63,0), (0,31) have **A:0** (fully transparent)
- Tree body pixels (31,0), (32,0) have **A:244** (mostly opaque)
- **PNGs do have proper alpha channels** — the issue is NOT the source images

### Project Structure (Updated)
```
src/
├── PokemonGreen/              # MonoGame executable (thin shell)
│   ├── Game1.cs
│   └── Program.cs
├── PokemonGreen.Core/         # Game engine
│   ├── Rendering/
│   │   ├── TileRenderer.cs
│   │   ├── PlayerRenderer.cs
│   │   └── TextureStore.cs
│   ├── Maps/
│   ├── Player/
│   └── Systems/
├── PokemonGreen.Assets/       # Embedded resources
│   ├── Sprites/*.png          # Tile sprites
│   ├── Player/*.png           # Player sprite sheets
│   └── AssetLoader.cs
└── PokemonGreen.MapGen/       # Map generation CLI
```

---

## 2. What Work Remains

### Critical Bug: Black Background on Transparent Sprites
**Status:** UNRESOLVED
- PNGs have valid alpha (verified via ImageSharp test)
- Premultiplied alpha conversion added to `AssetLoader.PremultiplyAlpha()`
- `BlendState.AlphaBlend` used in SpriteBatch
- **Still seeing black backgrounds instead of transparency**

### Critical Bug: Tree Cutoff on Sides
**Status:** PARTIALLY ADDRESSED
- Expanded render bounds by 2 tiles in all directions
- Updated scaling to preserve aspect ratio
- Needs verification

### High Priority
- [ ] Fix transparency rendering issue
- [ ] Fix tree cutoff issue
- [ ] Test all sprites load correctly from embedded resources
- [ ] Verify player sprites render with transparency

### Medium Priority
- [ ] Remove CheckPng temporary project
- [ ] Add nullable annotation to Game1.cs
- [ ] Clean up old Content/sprites and Content/player folders

---

## 3. Optimization Suspects

### 3.1 Premultiplied Alpha Conversion Per Texture
Every texture loaded goes through a CPU-side pixel loop to convert straight alpha → premultiplied. This is correct but could be slow with many sprites.
- **Status:** Already implemented, working
- **Future:** Consider MonoGame Content Pipeline for build-time conversion

### 3.2 Dictionary Lookups Per Tile Per Frame
`DrawTile` calls multiple TryGetValue per tile. At 32x32 visible tiles, that's thousands of dictionary lookups per frame.
- **Fix:** Pre-bake `Texture2D[] _texturesByTileId` array at initialization

### 3.3 No Texture Atlasing
Each tile is a separate texture. GPU texture switching is expensive.
- **Fix:** Pack all tile sprites into a single atlas texture with UV coordinates

### 3.4 Embedded Resource Loading Overhead
Loading from embedded resources requires MemoryStream copy. Not a runtime issue (cached) but increases memory during load.
- **Status:** Acceptable for now, textures are cached after first load

---

## 4. Step-by-Step: Get the App Fully Working

### Prerequisites
- .NET 9 SDK
- MonoGame 3.8.2+ (DesktopGL)

### Build
```bash
cd D:\Projects\PokemonGreen
dotnet build
```

### Run
```bash
dotnet run --project src\PokemonGreen
```

### Verify Sprites Are Embedded
```bash
# PowerShell - list embedded resource names
[System.Reflection.Assembly]::LoadFrom('src\PokemonGreen.Assets\bin\Debug\net9.0\PokemonGreen.Assets.dll').GetManifestResourceNames()
```

### Check PNG Alpha Values (Debug Tool)
```bash
cd src\CheckPng
dotnet run
```

---

## 5. How to Start/Test

### Quick Smoke Test
```bash
dotnet build && dotnet run --project src\PokemonGreen
```
- Window opens at 800x600
- Should see test_map_center with grass, water, trees
- WASD to move, Shift to run, Space to jump
- **KNOWN ISSUE:** Trees show black backgrounds, may be cut off

### Verify Asset Loading
Add debug output to `TextureStore.Initialize`:
```csharp
var names = PokemonGreen.Assets.AssetLoader.GetEmbeddedResourceNames();
foreach (var name in names) Console.WriteLine(name);
```

---

## 6. Known Issues & Debug Strategies

### Issue 1: Black Background on Transparent Pixels (CRITICAL)

**What we've tried:**
1. Added `PremultiplyAlpha()` conversion in AssetLoader
2. Changed to `BlendState.AlphaBlend`
3. Verified PNGs have alpha channel (A:0 on transparent pixels)

**Strategies to try:**

#### Strategy A: Check GraphicsDevice Blend State
The GraphicsDevice might have a different blend state set elsewhere:
```csharp
// In Draw, before SpriteBatch.Begin
GraphicsDevice.BlendState = BlendState.AlphaBlend;
```

#### Strategy B: Use NonPremultiplied WITHOUT PremultiplyAlpha
Reverse approach — load straight alpha, use `BlendState.NonPremultiplied`:
```csharp
// In AssetLoader - remove PremultiplyAlpha call
var texture = Texture2D.FromStream(_graphicsDevice, memStream);
// DON'T call PremultiplyAlpha(texture);

// In Game1.cs
_spriteBatch.Begin(..., BlendState.NonPremultiplied, ...);
```

#### Strategy C: Check SamplerState
`SamplerState.PointClamp` is correct, but verify no other sampler state is interfering:
```csharp
GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
```

#### Strategy D: Debug Output Pixel Values
After loading, sample actual texture pixels to verify alpha was preserved:
```csharp
var data = new Color[texture.Width * texture.Height];
texture.GetData(data);
Console.WriteLine($"First pixel: {data[0]}"); // Should show A:0
```

---

### Issue 2: Tree Cutoff on Sides

**Root cause candidates:**

#### Candidate A: Camera Bounds Calculation
The render bounds expansion may not be enough for 1.8x scaled 64x64 textures:
```csharp
// Current: -2 to +3
// Try: -3 to +4 for larger sprites
int startX = Math.Max(0, bounds.Left / tileSize - 3);
```

#### Candidate B: Negative Offset Drawing
Scaled trees draw with negative Y offset (above tile), but X offset might be wrong:
```csharp
int offsetX = (tileSize - drawWidth) / 2;  // Could be negative for wide sprites
```
If `drawWidth > tileSize`, offset goes negative and left side clips.

#### Candidate C: Viewport/Scissor Clipping
MonoGame may be clipping sprites that extend outside the logical map area. Check if `RasterizerState.ScissorTestEnable` is on.

---

## 7. Architecture Summary (Updated)

```
┌─────────────────────────────────────────────────────────────┐
│                     PokemonGreen (exe)                      │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Game1.cs                                             │   │
│  │ - Initialize() → TextureStore.Initialize()          │   │
│  │ - LoadContent()                                      │   │
│  │ - Update() → GameWorld.Update() + TileRenderer.Update│   │
│  │ - Draw() → SpriteBatch with transform matrix         │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   PokemonGreen.Core                         │
│  ┌─────────────────────┐  ┌─────────────────────────────┐  │
│  │ Rendering/          │  │ Maps/                       │  │
│  │ - TileRenderer      │  │ - TileRegistry (104 tiles)  │  │
│  │ - PlayerRenderer    │  │ - TileMap, MapDefinition    │  │
│  │ - TextureStore      │  │ - MapCatalog, MapRegistry   │  │
│  └─────────────────────┘  └─────────────────────────────┘  │
│  ┌─────────────────────┐  ┌─────────────────────────────┐  │
│  │ Player/             │  │ Systems/                    │  │
│  │ - Player            │  │ - Camera                    │  │
│  │ - Direction, State  │  │ - InputManager              │  │
│  └─────────────────────┘  └─────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   PokemonGreen.Assets                       │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ AssetLoader.cs                                       │   │
│  │ - Initialize(GraphicsDevice)                        │   │
│  │ - LoadSprite(name) → Texture2D?                     │   │
│  │ - LoadPlayerSprite(name) → Texture2D?               │   │
│  │ - PremultiplyAlpha(texture)                         │   │
│  └─────────────────────────────────────────────────────┘   │
│  Embedded Resources:                                        │
│  - PokemonGreen.Assets.Sprites.tile_*.png                  │
│  - PokemonGreen.Assets.Player.*.png                        │
└─────────────────────────────────────────────────────────────┘
```

---

## 8. Quick Wins & Next Steps

### Immediate Actions (< 30 min each)
1. **Try BlendState.NonPremultiplied** — Remove PremultiplyAlpha call, use NonPremultiplied blend state
2. **Debug output first texture pixel** — Verify alpha values after load
3. **Increase render bounds** — Try -4 to +5 for scaled sprites

### Quick Wins (< 2 hours each)
4. **Add sprite size logging** — Log dimensions of loaded textures to verify 64x64
5. **Test with a known-good PNG** — Create a simple 32x32 test sprite with obvious transparency
6. **Remove CheckPng project** — Cleanup after debugging

### Next Session Priority
1. Fix transparency (try Strategy B: NonPremultiplied without conversion)
2. Fix tree cutoff (check offsetX calculation)
3. Verify all sprites render correctly
4. Delete old Content/sprites and Content/player folders
5. Commit working state

---

## Key Lessons

1. **MonoGame alpha handling is subtle.** The combination of `Texture2D.FromStream` + `BlendState.AlphaBlend` + premultiplied conversion should work but may have edge cases. Try the opposite approach (no conversion + NonPremultiplied).

2. **Embedded resources require MemoryStream.** `Texture2D.FromStream` needs a seekable stream. Always copy `GetManifestResourceStream` to a `MemoryStream` before passing to MonoGame.

3. **Large sprites extend beyond tile bounds.** A 64x64 sprite scaled to 1.8x on a 32px tile will draw from -25 Y offset. Render bounds must account for this or sprites clip.

4. **Verify source data early.** Before debugging rendering code, verify the source PNGs actually have alpha. We confirmed A:0 on transparent pixels, so the issue is in our pipeline not the assets.

5. **Project separation is clean.** Having `PokemonGreen.Assets` as a separate library means the game executable has no file dependencies — everything is embedded. Good for distribution.
