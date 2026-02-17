# 28 - Map Editor Encounters, Electron Wrapper & Data Architecture Handoff

**Date:** 2026-02-17
**Scope:** MapEditor restructure (Electron wrapper, page navigation, native file dialogs), encounter editor UI, encounter data baked into generated C# map classes, data architecture discussion
**Depends on:** Doc 15 (Pokemon Encounter System Design), Doc 16 (Persistence & Encounters)

---

## 1. What We Accomplished

### MapEditor → Electron App with Page Navigation

Restructured the MapEditor from a single-page Vite/React app into a multi-page Electron application:

| Change | Detail |
|--------|--------|
| **Electron wrapper** | `electron/main.js` + `electron/preload.js` — context-isolated, no node integration |
| **Monorepo layout** | Root `package.json` runs `frontend` + `electron` via `concurrently` |
| **React Router** | `react-router-dom` v7 with `NavSidebar` for page navigation |
| **Shared layout** | `App.tsx` renders `MenuBar` → `NavSidebar` + `<Routes>` (layout is not inside pages) |
| **Native file dialogs** | `window.electronAPI.openFile()` / `saveFile()` / `readFile()` via IPC |
| **Window persistence** | `electron-store` saves/restores window bounds across sessions |

File structure after restructure:
```
PokemonGreen.MapEditor/
├── package.json              # root — runs "npm run dev" with concurrently
├── frontend/
│   ├── package.json          # vite + react + zustand + react-router-dom
│   ├── src/
│   │   ├── App.tsx           # shared layout: MenuBar, NavSidebar, Routes
│   │   ├── pages/
│   │   │   ├── EditorPage.tsx
│   │   │   └── EncountersPage.tsx
│   │   ├── components/layout/
│   │   │   ├── MenuBar.tsx   # File menu with native Electron dialogs
│   │   │   ├── NavSidebar.tsx # 56px icon sidebar (Grid/Swords)
│   │   │   └── AppShell.tsx  # Editor-page-specific layout (sidebar + canvas + props)
│   │   ├── services/
│   │   │   ├── codeGenService.ts  # generates C# MapDefinition subclasses
│   │   │   └── registryService.ts # parses C# maps + tile registries
│   │   ├── store/
│   │   │   └── editorStore.ts     # zustand + immer, localStorage persistence
│   │   └── types/
│   │       ├── editor.ts
│   │       ├── encounters.ts      # NEW — encounter types, constants, defaults
│   │       └── electron.d.ts      # window.electronAPI type declarations
│   └── ...
└── electron/
    ├── package.json          # electron + electron-store
    ├── main.js               # IPC handlers: open-file, save-file, read-file, browse-folder, store
    └── preload.js            # contextBridge exposing electronAPI
```

### Encounter Editor Page

Full encounter authoring UI on the `/encounters` route:

- **Encounter groups** — each group has an encounter type (tall_grass, cave_floor, surf, etc.) and a base encounter rate (0-255)
- **Species picker** — searchable dropdown filtering species by name, loaded from `species.json` via native file picker
- **Entry fields** — min/max level, weight, required badges, required flags (comma-separated)
- **Weight visualization** — color-coded probability bar per group showing each entry's % chance
- **CRUD** — add/remove groups, add/remove entries, all wired to zustand store with immer
- **Persistence** — encounter data saved to localStorage alongside tile data

### Encounter Data Baked into C# Map Classes

The map editor's Export Map (C#) now generates encounter tables directly in the MapDefinition subclass:

```csharp
public sealed class Route1 : MapDefinition
{
    // ... BaseTileData, OverlayTileData, WalkableTileIds ...

    public const float ProgressMultiplier = 0.3f;

    private static readonly EncounterTable[] EncounterGroups =
    [
        new() { EncounterType = "tall_grass", BaseEncounterRate = 26, Entries =
        [
            new() { SpeciesId = 16, MinLevel = 3, MaxLevel = 5, Weight = 40 }, // Pidgey
            new() { SpeciesId = 19, MinLevel = 2, MaxLevel = 4, Weight = 35 }, // Rattata
        ] },
    ];

    private Route1()
        : base("default", "route_1", "Route 1", 20, 15, 16,
               BaseTileData, OverlayTileData, WalkableTileIds,
               null, null, 0, 0,
               EncounterGroups, ProgressMultiplier)
    { }
}
```

Key details:
- Species are stored by **name** in the editor (`"Pidgey"`) and resolved to **int ID** on export by looking up the loaded species list
- Species name is preserved as a `// comment` for readability
- Import (re-opening a generated `.g.cs`) parses the `// comment` back to restore the species name

### C# Runtime Wiring

Updated the game runtime so encounter data flows from generated maps into the encounter system:

| File | Change |
|------|--------|
| `MapDefinition.cs` | Added `EncounterTable[] encounterGroups` and `float progressMultiplier` to constructor; exposed as `EncounterGroups` and `ProgressMultiplier` properties |
| `EncounterRegistry.cs` | Changed `LoadForMap(string mapId)` → `LoadForMap(MapDefinition mapDef)`; reads encounter data directly from the map instead of loading JSON files |
| `GameWorld.cs` | Updated call from `LoadForMap(mapDef.Id)` to `LoadForMap(mapDef)` |

The encounter JSON files in `Content/Data/Encounters/` are now **dead** — all encounter data lives in generated C# classes.

---

## 2. What Work Remains

### Immediate (Blocking)

1. **Game data architecture** — Species (802), moves (800+), items (80+), learnsets, evolutions, TMs are currently split across JSON files, hardcoded registries, and embedded resources with no relational integrity. Need to decide: seed a SQLite `gamedata.db` or continue with JSON.

2. **MoveRegistry scale-up** — Currently 12 hardcoded moves in `MoveRegistry.cs`. Need 800+ moves with types, power, accuracy, PP, effects. This is the biggest data gap.

3. **Species learnsets** — `SpeciesData` has no `learnset` field. Every Pokemon needs a list of moves learned by level-up. This is a join: `species_id → move_id + level`.

4. **Species evolutions** — No evolution data exists. Need: `species_id → target_species_id + method + condition`.

5. **Type effectiveness table** — Currently no type chart. Need a 18x18 multiplier matrix (or equivalent lookup).

### Near-term

6. **MapEditor species loading UX** — Species list requires manual file picker each session. Could auto-load from a known path or bundle a trimmed copy.

7. **Encounter JSON cleanup** — Remove the now-dead `Content/Data/Encounters/*.json` files and the JSON deserialization code from `MapEncounterData.cs`.

8. **Items.json embedding** — The `.csproj` only explicitly embeds `Sprites/**` and `Player/**`. `Data/items.json` may not actually be embedded — needs verification.

9. **Two copies of species.json** — Both `Data/species.json` and `Content/Data/species.json` exist in Assets project. Only one is needed.

---

## 3. Optimizations — Prime Suspects

### 3.1 SQLite Game Data DB (Highest Impact)

**Problem:** Data is fragmented — species in a loose JSON file, items in an embedded resource, moves hardcoded, no learnsets/evolutions at all. Adding relational data (learnsets, evolutions, TM links) to JSON creates deeply nested arrays with no referential integrity.

**Optimization:** Seed a read-only `gamedata.db` at build time from JSON source files. One `GameDataDb` service replaces `SpeciesRegistry`, `MoveRegistry`, and `ItemRegistry`. FK constraints catch bad data at seed time. Joins are natural (species → learnsets → moves).

**Schema sketch:**
```sql
species (id PK, name, hp, atk, def, spa, spd, spe, type1, type2, growth_rate, catch_rate, base_exp)
moves (id PK, name, type, category, power, accuracy, pp, effect)
learnsets (species_id FK, level, move_id FK)
evolutions (species_id FK, target_id FK, method, condition)
items (id PK, name, category, buy_price, sell_price, effect)
tm_moves (item_id FK, move_id FK)
type_effectiveness (atk_type, def_type, multiplier)
```

### 3.2 Encounter Editor Species Auto-load

**Problem:** Users must click "Load Species Data" and pick `species.json` via file dialog every session. The species list is not persisted because it's too large for localStorage.

**Options:**
- Store the last-used file path in `electron-store` and auto-load on startup
- Bundle a trimmed species list (id, name, type1, type2 only — ~40KB) in `frontend/public/data/`
- Both: bundle a default, allow override via file picker

### 3.3 Tile Registry Consistency

**Problem:** The TileRegistry is defined in two places: the editor's `default.json` and the game's `TileRegistry.cs`. Edits in one don't propagate to the other.

**Optimization:** Make the editor's Export Registry (C#) the single source of truth. The editor exports `TileRegistry.cs` and generated maps reference tile IDs from it. Remove the need to manually sync.

### 3.4 MapDefinition Constructor Arg Sprawl

**Problem:** The `MapDefinition` constructor now has 14 parameters. Adding more features (ambient music, weather, NPC spawns) will make it worse.

**Optimization:** Introduce a `MapOptions` record or builder:
```csharp
protected MapDefinition(string worldId, string id, string name,
    int width, int height, int tileSize,
    int[] baseTileData, int?[] overlayTileData, int[] walkableTileIds,
    MapOptions? options = null)
```
Where `MapOptions` bundles warps, connections, world position, encounters, and future fields.

---

## 4. Step-by-Step: Getting the App Fully Working

### Prerequisites
- Node.js 18+ and npm
- .NET 8 SDK

### MapEditor (Electron + Vite)

```bash
# 1. Install dependencies
cd src/PokemonGreen.MapEditor
npm install
cd frontend && npm install && cd ..
cd electron && npm install && cd ..

# 2. Start dev mode (launches Vite + Electron concurrently)
npm run dev

# 3. The Electron window opens at http://localhost:5173
#    - Editor page: draw tiles on the canvas
#    - Encounters page: click nav sidebar (swords icon)
```

### Loading Species Data (for Encounters page)

1. Navigate to the Encounters page via the sidebar
2. Click **Load Species Data** in the top bar
3. Pick `src/PokemonGreen.Assets/Data/species.json` from the native file dialog
4. Species names now appear in the encounter entry dropdowns

### Exporting a Map with Encounters

1. Define encounter groups on the Encounters page
2. Go to **File > Export Map (C#)** in the MenuBar
3. The generated `.g.cs` file includes `EncounterGroups` and `ProgressMultiplier`
4. Place the file in `src/PokemonGreen.Core/Maps/Generated/`

### Building the Game

```bash
cd src/PokemonGreen.3D
dotnet build
dotnet run
```

### Running Tests

There are currently no automated tests for the MapEditor frontend or the encounter system. Testing is manual:
- Verify Export → Import round-trip preserves encounter data
- Verify generated C# compiles with `dotnet build`
- Verify encounter triggers work in-game on maps with encounter data

---

## 5. How to Start/Test the App

### MapEditor

```bash
cd src/PokemonGreen.MapEditor
npm run dev
```

This runs two processes via `concurrently`:
- **Vite dev server** on `http://localhost:5173` (hot reload)
- **Electron** loading that URL (native window with file dialogs)

If Electron fails to connect (Vite hasn't started yet), it will show a blank window — just reload with Ctrl+R.

### Game (MonoGame / .NET 8)

```bash
cd src/PokemonGreen.3D
dotnet run
```

The game loads species from `Data/species.json` in the output directory. If you see missing species errors, ensure `PokemonGreen.Assets` has built and copied its data files.

---

## 6. Known Issues & Strategies

### Issue 1: Encounter JSON Files Are Dead Code

The `Content/Data/Encounters/*.json` files and `MapEncounterData.cs` (with its `[JsonPropertyName]` attributes) are no longer used at runtime. `EncounterRegistry` reads from `MapDefinition` directly.

**Strategy:** Delete the JSON encounter files. Keep `MapEncounterData.cs` only if something else references it, otherwise remove. Clean up unused `System.Text.Json` and `System.IO` imports.

### Issue 2: Species ID Resolution Is Fragile

The editor stores species by name (`"Pidgey"`), resolves to int ID on export by searching the loaded species list. If species aren't loaded, all IDs export as `0`. There's no warning when this happens.

**Strategy A:** Make species loading mandatory before export — show a validation error if species aren't loaded and encounter data exists.

**Strategy B:** Store both name and ID in the editor's encounter data. When species are loaded, sync IDs. On export, use the stored ID as fallback if species list isn't loaded.

**Strategy C:** Bundle a minimal ID→name mapping (no stats needed) in the frontend so export always works. Full species data only needed for the search dropdown.

### Issue 3: Items.json May Not Be Embedded

The `PokemonGreen.Assets.csproj` only explicitly embeds `Sprites/**` and `Player/**`. The `Data/items.json` file relies on SDK default behavior to be included as an embedded resource, which may not work.

**Strategy:** Add an explicit `<EmbeddedResource Include="Data\items.json" />` to the `.csproj`, or switch items to the same file-copy strategy used by species. Best long-term: migrate to the SQLite game data DB.

### Issue 4: No Automated Tests

No unit tests exist for the encounter editor, code generation, C# parsing, or encounter registry logic. Round-trip bugs (export → import losing data) are caught manually.

**Strategy:** Add focused tests for the critical paths:
- `codeGenService.test.ts` — generate a map class with encounters, verify output matches expected C#
- `registryService.test.ts` — parse a generated C# file, verify encounter data round-trips
- `EncounterRegistry` C# unit test — load a MapDefinition with known encounters, verify `TryEncounter` returns expected species/levels

---

## 7. New Architecture & Features

### Architecture: SQLite Game Data DB

The biggest architectural change on the horizon. Current state vs. proposed:

| Data | Current | Proposed |
|------|---------|----------|
| Species (802) | Loose JSON, loaded at startup | `species` table in `gamedata.db` |
| Moves (12 → 800+) | Hardcoded in `MoveRegistry.cs` | `moves` table |
| Items (80+) | Embedded JSON resource | `items` table |
| Learnsets | **Does not exist** | `learnsets` table (species_id, level, move_id) |
| Evolutions | **Does not exist** | `evolutions` table (species_id, target_id, method) |
| Type chart | **Does not exist** | `type_effectiveness` table |
| TM/HM links | **Does not exist** | `tm_moves` table |

**Build pipeline:** JSON source files → seeder tool → `gamedata.db` → copied to output directory.

**Runtime:** Single `GameDataDb` static class opens `gamedata.db` read-only. Replaces `SpeciesRegistry`, `MoveRegistry`, `ItemRegistry` with typed query methods.

### Quick Wins

1. **Auto-load species on startup** — Save last file path in `electron-store`, read on app launch. Eliminates the manual "Load Species Data" step. (~30 min)

2. **Delete dead encounter JSON files** — Remove `Content/Data/Encounters/*.json` and the JSON loading code in `EncounterRegistry`. The data now lives in generated map classes. (~15 min)

3. **Export validation** — Before exporting C#, check that species are loaded if encounters exist. Show a dialog warning instead of silently exporting `SpeciesId = 0`. (~20 min)

4. **Encounter type auto-detect** — When adding a new encounter group, pre-select the encounter type based on which tile behaviors are used in the current map (e.g., if the map has "tall_grass" tiles, default to tall_grass). (~30 min)

5. **Consolidate species.json** — Delete the duplicate `Content/Data/species.json`. Keep only `Data/species.json` with `CopyToOutputDirectory`. (~5 min)

---

## 8. File Change Summary

| File | Action | Description |
|------|--------|-------------|
| `MapEditor/package.json` | Existing | Root monorepo with concurrently |
| `MapEditor/electron/main.js` | Modified | IPC handlers for native file dialogs |
| `MapEditor/electron/preload.js` | Modified | contextBridge exposing electronAPI |
| `MapEditor/frontend/src/App.tsx` | Existing | Shared layout with MenuBar + NavSidebar + Routes |
| `MapEditor/frontend/src/types/encounters.ts` | **New** | EncounterEntry, EncounterGroup, MapEncounterData, SpeciesInfo, constants |
| `MapEditor/frontend/src/types/electron.d.ts` | Modified | Added readFile to ElectronAPI interface |
| `MapEditor/frontend/src/pages/EncountersPage.tsx` | Modified | Full encounter editor UI |
| `MapEditor/frontend/src/store/editorStore.ts` | Modified | Encounter state, species loading, CRUD actions |
| `MapEditor/frontend/src/services/codeGenService.ts` | Modified | Generate encounter data in C# classes |
| `MapEditor/frontend/src/services/registryService.ts` | Modified | Parse encounter data from C# imports |
| `Core/Maps/MapDefinition.cs` | Modified | Added EncounterGroups + ProgressMultiplier to constructor and properties |
| `Core/Encounters/EncounterRegistry.cs` | Modified | Reads from MapDefinition instead of JSON files |
| `Core/GameWorld.cs` | Modified | Passes MapDefinition to LoadForMap |
