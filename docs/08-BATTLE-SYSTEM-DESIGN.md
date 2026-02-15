# Battle System Design & Handoff

**Date:** 2026-02-15
**Sprint:** Battle System Research & Planning

---

## 1. What We Accomplished

### Research & Reference Analysis (Complete)
- Investigated the old Pokemon game engine at `D:\Projects\PokemonGameEngine\PokemonGameEngine`
- Mapped out the full battle flow: encounter detection → transition → battle screen → return to overworld
- Identified the PokemonBattleEngine (PBE) library as the core battle logic engine used by the old project
- Documented how the old engine handles encounter chance, ability modifiers, wild Pokemon generation, battle UI, action menus, event packets, and post-battle cleanup
- Reviewed our current project's architecture to identify integration points

### What Already Exists in PokemonGreen
- **Encounter tiles (IDs 72-79)** are already defined in `TileRegistry.cs` with `TileCategory.Encounter` — TallGrass, RareGrass, DarkGrass, CaveEncounter, WaterEncounter, SurfEncounter, FishingSpot, Headbutt
- **`PlayerState.Combat`** exists in the enum with a 6-frame one-shot animation, but nothing triggers it yet
- **Fade transition system** already works for map transitions (fade to black, load new content, fade in) — can be reused for battle entry/exit
- **`OverlayBehavior` field** on tile definitions holds strings like `"wild_encounter"`, `"rare_encounter"` — these tags can drive encounter logic
- **Input system** has rising-edge detection for discrete key presses, which works well for turn-based menu navigation

---

## 2. What Work Remains

### The battle system does not exist yet. Everything below needs to be built.

### Phase 1: Encounter Detection
When the player finishes a step onto a grass tile, roll a random chance. If it hits, start a battle. This hooks into `GameWorld.Update()` the same way step warps already work.

### Phase 2: Game State System
The game currently has no concept of scenes or states. Everything runs through `GameWorld` and `Game1` with a single rendering pipeline. We need a way to say "we're in a battle now" so the update loop runs battle logic instead of overworld logic, and the draw method renders the battle screen instead of the map.

### Phase 3: Pokemon Data
There are no Pokemon, moves, types, or stats anywhere in the codebase. We need data structures for what a Pokemon is, what moves it knows, what stats it has, and tables defining which Pokemon appear in which grass.

### Phase 4: Battle Screen
The actual battle UI — two Pokemon facing each other, HP bars, a menu with Fight/Pokemon/Bag/Run, move selection, damage text, and turn-by-turn flow.

### Phase 5: Battle Logic
Damage calculation, type effectiveness, speed ordering, fainting, fleeing, and returning to the overworld when the battle ends.

---

## 3. Optimizations — Where to Begin

### 3a. Use PBE for Battle Logic
The old engine uses the PokemonBattleEngine (PBE) NuGet library to handle all battle math — damage formulas, type charts, ability effects, status conditions, stat stages, accuracy checks, critical hits, and multi-hit moves. Building all of this from scratch would take a very long time. PBE handles it all and communicates through a packet/event system. The tradeoff is that PBE is a heavy dependency with its own data format requirements, but it gives us a complete, tested battle engine immediately.

### 3b. Start With the Simplest Possible Battle
Don't try to build the full battle system in one go. Start with: one wild Pokemon vs one player Pokemon, four moves, HP bars, and a Fight/Run menu. No items, no switching, no abilities, no status effects. Get the loop working end to end first, then add complexity.

### 3c. Reuse the Fade Transition
The existing `TransitionState` and `FadeAlpha` system in `GameWorld` already handles smooth fade-to-black transitions. Rather than building a new transition system, extend the existing one with a new state like `FadingToBattle` / `FadingFromBattle`. The battle screen loads during the black frame, same as map transitions.

### 3d. Separate Battle Rendering From Overworld Rendering
The battle screen should be its own rendering path in `Game1.Draw()`. When the game is in battle state, skip the tile/player rendering entirely and draw the battle screen instead. This keeps the two systems independent and avoids fighting with the camera/viewport system.

---

## 4. Step-by-Step Approach

### Step 1: Add a Game State enum

Add a `GameState` enum to `GameWorld` (or a new top-level class) with values `Overworld` and `Battle`. Branch the update and draw logic based on this state. When in `Overworld`, everything works exactly as it does now. When in `Battle`, run battle update logic and render the battle screen.

### Step 2: Create encounter detection

In `GameWorld.Update()`, after `Player.Update()` (after the player finishes a step), check the tile the player is standing on. If it's a `TileCategory.Encounter` tile, roll a random chance. Start simple — 1 in 10 chance per step onto grass. If the roll hits, set the game state to start a battle transition.

Where to put it — right after the existing step warp check (step 7 in the current update loop):

```
// After step warp check...
// Check for wild encounter
if (CurrentMapDefinition != null && playerSteppedOnNewTile)
{
    int baseTile = CurrentMapDefinition.GetBaseTile(Player.TileX, Player.TileY);
    var tileDef = TileRegistry.GetTile(baseTile);
    if (tileDef?.Category == TileCategory.Encounter)
    {
        if (random roll succeeds)
            BeginBattleTransition();
    }
}
```

### Step 3: Define Pokemon and Move data structures

Create simple classes:
- `Pokemon` — species name, level, current HP, max HP, attack, defense, speed, type, list of moves
- `Move` — name, power, accuracy, type, PP
- `EncounterTable` — maps a grass area to a list of possible Pokemon with level ranges

Start with hardcoded data for 5-10 Pokemon and 10-15 moves. No need for a database or JSON files yet.

### Step 4: Build the battle screen

The battle screen needs:
- **Background** — a simple colored or tiled background
- **Two Pokemon sprites** — player's Pokemon (bottom-left) and wild Pokemon (top-right)
- **Two HP bars** — showing name, level, and HP as a colored bar
- **Text box** — bottom of screen, shows "A wild Rattata appeared!" or "Bulbasaur used Tackle!"
- **Action menu** — Fight / Run (start with just these two)
- **Move menu** — shows 4 moves when Fight is selected

This is a separate rendering pass in `Game1.Draw()`. It does not use the camera or tile system at all. It draws directly to screen coordinates.

### Step 5: Implement turn flow

The simplest battle loop:
1. Player picks a move (or Run)
2. Determine who goes first (compare speed)
3. First Pokemon attacks — calculate damage, subtract HP, show message
4. If defender faints, battle ends
5. Second Pokemon attacks — same thing
6. If defender faints, battle ends
7. Back to step 1

Damage formula (simplified): `damage = (attackerAttack * movePower) / defenderDefense`
Add some randomness (85-100% multiplier) and type effectiveness (2x, 0.5x, 0x).

### Step 6: Return to overworld

When the battle ends (enemy fainted or player ran), fade to black, set game state back to `Overworld`, fade in. The player resumes on the same grass tile they were standing on.

### Step 7: Polish and expand

Once the basic loop works end to end:
- Add Pokemon switching
- Add items (Potions, Pokeballs)
- Add status effects (poison, paralysis)
- Add abilities
- Add experience and leveling
- Consider integrating PBE for the full battle engine

---

## 5. How to Start/Test

### Run the Game
```bash
cd D:\Projects\PokemonGreen
dotnet run --project src/PokemonGreen/PokemonGreen.csproj
```
- Player spawns at (8, 8) on `test_map_center`
- Currently no encounter tiles are placed on the test maps — they use grass tile ID 1 (Terrain), not ID 72 (TallGrass/Encounter)
- To test encounters, either place encounter tiles (ID 72) on a test map, or temporarily make the encounter check trigger on regular grass (ID 1)

### Verify Builds
```bash
# C# build
dotnet build D:\Projects\PokemonGreen\src\PokemonGreen\PokemonGreen.csproj

# TypeScript type check (map editor)
cd D:\Projects\PokemonGreen\src\PokemonGreen.MapEditor && npx tsc --noEmit
```

### Test the Battle Screen (once built)
1. Walk into grass
2. After a few steps, screen should fade to black
3. Battle screen appears with wild Pokemon
4. Select Fight → pick a move → damage applied → enemy turn
5. Defeat the enemy or select Run
6. Screen fades back to overworld, player is on the same tile

---

## 6. Known Issues + Strategies

### Issue 1: No game state system
**Current state:** `GameWorld` and `Game1` assume the game is always on the overworld. There's no way to switch to a different screen.
**Strategy A — GameState enum in GameWorld:** Add `GameState { Overworld, BattleTransition, Battle }` to `GameWorld`. Branch `Update()` and add a `IsBattleActive` flag that `Game1.Draw()` checks to decide what to render. Simplest approach, keeps everything in one place.
**Strategy B — Scene manager:** Create a `SceneManager` class that holds the active scene (`OverworldScene`, `BattleScene`). Each scene has its own `Update()` and `Draw()`. Cleaner separation but more files and plumbing.
**Strategy C — State stack:** Push/pop states so the battle "sits on top of" the overworld. When battle pops, the overworld resumes exactly where it was. Useful later for menus and pause screens too.

### Issue 2: No Pokemon data exists
**Current state:** Zero Pokemon, move, or type data in the codebase.
**Strategy A — Hardcode a starter set:** Define 5-10 Pokemon and 10-15 moves directly in C# classes. No files, no parsing, just get the battle working.
**Strategy B — Use PBE library data:** PBE comes with complete Pokemon/move databases. Add the NuGet package, use its data provider, and skip building our own data layer entirely.
**Strategy C — JSON encounter tables:** Define Pokemon/moves in JSON files that load at startup. More flexible than hardcoding but adds a loading step.

### Issue 3: Test maps don't have encounter grass
**Current state:** All test maps use grass tile ID 1 (`TileCategory.Terrain`), not ID 72 (`TileCategory.Encounter`). Walking on grass won't trigger encounters.
**Strategy A — Place encounter tiles in the map editor:** Open each test map, replace some grass tiles with TallGrass (ID 72), re-export. Most realistic.
**Strategy B — Treat regular grass as encounter-capable temporarily:** In the encounter check, also trigger on tile ID 1. Remove this hack once real encounter tiles are placed.
**Strategy C — Create a dedicated test map:** Build a small map specifically for battle testing with encounter grass covering most of the area.

### Issue 4: Battle screen rendering
**Current state:** The game only renders tiles and a player sprite. There's no UI framework, no text rendering, no menu system.
**Strategy A — MonoGame SpriteFont:** Use MonoGame's built-in `SpriteFont` for text and draw rectangles/sprites for HP bars and menus. Low-level but no extra dependencies.
**Strategy B — Minimal UI framework:** Build a simple `Panel`, `Label`, `ProgressBar` class set that handles layout and input. Reusable for inventory, pause menu, etc.
**Strategy C — Draw everything as sprites:** Pre-render the battle UI elements as sprite sheets (HP bar states, menu backgrounds, text boxes). Pixel-art style, looks authentic, but less flexible.

---

## 7. Architecture

### How the Old Engine Does It (Reference)

The old engine at `D:\Projects\PokemonGameEngine\PokemonGameEngine` splits battle responsibilities:

```
OverworldGUI
  → EncounterMaker.CheckForWildBattle()    (chance roll, ability modifiers)
  → BattleMaker.CreateWildBattle()          (creates PBEBattle instance)
  → BattleTransition_Liquid                 (screen wipe animation)
  → BattleGUI                              (battle screen controller)
      ├── BattleGUI_Actions.cs              (player menu: Fight/Pokemon/Bag/Run)
      ├── BattleGUI_Events.cs               (packet processing from PBE)
      ├── BattleGUI_Tasks.cs                (animations, messages, camera)
      ├── BattleGUI_Render.cs               (3D terrain, sprites, info bars)
      ├── BattleGUI_FaintReplacement.cs     (switch after fainting)
      ├── ActionsBuilder.cs                 (queues actions for multi-Pokemon)
      └── PBEBattle (external library)      (all battle math and state)
```

PBE runs on a separate thread and communicates through packets. The main thread processes packets one at a time, playing animations and showing messages for each event. Player input goes through `ActionsBuilder` which queues moves/switches/items and submits them all at once.

### Proposed Architecture for PokemonGreen

```
GameWorld.Update()
  → Check encounter tile after player step
  → Roll random chance
  → If hit: BeginBattleTransition()
      → Fade to black (reuse existing fade system)
      → Create BattleState with player Pokemon + wild Pokemon
      → Set GameState = Battle

GameWorld.UpdateBattle()
  → Process current battle phase:
      Phase.Intro        → "A wild Rattata appeared!" text
      Phase.PlayerChoice → Show Fight/Run menu, wait for input
      Phase.MoveSelect   → Show 4 moves, wait for input
      Phase.ExecuteTurn   → Apply damage in speed order, show messages
      Phase.CheckFaint   → Did anyone faint? If so, end battle
      Phase.BattleEnd    → Show victory/defeat message
      Phase.FadeOut      → Fade to black, restore overworld

Game1.Draw()
  → if (gameWorld.GameState == Overworld)
      → Draw tiles, player, overlays (current code)
  → else if (gameWorld.GameState == Battle)
      → Draw battle background
      → Draw two Pokemon sprites
      → Draw HP bars
      → Draw text box with current message
      → Draw action/move menu if in choice phase
  → Draw fade overlay (works for both)
```

### Key Files That Will Be Created

| File | Purpose |
|------|---------|
| `Core/Battle/BattleState.cs` | Battle state machine — phases, current Pokemon, turn order |
| `Core/Battle/BattlePokemon.cs` | Pokemon in battle — HP, moves, stats, species |
| `Core/Battle/Move.cs` | Move definition — name, power, accuracy, type, PP |
| `Core/Battle/TypeChart.cs` | Type effectiveness table (fire beats grass, etc.) |
| `Core/Battle/DamageCalc.cs` | Damage formula |
| `Core/Battle/EncounterTable.cs` | Which Pokemon appear in which area, at what levels |
| `Core/Battle/EncounterChecker.cs` | Random chance roll, step counting |
| `Rendering/BattleRenderer.cs` | Draws the battle screen (sprites, HP bars, menus, text) |

### Key Files That Will Be Modified

| File | Changes |
|------|---------|
| `Core/GameWorld.cs` | Add `GameState` enum, encounter check in `Update()`, `UpdateBattle()` method |
| `Game1.cs` | Branch `Draw()` based on game state — overworld vs battle rendering |
| `Core/Systems/InputManager.cs` | May need menu navigation inputs (up/down/confirm/cancel) |

### Quick Wins

1. **Encounter detection** — Add the tile check after `Player.Update()` in `GameWorld.Update()`. ~20 lines. Uses existing `TileCategory.Encounter` and `TileRegistry.GetTile()`. Doesn't need the battle screen to exist — just log "encounter triggered" to console to verify it works.
2. **Game state branching** — Add a `GameState` enum and an `if` check in `Update()` and `Draw()`. ~10 lines each. Everything else stays the same when in Overworld state.
3. **Reuse fade system** — The existing `TransitionState`/`FadeAlpha` system already fades to black and back. Extend it with a battle callback instead of a map-load callback. ~15 lines.
4. **Battle screen layout** — The battle screen has a fixed layout: background fills the screen, wild Pokemon sprite top-right, player's Pokemon sprite bottom-left, HP bars next to each Pokemon showing name/level/health, text box across the bottom showing battle messages, and an action menu (Fight/Run) that appears over the text box when it's the player's turn.

### New Feature Ideas

- **Encounter animation** — Flash the screen or play a swirl effect before fading to battle (like the old games). The old engine uses a liquid wipe shader.
- **Shaking grass** — Animate grass tiles with a rustling effect before the encounter triggers, giving the player a visual warning.
- **Repel items** — Suppress encounters for N steps. Simple counter that decrements on each grass step.
- **Area-specific encounter tables** — Different maps or map zones have different Pokemon. Tag maps with encounter table IDs.
- **Trainer battles** — NPCs that trigger a battle when the player walks into their line of sight. The `TileCategory.Trainer` tiles already exist.

---

## 8. Reference Files in Old Engine

For anyone picking this up, these are the key files to study in `D:\Projects\PokemonGameEngine\PokemonGameEngine`:

| File | What To Learn |
|------|---------------|
| `World/EncounterMaker.cs` | How encounter chance is calculated, ability modifiers, Pokemon generation |
| `Core/BattleMaker.cs` | How a PBEBattle instance is created from party data |
| `Render/Battle/BattleGUI.cs` | How the battle screen initializes and manages lifecycle |
| `Render/Battle/BattleGUI_Actions.cs` | How the Fight/Pokemon/Bag/Run menu works |
| `Render/Battle/BattleGUI_Events.cs` | How battle events (damage, fainting, status) are processed |
| `Pkmn/PartyPokemon.cs` | Complete Pokemon data structure with all fields |
| `Pkmn/Moveset.cs` | How moves are stored (4 slots, PP tracking) |
| `Render/Battle/PkmnPosition.cs` | How Pokemon are positioned on screen for single/double/triple formats |
| `Render/Transitions/BattleTransition_Liquid.cs` | How the screen transition effect works |

---

## 9. PBE Library Decision

### Option A: Use PBE (PokemonBattleEngine)
**Pros:**
- Complete battle logic out of the box — damage, types, abilities, status, stat stages, weather, multi-hit, critical hits
- Tested and proven (the old engine ships with it)
- Handles edge cases we'd never think of
- Packet-based event system makes UI sync straightforward

**Cons:**
- Heavy dependency with its own data format requirements
- Need to learn its API and data provider interface
- May be over-engineered for a simple early prototype
- Threading model adds complexity

### Option B: Build Our Own (Simplified)
**Pros:**
- Full control, no external dependency
- Can start extremely simple (just HP and damage) and add features one at a time
- Easier to debug and understand
- No threading needed

**Cons:**
- Type chart, ability effects, status conditions, stat stages — each is a significant chunk of work
- Easy to get formulas wrong
- Will eventually want what PBE already provides

### Recommendation
Start with Option B for the prototype — get the battle screen working with simple damage math. Once the screen, menus, and flow are solid, evaluate whether to plug in PBE for the battle engine or keep building our own. The rendering and UI work is the same either way.

---

## 10. Proof of Concept — How to Build the Battle Screen

This section explains exactly how to construct a working battle screen proof of concept using MonoGame, step by step. Each step builds on the previous one.

### What We Have to Work With

MonoGame gives us `SpriteBatch` which can draw two things:
1. **Textures** — any image (PNG, sprite sheet) drawn at a position, with optional scaling, rotation, and color tinting
2. **Rectangles** — by stretching a 1x1 white pixel texture to any size and tinting it any color

`Game1.cs` already creates a 1x1 white pixel texture (`_pixelTexture`) on line 69-70. This is the tool for drawing HP bars, backgrounds, text boxes, and menu panels — stretch it to the size you need and tint it the color you want.

MonoGame also has `SpriteFont` for text rendering. A `.spritefont` file defines which font to use (name, size, style). The MonoGame Content Pipeline compiles it into a bitmap font. Then `SpriteBatch.DrawString(font, "text", position, color)` draws text on screen. No `.spritefont` file exists in the project yet — one must be created.

### POC Step 1: Create a SpriteFont for Text

The battle screen needs text for Pokemon names, levels, HP numbers, move names, and battle messages. MonoGame requires a `.spritefont` XML file processed through the Content Pipeline.

1. Create `Content/Content.mgcb` if it doesn't exist (MonoGame content pipeline project)
2. Create `Content/Fonts/BattleFont.spritefont` — an XML file like this:
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <XnaContent xmlns:Graphics="Microsoft.Xna.Framework.Content.Pipeline.Graphics">
     <Asset Type="Graphics:FontDescription">
       <FontName>Arial</FontName>
       <Size>12</Size>
       <Spacing>0</Spacing>
       <UseKerning>true</UseKerning>
       <Style>Bold</Style>
       <CharacterRegions>
         <CharacterRegion>
           <Start>&#32;</Start>
           <End>&#126;</End>
         </CharacterRegion>
       </CharacterRegions>
     </Asset>
   </XnaContent>
   ```
3. Add the font to the content pipeline so it gets built
4. Load it in `Game1.LoadContent()`: `_battleFont = Content.Load<SpriteFont>("Fonts/BattleFont")`
5. Now `_spriteBatch.DrawString(_battleFont, "Hello", new Vector2(10, 10), Color.White)` draws text

**Alternative if the Content Pipeline is not set up:** Use a bitmap font library like MonoGame.Extended or load a pre-rendered font texture manually. Or generate a `Texture2D` font atlas at runtime from system fonts using `System.Drawing`.

### POC Step 2: Create the Game State Switch

Before building any battle UI, the game needs to know when it's in a battle vs on the overworld.

1. Add to `GameWorld.cs`:
   ```csharp
   public enum GameState { Overworld, Battle }
   public GameState State { get; private set; } = GameState.Overworld;
   ```

2. In `GameWorld.Update()`, wrap the entire existing overworld logic in:
   ```csharp
   if (State == GameState.Overworld)
   {
       // ... existing update code ...
   }
   else if (State == GameState.Battle)
   {
       UpdateBattle(deltaTime);
   }
   ```

3. In `Game1.Draw()`, branch the rendering:
   ```csharp
   if (_gameWorld.State == GameWorld.GameState.Overworld)
   {
       // ... existing tile/player drawing code ...
   }
   else if (_gameWorld.State == GameWorld.GameState.Battle)
   {
       DrawBattle();
   }
   ```

4. Add a temporary debug key (e.g., B key) that toggles `State` between Overworld and Battle so you can test the switch without needing encounter detection yet

### POC Step 3: Draw the Battle Background

The battle screen draws without the camera transform — it uses raw screen coordinates.

1. In `Game1.DrawBattle()`, start a new SpriteBatch without the camera matrix:
   ```csharp
   private void DrawBattle()
   {
       _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);

       int screenW = Window.ClientBounds.Width;
       int screenH = Window.ClientBounds.Height;

       // Background — fill the screen with a color (light green for grass battle)
       _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, screenW, screenH), new Color(144, 200, 96));

       _spriteBatch.End();
   }
   ```

2. Press B to switch to battle state — the screen should fill with solid green. Press B again to go back to the overworld. This proves the state switch and battle rendering path work.

3. Later, replace the solid color with a sprite image (a grass field background PNG).

### POC Step 4: Draw the Battle Layout Zones

The screen is divided into fixed zones. Everything is positioned as a percentage of screen size so it scales with the window. Define these layout constants:

```
┌─────────────────────────────────────────────────────┐
│                                                       │
│   [Enemy Info Bar]              [Enemy Sprite]       │  Top third
│   Name  Lv.5                   (Pokemon image)       │
│   ████████░░░░ HP                                    │
│                                                       │
│                                                       │  Middle
│                                                       │
│   [Player Sprite]              [Player Info Bar]     │  Bottom third
│   (Pokemon image)              Name  Lv.5            │
│                                ████████░░░░ HP       │
│                                42 / 50                │
│                                                       │
│ ┌───────────────────────────────────────────────────┐ │
│ │ A wild Rattata appeared!                          │ │  Text Box
│ │                                                   │ │  (bottom 20%)
│ │              [FIGHT]   [RUN]                      │ │
│ └───────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
```

Draw each zone as a colored rectangle to verify positioning:

```csharp
// Enemy info bar area (top-left)
int infoW = screenW * 45 / 100;
int infoH = screenH * 10 / 100;
_spriteBatch.Draw(_pixelTexture, new Rectangle(8, screenH * 8 / 100, infoW, infoH), Color.Black * 0.5f);

// Enemy sprite area (top-right)
int spriteSize = screenH * 30 / 100;
_spriteBatch.Draw(_pixelTexture, new Rectangle(screenW * 60 / 100, screenH * 5 / 100, spriteSize, spriteSize), Color.Red * 0.3f);

// Player sprite area (bottom-left)
_spriteBatch.Draw(_pixelTexture, new Rectangle(screenW * 5 / 100, screenH * 40 / 100, spriteSize, spriteSize), Color.Blue * 0.3f);

// Player info bar area (bottom-right)
_spriteBatch.Draw(_pixelTexture, new Rectangle(screenW * 52 / 100, screenH * 52 / 100, infoW, infoH + screenH * 5 / 100), Color.Black * 0.5f);

// Text box (bottom 20% of screen)
int textBoxY = screenH * 78 / 100;
int textBoxH = screenH * 20 / 100;
_spriteBatch.Draw(_pixelTexture, new Rectangle(0, textBoxY, screenW, textBoxH), Color.Black * 0.85f);

// Border line on text box
_spriteBatch.Draw(_pixelTexture, new Rectangle(0, textBoxY, screenW, 2), Color.White * 0.5f);
```

This gives you the full layout with colored placeholder zones. Resize the window and everything scales proportionally.

### POC Step 5: Draw HP Bars

An HP bar is three layered rectangles drawn on top of each other:

1. **Border** — dark gray rectangle, full width
2. **Background** — black rectangle, 1px smaller on each side (the "empty" portion)
3. **Fill** — colored rectangle, width proportional to current HP / max HP

The fill color changes based on HP percentage:
- Above 50%: green (`Color(0, 200, 72)`)
- 20% to 50%: yellow/orange (`Color(248, 176, 0)`)
- Below 20%: red (`Color(240, 48, 48)`)

```csharp
void DrawHPBar(int x, int y, int width, int height, float hpPercent)
{
    // Border
    _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, height), new Color(64, 64, 64));
    // Background (empty bar)
    _spriteBatch.Draw(_pixelTexture, new Rectangle(x + 1, y + 1, width - 2, height - 2), new Color(32, 32, 32));
    // Fill (current HP)
    Color hpColor = hpPercent > 0.5f ? new Color(0, 200, 72)
                  : hpPercent > 0.2f ? new Color(248, 176, 0)
                  : new Color(240, 48, 48);
    int fillWidth = (int)((width - 2) * hpPercent);
    _spriteBatch.Draw(_pixelTexture, new Rectangle(x + 1, y + 1, fillWidth, height - 2), hpColor);
}
```

Call this for both Pokemon, passing their current HP percentage.

### POC Step 6: Draw the Text Box and Messages

The text box is a dark semi-transparent panel at the bottom of the screen. Battle messages are drawn on top of it using the SpriteFont.

```csharp
// Text box background
int textBoxY = screenH * 78 / 100;
_spriteBatch.Draw(_pixelTexture, new Rectangle(0, textBoxY, screenW, screenH - textBoxY), Color.Black * 0.85f);
_spriteBatch.Draw(_pixelTexture, new Rectangle(0, textBoxY, screenW, 2), Color.White * 0.4f);

// Battle message text
string message = "A wild Rattata appeared!";
Vector2 textPos = new Vector2(16, textBoxY + 12);
_spriteBatch.DrawString(_battleFont, message, textPos, Color.White);
```

For the character-by-character typewriter effect (text appearing one letter at a time):
- Track a `_visibleChars` counter that starts at 0 and increments over time
- Draw `message.Substring(0, _visibleChars)` instead of the full message
- Increment `_visibleChars` by `charsPerSecond * deltaTime` each frame
- When the player presses a button, instantly set `_visibleChars = message.Length`

### POC Step 7: Draw the Action Menu

The action menu appears inside the text box area when it's the player's turn. It shows Fight and Run as selectable options.

```csharp
// Menu items
string[] menuItems = { "FIGHT", "RUN" };
int selectedIndex = 0;  // Track with up/down input

int menuX = screenW * 55 / 100;
int menuY = textBoxY + 10;
int lineHeight = 24;

for (int i = 0; i < menuItems.Length; i++)
{
    Color textColor = (i == selectedIndex) ? Color.Yellow : Color.White;
    string prefix = (i == selectedIndex) ? "> " : "  ";
    _spriteBatch.DrawString(_battleFont, prefix + menuItems[i], new Vector2(menuX, menuY + i * lineHeight), textColor);
}
```

Input handling in `UpdateBattle()`:
- Up/Down arrows move `selectedIndex`
- Enter/E confirms the selection
- If "FIGHT" selected → switch to move selection menu (same pattern, 4 move names)
- If "RUN" selected → end the battle, return to overworld

### POC Step 8: Draw the Move Selection Menu

When the player selects FIGHT, replace the action menu with a 2x2 grid of moves:

```
┌─────────────────────────────────────┐
│ > Tackle        Growl               │
│   Vine Whip     Leech Seed          │
│                          PP: 25/25  │
└─────────────────────────────────────┘
```

Same drawing technique — `DrawString` for each move name, highlight the selected one in yellow, show PP for the currently highlighted move. Navigate with arrow keys in a 2x2 grid (up/down/left/right).

### POC Step 9: Draw Pokemon Sprites

For the proof of concept, Pokemon sprites can be simple colored squares with the Pokemon name drawn on them. For a more complete version:

1. Create or download front/back Pokemon sprite PNGs (standard size is 96x96 pixels)
2. Place them in the Content folder or load them directly as `Texture2D`
3. Draw the enemy's front sprite in the top-right zone
4. Draw the player's Pokemon back sprite in the bottom-left zone

Loading a PNG directly without the content pipeline:
```csharp
using var stream = File.OpenRead("Assets/Sprites/rattata_front.png");
Texture2D rattatSprite = Texture2D.FromStream(GraphicsDevice, stream);
```

Draw with scaling to fit the zone:
```csharp
_spriteBatch.Draw(rattatSprite, new Rectangle(spriteX, spriteY, spriteSize, spriteSize), Color.White);
```

### POC Step 10: Wire Up the Battle State Machine

The battle screen cycles through phases. Each phase controls what's drawn and what input does:

```
Intro → PlayerChoice → MoveSelect → ExecuteTurn → CheckResult → (loop or end)
```

```csharp
public enum BattlePhase
{
    Intro,           // "A wild Rattata appeared!" — wait for button press
    PlayerChoice,    // Show Fight/Run menu — wait for selection
    MoveSelect,      // Show 4 moves — wait for selection
    EnemyTurn,       // "Rattata used Tackle!" — auto-advance after delay
    PlayerTurn,      // "Bulbasaur used Vine Whip!" — auto-advance after delay
    DamageResult,    // "It's super effective!" — auto-advance after delay
    CheckFaint,      // Did anyone faint? If yes → Victory or Defeat
    Victory,         // "You won!" — wait for button press → fade out
    Defeat,          // "You blacked out!" — wait for button press → fade out
    FadeOut          // Fading back to overworld
}
```

In `UpdateBattle(float deltaTime)`:
- Each phase checks input or advances a timer
- Phase transitions happen by setting the next phase
- Drawing code checks the current phase to decide what to show

### POC Step 11: Simple Damage Calculation

For the proof of concept, use a simplified version of the Gen 1 damage formula:

```csharp
int CalculateDamage(BattlePokemon attacker, BattlePokemon defender, Move move)
{
    // Base damage: ((2 * Level / 5 + 2) * Power * Attack / Defense) / 50 + 2
    float base = ((2f * attacker.Level / 5f + 2f) * move.Power * attacker.Attack) / (float)defender.Defense / 50f + 2f;

    // Random multiplier: 85% to 100%
    float random = Random.Shared.Next(85, 101) / 100f;

    // Type effectiveness: 2.0 (super effective), 0.5 (not very effective), 0.0 (immune)
    float typeMultiplier = TypeChart.GetEffectiveness(move.Type, defender.Type);

    return Math.Max(1, (int)(base * random * typeMultiplier));
}
```

### POC Step 12: Create Test Data

Hardcode a few Pokemon and moves to test with:

```csharp
// Player's starter
var bulbasaur = new BattlePokemon("Bulbasaur", PokemonType.Grass, level: 5,
    hp: 45, attack: 49, defense: 49, speed: 45,
    moves: new[] {
        new Move("Tackle", PokemonType.Normal, power: 40, accuracy: 100, pp: 35),
        new Move("Growl", PokemonType.Normal, power: 0, accuracy: 100, pp: 40),
        new Move("Vine Whip", PokemonType.Grass, power: 45, accuracy: 100, pp: 25),
        new Move("Leech Seed", PokemonType.Grass, power: 0, accuracy: 90, pp: 10),
    });

// Wild encounter
var rattata = new BattlePokemon("Rattata", PokemonType.Normal, level: 3,
    hp: 30, attack: 56, defense: 35, speed: 72,
    moves: new[] {
        new Move("Tackle", PokemonType.Normal, power: 40, accuracy: 100, pp: 35),
        new Move("Tail Whip", PokemonType.Normal, power: 0, accuracy: 100, pp: 30),
    });
```

### POC Step 13: End-to-End Test

With all pieces in place, the full flow works like this:

1. Player walks on grass → random encounter triggers → screen fades to black
2. Battle screen appears → "A wild Rattata appeared!" prints character by character
3. Player presses button → Fight/Run menu appears
4. Player selects Fight → move list appears (Tackle, Growl, Vine Whip, Leech Seed)
5. Player picks Vine Whip → compare speeds → faster Pokemon attacks first
6. "Bulbasaur used Vine Whip!" → damage calculated → Rattata HP bar shrinks → "It's super effective!"
7. "Rattata used Tackle!" → damage calculated → Bulbasaur HP bar shrinks
8. Back to step 3 — repeat until one side faints
9. "Rattata fainted!" → "You won!" → player presses button → fade to black → overworld resumes

### POC Milestone Checklist

These are in order — each one is testable on its own:

- [ ] SpriteFont loads and can draw text on screen
- [ ] GameState enum switches between Overworld and Battle (debug key)
- [ ] Battle screen draws a colored background (no overworld visible)
- [ ] Layout zones are visible (colored placeholder areas for sprites, info bars, text box)
- [ ] HP bars draw with correct colors at different percentages
- [ ] Text box shows a message with typewriter effect
- [ ] Action menu (Fight/Run) navigates with arrow keys and highlights selection
- [ ] Move menu shows 4 moves in a 2x2 grid with PP display
- [ ] Pokemon data structures exist (name, HP, attack, defense, speed, type, moves)
- [ ] Damage formula calculates a number and subtracts from HP
- [ ] Turn flow works: player picks move → both sides attack → HP bars update
- [ ] Fainting detected → victory/defeat message shown
- [ ] Battle ends → fade to black → overworld resumes on same tile
- [ ] Encounter detection triggers battle from grass tile (replaces debug key)
