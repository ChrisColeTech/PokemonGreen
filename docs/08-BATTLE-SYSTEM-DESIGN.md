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
