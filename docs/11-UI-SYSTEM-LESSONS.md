# 11 - UI System & Battle Menu Lessons Learned

## What Was Accomplished

### Reusable UI System (`Core/UI/`)
- **MessageBox** — Typewriter text with message queue, confirm-to-skip/advance, `OnFinished` callback for sequencing game flow. Properly resets all state (including `OnFinished` and `_blinkTimer`) on `Clear()`.
- **MenuBox** — Grid/list menu with arrow key + mouse navigation. `MenuItem` class with label, enabled flag, and `Action? OnConfirm` callback. Supports 2-column grid (battle) or single-column list layouts.
- **UIStyle** — Centralized battle panel rendering matching the old engine's `Window.Decoration.Battle` style: 3px gradient border (black -> dark gray -> gray) on all four sides, 2px white inner edge, semi-transparent dark gray fill `RGBA(80,80,80,200)`. All text colors from old engine's `FontColors.cs` with exact RGB values.
- **Arrow cursors** — Pixel-drawn right-pointing triangle for menu selection, down-pointing triangle for message advance prompt. No font character hacks.

### KermFont Loader (`Core/UI/Fonts/`)
- **KermFont.cs** — Full binary parser for the old engine's custom `.kermfont` format. Verified against all 5 font files (Default, Battle, DefaultSmall, PartyNumbers, Braille) with zero bytes remaining.
- **KermFontRenderer.cs** — SpriteBatch-based text renderer with `DrawString`, `DrawStringCentered`, typewriter support (`maxVisibleChars`), `PointClamp` sampling.
- **KermFontPalettes.cs** — All 12 color palettes from old engine with exact RGB values.
- **Pre-baked RGBA atlas** — Indexed color atlas converted to RGBA at load time. Can re-bake with different palettes at runtime without re-parsing.
- Format: `byte` FontHeight, `byte` BitsPerPixel, `int32` NumGlyphs, then per-glyph: `ushort` CharCode, `byte` CharWidth, `byte` CharSpace, packed bitmap data (MSB-first).

### Old Engine Architecture Doc (`docs/10-OLD-ENGINE-ARCHITECTURE.md`)
- Comprehensive reference covering all 12 subsystems: rendering (OpenGL 4.2 via Silk.NET), fonts (kermfont + custom shader), UI (Window decorations, TextGUIChoices), battle (3D with shadow mapping), world (8x8 tiles, day/night tints), scripts (binary VM with 60+ commands), audio (custom 48kHz F32 mixer), input (13 key enum).

### Bug Fixes
- **Encounter dead zones** — `IsEncounterTile()` only checked the player's head tile (`TileY`), but the renderer draws grass at the feet tile (`TileY + 1`). Fixed to check both positions, matching the renderer's detection.
- **MessageBox state leak** — `Clear()` didn't reset `OnFinished` or `_blinkTimer`, causing stale callbacks to persist across battles and potentially re-trigger camera zoom.
- **Mouse click reliability** — Restructured `MenuBox.Update()` to guard mouse checks with `LastBounds.Contains()` and separate click/hover paths.

### Battle Flow (from previous session, refined here)
Three-phase system wired through MessageBox/MenuBox:
1. **Intro** — Camera zoomed on foe, "Wild POKEMON appeared!" typewriter text
2. **ZoomOut** — Camera lerps to default position (ease-out), triggered by message dismiss
3. **Menu** — Fight/Bag/Pokemon/Run grid, "What will you do?" message

## Key Technical Lessons

### Tile Coordinate Mismatch
The renderer and game logic used different tile positions for the same concept. The renderer draws grass at `playerTileY + 1` (feet), but encounter checks used `playerTileY` (position). Any time a visual effect is tied to a tile, the game logic check must use the same tile coordinate.

### UI State Must Fully Reset
`MessageBox.Clear()` originally only cleared the message queue and text state. The `OnFinished` callback and `_blinkTimer` persisted, creating a subtle bug where the previous battle's zoom callback could fire in the next battle. Rule: **every method that resets state must reset ALL state**, including callbacks and timers.

### Old Engine Is Fully Procedural
No UI image assets anywhere — all windows, HP bars, menus, and text are drawn as colored rectangles with custom shaders. The `.kermfont` files use indexed-color glyph atlases rendered through a palette shader. MonoGame equivalent: pre-bake the atlas to RGBA and render with standard SpriteBatch.

### Mouse Hit Testing Timing
`MenuBox.GetItemRect()` depends on `LastBounds` which is set during `Draw()`. Since `Update()` runs before `Draw()`, the first frame a menu appears has stale/empty bounds. Guard all mouse checks with `LastBounds.Contains()` to avoid phantom clicks.

## Architecture

```
Core/UI/
├── UIStyle.cs              — Battle panel rendering, text colors, arrow drawing
├── MessageBox.cs           — Typewriter text + message queue
├── MenuBox.cs              — Grid/list menu with navigation
└── Fonts/
    ├── KermFont.cs         — .kermfont binary parser + atlas builder
    ├── KermGlyph.cs        — Per-glyph metadata (width, spacing, UVs)
    ├── KermFontRenderer.cs — SpriteBatch text renderer
    ├── KermFontPalettes.cs — Color palettes from old engine
    ├── KermFontExample.cs  — Usage demo
    └── KermFontSmokeTest.cs — Headless format validator
```

## Next Steps

### Immediate — Battle System Completion
1. **Fight sub-menu** — Move list when Fight is selected (PP display, type indicators)
2. **Bag sub-menu** — Item list with categories
3. **Pokemon sub-menu** — Party list with HP bars, switch/summary options
4. **Battle logic** — Turn order, damage calc, move effects, HP reduction, faint handling
5. **Wire KermFont into battle UI** — Replace Arial SpriteFont with loaded Battle.kermfont

### Immediate — Overworld Pause Menu
1. **Start menu** — Pokemon, Bag, Save, Options (vertical list layout using MenuBox with `Columns = 1`)
2. **Trigger** — ESC or Start button opens/closes
3. **Style** — Use old engine's `GrayRounded` decoration (gray border, white fill) instead of Battle decoration

### Quick Wins
1. **Day/night cycle** — Old engine has 24 hourly RGB tint values (documented in `10-OLD-ENGINE-ARCHITECTURE.md`). Apply as a color multiply over the overworld SpriteBatch. Interpolate between hours. Timer: 30 min real time = 24 game hours. ~5 lines of tint logic + a color table.
2. **KermFont integration** — Load `Battle.kermfont` in Game1, pass to MessageBox/MenuBox instead of SpriteFont. The renderer already accepts font as a parameter.
3. **GrayRounded panel style** — Add `UIStyle.DrawStandardPanel()` for overworld menus (gray border with corner radius, white fill). Reuse the same MenuBox class with a different draw style.
4. **Battle music transition** — Old engine backs up current song, plays battle music, restores on exit. AudioManager already exists in concept.

### Medium-Term
- Pokemon sprites as textured billboards in 3D battle scene
- HP bar rendering (old engine draws these procedurally with colored rectangles)
- Experience bar and level-up flow
- Multiple battle scene types (different .dae backgrounds per route/area)
- Wild Pokemon data (species, level ranges, encounter rates per route)

## How to Test

```bash
cd D:\Projects\PokemonGreen
dotnet build
dotnet run --project src/PokemonGreen

# Battle test: set DebugStartInBattle = true in Game1.cs
# Overworld test: set DebugStartInBattle = false, walk into grass
```

Verify:
- Battle opens with typewriter "Wild POKEMON appeared!"
- Confirm skips/advances text, camera zooms out
- Menu appears with Fight/Bag/Pokemon/Run in 2x2 grid
- Arrow keys navigate, yellow highlight on selected
- Mouse click on Run exits battle
- Walk through ALL grass patches — encounters should trigger everywhere grass animation plays
