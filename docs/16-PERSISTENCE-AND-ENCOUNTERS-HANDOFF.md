# 16 - Persistence, Encounters & CRUD: Lessons Learned & Handoff

**Date:** 2026-02-16
**Scope:** SQLite save system, encounter system wiring, PC box storage, day/night persistence
**Codebase:** 81 .cs files across 3 projects, 7 encounter files, 3 save files, 7 Pokemon files

---

## 1. What We Accomplished

### SQLite Persistence Layer (New)
- **SaveManager** (`Core/Save/SaveManager.cs`) — full CRUD against per-slot SQLite databases
  - `Save(slot, data)` / `Load(slot)` / `DeleteSave(slot)` / `HasSave(slot)`
  - `GetSaveSlots()` scans the save directory for any `save*.db` files (no artificial slot cap)
  - `NextAvailableSlot()` finds the lowest unused slot number
  - Schema versioning with migration support (currently v2)
  - Atomic save via DELETE-all + INSERT-all inside a single transaction
  - Save path: `%LOCALAPPDATA%/PokemonGreen/Saves/save{N}.db`
- **GameSaveData** (`Core/Save/GameSaveData.cs`) — DTO snapshot of all player state
- **SqliteHelpers** (`Core/Save/SqliteHelpers.cs`) — CSV serialization for small int arrays (IVs, EVs, moves)
- **Schema v2 migration** — added `game_time_seconds` column to player table for day/night persistence

### PC Box Storage (New)
- **PCBox** / **PCBoxes** (`Core/Pokemon/PCBoxes.cs`) — 18 boxes x 30 slots = 540 Pokemon capacity
  - Store, StoreAt, Withdraw, Clear (release) per slot
  - Auto-find-first-empty across all boxes
  - Single `pokemon` table in SQLite with discriminated location via CHECK constraint:
    - `party_slot IS NOT NULL` → party member (0-5)
    - `box_number/box_slot IS NOT NULL` → PC storage
  - Partial unique indexes prevent duplicate slot assignments

### Encounter System Wiring (New)
- **EncounterRegistry** (`Core/Encounters/EncounterRegistry.cs`) — central encounter service
  - `LoadForMap(mapId)` loads JSON encounter tables
  - `TryEncounter(encounterType, progress)` — rate check, progress filtering, weighted species selection, level scaling
- **EncounterTypeResolver** — maps tile overlay behaviors ("tall_grass", "cave_floor") to encounter type strings
- **PlayerProgress** — tracks badge count, highest party level, story flags for encounter gating
- **21 encounter entries** across 4 maps (test_map_a/b/c/d), 9 unique species
- **GameWorld integration** — replaced hardcoded 1-in-10 roll with full encounter pipeline: tile detection -> behavior lookup -> type resolution -> registry -> battle

### Party & Inventory CRUD (Modified)
- **Party.cs** — added `Remove`, `RemoveAt`, `Swap`, `Members` (IReadOnlyList), `Clear`
  - Remove enforces party must keep at least 1 member
- **PlayerInventory.cs** — added `GetAllPouches()` for serialization, `Clear()` for load
- **PartyPokemon.Create()** — now assigns default Tackle move so wild Pokemon always have at least one move

### Time Persistence (New)
- **DayNightCycle** — exposed `ElapsedSeconds` get/set for save/restore
- **Game1** — accumulates `_playtimeSeconds` every frame, persists both playtime and game-time-of-day through saves
- On load: day/night cycle resumes from saved position instead of always starting at noon

---

## 2. What Work Remains

### Critical Path (Must-Have for Playable Loop)
| Feature | Status | Blocking? |
|---------|--------|-----------|
| Save slot selection UI | No UI — hardcoded to slot 1 | Yes — player can't choose which save to load |
| Catch mechanic | Not started | Yes — can't grow your team |
| PC access screen | Data layer done, no UI | Yes — can't manage stored Pokemon |
| Item use in battle | Bag screen opens but items do nothing | Yes — Potions/Pokeballs non-functional |
| Item use from overworld | Not started | Medium |
| Rename Pokemon | Not started | Low |
| Release Pokemon | PCBox.Clear() exists, no UI | Low |

### Designed But Not Implemented
| Design Doc | What Exists | What's Missing |
|------------|-------------|----------------|
| 13-EXP-AND-BATTLE-REWARDS | EXP formula, growth rates, stat calc all implemented in code | Move learning on level-up, evolution triggers, evolution screen |
| 14-HEALTH-AND-RECOVERY-ITEMS | 50+ items catalogued with effect grammar | ItemEffectParser, ItemUseHandler, StatusCondition enum, battle bag integration |
| 15-POKEMON-ENCOUNTER-SYSTEM | Fully implemented and wired | Ability modifiers (MagnetPull/Static), time-of-day encounters, repel system |

### Known Gaps
- **Money** — hardcoded to 0 in PerformSave, no economy system
- **PlayerName** — hardcoded to "Red", no name entry
- **Encounter hitbox alignment** — sub-tile detection works but flame sprite visual bounds don't perfectly match collision rect (documented in 12-BATTLE-BG-AND-ENCOUNTER-HANDOFF)
- **Move PP not synced back** — after battle, PP deductions on BattlePokemon don't write back to PartyPokemon
- **Fainted party members** — no whiteout/Pokemon Center healing flow

---

## 3. Optimization Suspects

### 3.1 SaveManager: Connection-Per-Operation
Every Save/Load/GetSaveSlots opens and closes a new SQLite connection. For the current use case (save on pause menu, load on startup) this is fine. But if we add autosave or frequent reads (e.g., checking save metadata for a title screen), connection pooling or keeping a connection open for the active slot would reduce overhead.

**Quick win:** Cache `SaveSlotInfo` list after first scan; invalidate on save/delete.

### 3.2 Encounter JSON Loading
`EncounterRegistry.LoadForMap()` reads and deserializes a JSON file every time a map loads. With 4 maps this is negligible, but at 50+ maps with frequent transitions, this becomes I/O-bound.

**Quick win:** Cache parsed `MapEncounterData` in a dictionary keyed by map ID. Only load from disk on first access.

### 3.3 Battle Model Loading
All 4 battle background sets (Grass, TallGrass, Cave, Dark) with 10 .dae models are loaded at startup in `LoadBattleModels()`. This blocks the loading screen and consumes GPU memory for scenes the player may never visit.

**Quick win:** Lazy-load battle scenes on first encounter of each type. The model cache dictionary already exists — just defer the `LoadModel()` call.

### 3.4 Overlay Tile Iteration
`TileRenderer.DrawOverlaysBehindPlayer` and `DrawOverlaysInFrontOfPlayer` iterate visible tiles twice per frame with per-tile branching. For large maps with many overlay objects, this is the hottest draw path.

**Quick win:** Pre-sort overlay tiles into behind/front lists when the map loads, indexed by Y coordinate. Draw from pre-sorted lists instead of re-checking every frame.

---

## 4. Step-by-Step: Getting the App Fully Working

### Prerequisites
```
- .NET 9 SDK
- Windows 10/11 (MonoGame DesktopGL)
- Git
```

### Build & Run
```bash
cd D:\Projects\PokemonGreen
dotnet restore src/PokemonGreen/PokemonGreen.csproj
dotnet build src/PokemonGreen/PokemonGreen.csproj
dotnet run --project src/PokemonGreen/PokemonGreen.csproj
```

### First Launch Behavior
1. No save file exists → test party (Charmander/Pidgey/Bulbasaur) and test inventory load
2. Player spawns on `test_map_center` at the world registry's spawn point
3. Walk with WASD/arrows, run with shift (not yet wired — uses default walk speed)
4. ESC opens pause menu → Save writes to `%LOCALAPPDATA%/PokemonGreen/Saves/save1.db`
5. Close and relaunch → save loads automatically from slot 1

### Encounter Testing
1. Walk from `test_map_center` to `test_map_a` (tall grass encounters)
2. Step on tall grass tiles — ~10% chance per step (rate byte 26/255)
3. Battle screen loads with 3D background, wild Pokemon from encounter table
4. Fight menu shows party lead's moves with PP tracking
5. Run option exits battle immediately

### Verify Save Round-Trip
1. Win a battle → party Pokemon gains EXP
2. ESC → Save
3. Close game, relaunch
4. Verify: same map, same position, same party with earned EXP, same time of day

### Inspect Save Data
Open `%LOCALAPPDATA%/PokemonGreen/Saves/save1.db` in DB Browser for SQLite:
- `player` table: position, badges, playtime, game_time_seconds
- `pokemon` table: party (party_slot 0-5) and PC (box_number/box_slot)
- `inventory` table: item_id, category, quantity
- `schema_version` table: should read 2

---

## 5. How to Start/Test the System

### Save System API
```csharp
var sm = new SaveManager();

// List all saves
List<SaveSlotInfo> slots = sm.GetSaveSlots();
// → each has: Slot, PlayerName, BadgeCount, PartyCount, SavedAt, MapId

// Save to any slot
sm.Save(1, gameSaveData);

// Load from any slot (null if doesn't exist)
GameSaveData? data = sm.Load(1);

// Delete a save
sm.DeleteSave(1);

// Next available slot number
int next = sm.NextAvailableSlot(); // 1, 2, 3, ...
```

### Encounter System API
```csharp
// Loaded automatically when GameWorld.LoadMap() is called
EncounterRegistry.LoadForMap("test_map_a");

// Called by GameWorld on each step
WildEncounterResult? result = EncounterRegistry.TryEncounter("tall_grass", progress);
// → result.SpeciesId, result.Level if encounter triggers
// → null if no encounter this step
```

### PC Box API
```csharp
var boxes = new PCBoxes(); // 18 boxes, 30 slots each

// Store Pokemon (auto-find empty slot)
var (boxIdx, slotIdx) = boxes.Store(pokemon);

// Store at specific location
boxes[0].StoreAt(5, pokemon);

// Withdraw
PartyPokemon? pkmn = boxes[0].Withdraw(5);

// Release
boxes[0].Clear(5);
```

### Manual Testing Checklist
- [ ] Fresh launch with no save → test data loads
- [ ] Save → close → relaunch → data persists (party, position, inventory, time of day)
- [ ] Walk into tall grass → encounter triggers → correct species from encounter table
- [ ] Win battle → EXP gained → save → reload → EXP persists
- [ ] Multiple saves: change `_currentSaveSlot` to 2 → save → slot 1 unaffected
- [ ] Delete save file manually → game falls back to test data on next launch

---

## 6. Known Issues & Strategies

### Issue 1: No Save Slot Selection UI
**Problem:** `_currentSaveSlot = 1` is hardcoded. Player can never choose a different slot.
**Strategy:** Build a `SaveSlotScreen` (IScreenOverlay) that calls `GetSaveSlots()`, displays slot summaries (name, badges, playtime, timestamp), and lets the player pick one or create new. Show this screen before the game starts (title screen) and when the player hits Save in the pause menu. Wire `_currentSaveSlot` from the selection.

### Issue 2: Battle PP Not Synced Back to Party
**Problem:** `BattlePokemon` copies move data from `PartyPokemon` at battle start. PP deductions during battle modify the `BattlePokemon` copy, but never write back. On save after battle, the party Pokemon has full PP.
**Strategy:** In the `exitBattle` callback (Game1.cs), iterate `_allyPokemon.Moves` and copy `CurrentPP` values back to the corresponding `PartyPokemon.MovePPs[]` array. This is a 5-line fix.

### Issue 3: Schema Migration on Corrupt/Partial DB
**Problem:** If the game crashes mid-save (power loss, force quit), the transaction rolls back correctly. But if the .db file itself is corrupted (disk error), `Load()` will throw an unhandled exception.
**Strategy:** Wrap `Load()` in a try-catch at the Game1 level. On failure, log the error, rename the corrupt file to `save{N}.db.corrupt`, and fall back to test data. Show the player a message. Also consider writing a `.bak` copy before each save (rotate last 2 backups).

### Issue 4: Encounter Rate Feels Wrong at High Speeds
**Problem:** Encounter check runs once per tile-step, but the check uses a byte rate (0-255) as probability. At running speed, the player crosses tiles faster, so encounters-per-minute increases with speed even though per-step rate is constant.
**Strategy:** Two options: (a) scale rate inversely with speed so encounters-per-minute stays constant regardless of walk/run, or (b) switch to a distance-accumulator model where encounter probability is checked per N pixels traveled rather than per tile boundary. Option (b) matches Gen IV+ behavior.

---

## 7. Architecture Overview

### Project Structure
```
PokemonGreen/                    # MonoGame executable (2 files)
  Game1.cs                       # Main game loop, input, rendering, state machine
  Program.cs                     # Entry point

PokemonGreen.Core/               # Engine library (77 files)
  Battle/                        # BattlePokemon, BattleTurnManager, MoveRegistry
  Encounters/                    # EncounterRegistry, EncounterTypeResolver, PlayerProgress
  Items/                         # ItemRegistry, PlayerInventory, ItemDefinition
  Maps/                          # TileMap, MapDefinition, MapCatalog, WorldRegistry
  Player/                        # Player (position, movement, animation)
  Pokemon/                       # PartyPokemon, Party, PCBoxes, SpeciesRegistry, StatCalculator
  Rendering/                     # PlayerRenderer, TileRenderer, Camera
  Save/                          # SaveManager, GameSaveData, SqliteHelpers
  Systems/                       # DayNightCycle
  UI/                            # MenuBox, MessageBox, BattleInfoBar, Screens (Bag, Party)

PokemonGreen.Assets/             # Content pipeline, .dae model loader, texture store
```

### Data Flow: Save/Load
```
Game1.PerformSave()
  ├─ Build GameSaveData from live state
  │    ├─ _playerParty, _playerBag, _pcBoxes
  │    ├─ _gameWorld.Player (position, facing)
  │    ├─ _gameWorld.Progress (badges, story flags)
  │    ├─ _playtimeSeconds, _dayNightCycle.ElapsedSeconds
  │    └─ DateTime.UtcNow
  └─ SaveManager.Save(slot, data)
       └─ BEGIN TRANSACTION
            DELETE all mutable rows
            INSERT player, pokemon, pc_boxes, inventory, story_flags
          COMMIT

Game1.Initialize()
  └─ SaveManager.Load(slot)
       ├─ Read player → position, badges, money, playtime, game_time
       ├─ Read pokemon (party_slot) → Party
       ├─ Read pokemon (box_number) → PCBoxes
       ├─ Read pc_boxes → box names
       ├─ Read inventory → PlayerInventory
       └─ Read story_flags → HashSet<string>
```

### Data Flow: Encounter
```
GameWorld.Update()
  └─ Player steps on new tile
       ├─ Get overlay tile ID → TileBehavior (e.g., "tall_grass")
       ├─ EncounterTypeResolver.Resolve(behavior) → "tall_grass"
       ├─ EncounterRegistry.TryEncounter("tall_grass", progress)
       │    ├─ Roll rate check (byte 0-255 as probability)
       │    ├─ Filter entries by progress (badges, flags)
       │    ├─ Weighted random species selection
       │    └─ Calculate level (base ± range, scaled by progress)
       ├─ Store result in PendingEncounterResult
       └─ BeginBattleTransition() → fade → Game1.EnterBattle()
            ├─ Create BattlePokemon from encounter result
            ├─ Select 3D background from BattleBackgroundResolver
            └─ Start battle UI sequence
```

---

## 8. Quick Wins & Next Features

### Quick Wins (< 1 hour each)
1. **Sync battle PP back to party** — 5 lines in Game1's `exitBattle` callback. Copy `BattlePokemon.Moves[i].CurrentPP` back to `PartyPokemon.MovePPs[i]`. Fixes PP resetting after every battle.

2. **Save confirmation message** — After `PerformSave()`, show a `_battleMessageBox.Show("Game saved!")` overlay for 1.5 seconds instead of silently closing the pause menu. Player gets feedback that save worked.

3. **Playtime display on save slots** — `SaveSlotInfo` already has `SavedAt`. Add `PlaytimeSeconds` to the slot info query. Format as "HH:MM" for display. Ready for when the slot selection UI is built.

4. **Cache encounter data** — Add a `Dictionary<string, MapEncounterData>` to EncounterRegistry. Check cache before hitting disk. Eliminates redundant JSON parsing on map revisits.

### Next Features (Priority Order)
1. **Save Slot Selection Screen** — Title screen or pause menu overlay showing all saves with name/badges/playtime. "New Game" creates next available slot. Required before the game can ship with multi-save.

2. **Item Use System** — Implement `ItemEffectParser` and `ItemUseHandler` from doc 14. Wire into the Bag screen so Potions heal HP and Pokeballs trigger catch. This unlocks the core gameplay loop.

3. **Catch Mechanic** — Calculate catch rate (Gen III formula: `(3*MaxHP - 2*CurrentHP) * Rate * Ball / (3 * MaxHP)`), shake animation, add caught Pokemon to party or PC if party full. Requires item use system first.

4. **Move Learning on Level-Up** — When `PartyPokemon.AddEXP()` returns levels gained, check the species' learnset for new moves at the gained levels. Show a "wants to learn X" prompt. If party has 4 moves, prompt to forget one.

5. **Pokemon Center / Healing** — Heal all party Pokemon to full HP/PP and cure status. Triggered by interacting with a Nurse NPC or specific tile. Simple: iterate party, set `CurrentHP = MaxHP`, clear status, refill MovePPs.

---

## Appendix: File Inventory (New/Modified This Session)

### New Files
| File | Lines | Purpose |
|------|-------|---------|
| `Core/Save/SaveManager.cs` | ~465 | SQLite CRUD, schema, migration |
| `Core/Save/GameSaveData.cs` | ~39 | Save/load DTO |
| `Core/Save/SqliteHelpers.cs` | ~25 | CSV ↔ int[] |
| `Core/Pokemon/PCBoxes.cs` | ~127 | PC box storage (18x30) |
| `Core/Encounters/EncounterRegistry.cs` | ~150 | Encounter service |
| `Core/Encounters/PlayerProgress.cs` | ~20 | Progress tracking |
| `Content/Data/Encounters/*.json` | 5 files | Per-map encounter tables |

### Modified Files
| File | What Changed |
|------|-------------|
| `Core/Pokemon/Party.cs` | Remove, RemoveAt, Swap, Members, Clear |
| `Core/Pokemon/PartyPokemon.cs` | Default Tackle move in Create() |
| `Core/Items/PlayerInventory.cs` | GetAllPouches(), Clear() |
| `Core/Player/Player.cs` | SetFacing() |
| `Core/Systems/DayNightCycle.cs` | ElapsedSeconds get/set |
| `Core/GameWorld.cs` | Encounter pipeline, PendingEncounterResult, Progress |
| `Core/PokemonGreen.Core.csproj` | Microsoft.Data.Sqlite 9.0.* |
| `PokemonGreen/Game1.cs` | Save/load wiring, playtime tracking, encounter battle creation |

### Dependencies Added
| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Data.Sqlite | 9.0.* | SQLite database access |
