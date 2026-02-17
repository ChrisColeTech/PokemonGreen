# 29 - SQLite Data Pipeline, Move/Evolution Wiring & Battle Integration Handoff

**Date:** 2026-02-17
**Scope:** Species data migration to SQLite, PokeAPI data fetching (moves, learnsets, evolutions), move/evolution wiring into battle and level-up systems, comprehensive test suite
**Depends on:** Doc 28 (Encounters & Data Architecture)

---

## 1. What We Accomplished

### SQLite Game Database (`gamedata.db`)

Migrated from JSON-based species loading to a single SQLite database that holds all game data:

| Table | Rows | Source | Description |
|-------|------|--------|-------------|
| `species` | 802 | `species.json` via `seed-gamedata.mjs` | Base stats, types, growth rates, catch rates |
| `moves` | 728 | PokeAPI `/move/{id}` | Name, type, category, power, accuracy, PP, priority |
| `learnsets` | 51,436 | PokeAPI `/pokemon/{id}` | Species→move mappings with method and level |
| `evolutions` | 502 | PokeAPI `/evolution-chain/{id}` | Evolution triggers, items, levels, conditions |

### Data Pipeline Tools

| Tool | Purpose |
|------|---------|
| `tools/seed-gamedata.mjs` | Node.js script — reads `species.json`, creates/seeds `gamedata.db` species table |
| `tools/fetch-gamedata.py` | Python script — fetches moves, learnsets, evolutions from PokeAPI in batches of 100 with 6s cooldown |
| `tools/package.json` | `better-sqlite3` dependency for the seeder |

### C# Data Layer

| File | What Changed |
|------|--------------|
| `GameDataDb.cs` | **New** — static SQLite accessor. Queries: `GetSpecies`, `GetMove`, `GetLevelUpMoves`, `GetMovesLearnedAtLevel`, `GetEvolutions` |
| `EvolutionData.cs` | **New** — immutable record for evolution chain data |
| `SpeciesRegistry.cs` | **Rewritten** — thin delegate to `GameDataDb`, removed all JSON loading |
| `MoveRegistry.cs` | **Rewritten** — delegates to `GameDataDb.GetMove()`, removed 12 hardcoded moves, now serves all 728 |
| `MoveData.cs` | Added `Priority` property |

### Level-Up System Overhaul

| Feature | Implementation |
|---------|---------------|
| **Wild moveset generation** | `PartyPokemon.Create()` queries learnsets for the last 4 level-up moves at or below the Pokemon's level |
| **Move learning on level-up** | `AddEXP()` checks `GetMovesLearnedAtLevel()` after each level. Appends if <4, replaces oldest if full |
| **Evolution detection** | `CheckEvolution()` queries evolution table for simple level-up triggers (level threshold met, no special conditions) |
| **Evolution execution** | `Evolve()` updates species, preserves custom nicknames, recalculates stats with new base stats |
| **`LevelUpResult` record** | Rich return type from `AddEXP()`: levels gained, new moves, replaced moves, pending evolution |

### Battle Turn Manager Updates

After defeating a foe, the message chain now shows:
1. "{Name} gained {X} EXP. Points!"
2. "{Name} grew to Lv. {X}!" (if leveled)
3. "{Name} learned {MoveName}!" (for each new move)
4. "{OldName} evolved into {NewName}!" (if evolution triggered)
5. "You win!"

### Test Suite — 89 Tests

| Test Class | Count | Coverage |
|------------|-------|----------|
| `MoveDbTests` | 7 | Move lookups, type/category mapping, status moves, priority |
| `LearnsetTests` | 5 | Level-up move queries, ordering, species filtering |
| `MovesetGenerationTests` | 12 | Wild Pokemon get real moves, 4-move cap, PP matching, level-dependent |
| `EncounterPipelineTests` | 3 | Encounter → PartyPokemon → BattlePokemon with BattleMoves |
| `BattleDamageTests` | 5 | HP reduction, floor at 0, PP tracking, SyncToParty |
| `ItemRecoveryTests` | 9 | Potions, Full Restore, Revive, status cures, battle/overworld context |
| `EvolutionDbTests` | 8 | Level-up, item, happiness evolutions, Eevee branching, edge cases |
| `EXPLevelUpTests` | 5 | Level gains, stat recalc, level 100 cap, scaled EXP formula |
| `LevelUpMoveLearnTests` | 7 | New moves on level-up, oldest replacement, no duplicates, 4-max |
| `EvolutionTriggerTests` | 10 | CheckEvolution, Evolve, nickname preservation, full Bulbasaur→Venusaur chain, Eevee conditional skip |
| `SpeciesDbTests` | 4 | Species data, base stats, types, count |
| `FullPipelineTests` | 3 | End-to-end: encounter→battle→EXP→evolution, mid-battle item heal |
| Other (skeletal) | 1 | Pre-existing Collada test |

---

## 2. What Work Remains

### High Priority
- **PokeAPI move IDs ≠ old hardcoded IDs** — `Party.CreateTestParty()` and `BattleScreen3D` still use old hardcoded move IDs (1=Tackle, 2=Scratch, etc.) which are now wrong (PokeAPI: 1=Pound, 10=Scratch, 33=Tackle). These test parties need updating.
- **BattlePokemon test helpers** — `CreateTestAlly()` and `CreateTestFoe()` use old move IDs — should be removed or updated.
- **Type effectiveness** — No type chart. Damage formula ignores move type vs defender type. Need a `TypeChart` class with 18x18 multiplier matrix.
- **STAB (Same Type Attack Bonus)** — Damage formula doesn't apply 1.5x when move type matches attacker type.
- **Attack/Defense in damage** — Current formula only uses `power * level`. Real formula uses attacker's Attack/SpAttack vs defender's Defense/SpDefense.
- **Save/Load move ID migration** — Existing save files may store old hardcoded move IDs. Need migration or graceful fallback.

### Medium Priority
- **Items into SQLite** — `items.json` still loaded via `ItemRegistry` with `System.Text.Json`. Should migrate to `gamedata.db`.
- **Player move replacement UI** — Currently auto-replaces oldest move. Player should get a "forget a move?" prompt.
- **Evolution animation/UI** — `Evolve()` is called silently. Need visual evolution sequence.
- **Happiness tracking** — Eevee→Espeon/Umbreon, Golbat→Crobat need happiness system.
- **Item-triggered evolution** — Fire Stone→Flareon etc. Need item use → evolution check pipeline.
- **Trade evolution** — Machoke→Machamp, Haunter→Gengar etc.

### Low Priority
- **Move effects** — Status moves (Growl, Leer, etc.) show message but don't apply stat changes.
- **Multi-hit moves, recoil, drain** — PokeAPI has `meta` fields we didn't fetch (ailment, crit_rate, drain, stat_changes).
- **Abilities** — Not in the data model at all yet.
- **Held item effects in battle** — `HeldItemId` property exists but is unused.

---

## 3. Optimizations — Prime Suspects

### 1. GameDataDb query caching
`MoveRegistry.GetMove()` hits SQLite on every call — during battle this is called per-move per-turn. Cache moves in a `Dictionary<int, MoveData>` after first lookup. Learnset queries are only called on level-up so those are fine.

### 2. Damage formula upgrade
The current formula `(power * level / 5 + 2) / 3` is placeholder. The real Gen V formula is:
```
damage = ((2*level/5 + 2) * power * A/D) / 50 + 2) * modifier
modifier = STAB * typeEffectiveness * random(0.85..1.0)
```
This uses actual Attack/Defense stats, making battles feel correct and balanced.

### 3. Batch learnset loading
`GenerateMoveset()` calls `GetLevelUpMoves()` then `GetMove()` for each of 4 moves = 5 DB queries per Pokemon creation. Could batch: `SELECT m.* FROM moves m JOIN learnsets l ON m.id = l.move_id WHERE l.species_id = ? AND l.method = 'level-up' AND l.level <= ? ORDER BY l.level DESC LIMIT 4`.

### 4. SQLite WAL mode for reads
The DB is opened read-only but we could enable WAL mode during seeding to allow concurrent reads if ever needed.

---

## 4. Step by Step — Get App Fully Working

1. **Fix test party move IDs** — Update `Party.CreateTestParty()`, `BattlePokemon.CreateTestAlly/Foe()`, and `BattleScreen3D` to use PokeAPI move IDs (Tackle=33, Scratch=10, Growl=45, Ember=52, etc.)
2. **Build and run** — `dotnet build src/PokemonGreen && dotnet run --project src/PokemonGreen`
3. **Verify encounters** — Walk into tall grass, confirm wild Pokemon have real movesets (not just Tackle)
4. **Verify battle** — Fight a wild Pokemon, confirm move names display correctly in menu
5. **Verify level-up** — Win battles until level-up, confirm new move message appears
6. **Verify evolution** — Level a starter to 16, confirm evolution message and species change
7. **Run tests** — `dotnet test src/PokemonGreen.Core.Tests` — all 89 should pass

---

## 5. How to Start/Test the Data Pipeline

### Seed species data
```bash
cd tools && npm install && node seed-gamedata.mjs
```
Output: `species: 802 rows` into `src/PokemonGreen.Assets/Data/gamedata.db`

### Fetch PokeAPI data
```bash
# Use the project's venv
D:/Projects/PokemonGreen/.venv/Scripts/python.exe tools/fetch-gamedata.py

# Or fetch specific tables
D:/Projects/PokemonGreen/.venv/Scripts/python.exe tools/fetch-gamedata.py --only moves
D:/Projects/PokemonGreen/.venv/Scripts/python.exe tools/fetch-gamedata.py --only learnsets
D:/Projects/PokemonGreen/.venv/Scripts/python.exe tools/fetch-gamedata.py --only evolutions
```
Fetches in batches of 100 with 6s cooldown. Skips existing data (resume-safe). Progress bars via tqdm.

### Run tests
```bash
dotnet test src/PokemonGreen.Core.Tests --verbosity normal
```

### Inspect the database
```python
python -c "
import sqlite3
conn = sqlite3.connect('src/PokemonGreen.Assets/Data/gamedata.db')
for t in ['species','moves','learnsets','evolutions']:
    print(f'{t}: {conn.execute(f\"SELECT COUNT(*) FROM {t}\").fetchone()[0]} rows')
conn.close()
"
```

---

## 6. Known Issues & Strategies

### Issue 1: Hardcoded move IDs throughout codebase
**Problem:** Old code assumed Tackle=1, Scratch=2, Ember=5. PokeAPI uses Tackle=33, Scratch=10, Ember=52.
**Strategy:** Search for all `MoveIds = [` and `new BattleMove(` assignments. Create a `MoveConstants.cs` with named constants (`public const int Tackle = 33;`) to prevent future mismatches.

### Issue 2: No type effectiveness = flat damage
**Problem:** A Water Gun on a Charizard does the same damage as on a Blastoise.
**Strategy:** Create `TypeChart.cs` with a static 18x18 float array. Multiply into damage formula. The data is well-known and can be hardcoded — no API needed.

### Issue 3: Save file compatibility
**Problem:** Existing saves store move IDs from the old hardcoded registry. Loading them with the new DB-backed registry will show wrong moves.
**Strategy:** Add a save version field. On load, if version < current, remap old IDs → new IDs using a migration table, or regenerate moveset from learnset data at the Pokemon's current level.

### Issue 4: Evolution during multi-level jumps
**Problem:** If a Pokemon gains enough EXP to skip from level 14 to 18, does it evolve? Currently yes — `CheckEvolution()` runs after all level-ups and checks `Level >= MinLevel`. But it only returns one evolution.
**Strategy:** This is actually correct for single-stage evolution (Charmander→Charmeleon at 16 still triggers at 18). For double-evolution skips (14→36 skipping both 16 and 32), would need to check at each level in the loop. Current implementation handles the common case.

---

## 7. New Architecture & Quick Wins

### Architecture: Unified Data Layer
All game data now flows through `GameDataDb.cs` — a single static class with a single SQLite connection. This is the foundation for:
- **TM/HM teaching** — query `learnsets WHERE method = 'machine'`
- **Move tutors** — query `learnsets WHERE method = 'tutor'`
- **Egg moves** — query `learnsets WHERE method = 'egg'`
- **Pokedex** — query `species` table directly
- **Evolution stones** — query `evolutions WHERE trigger = 'use-item'`

### Quick Win 1: Type chart (2-3 hours)
Hardcode the 18x18 effectiveness matrix. Multiply into `CalculateDamage()`. Immediate gameplay impact — battles become strategic.

### Quick Win 2: STAB bonus (30 minutes)
In `CalculateDamage()`, check if `move.Type == attacker.Type1 || move.Type == attacker.Type2`. If so, multiply damage by 1.5. Requires passing species type info to the damage function.

### Quick Win 3: Real damage formula (1 hour)
Replace `(power * level / 5 + 2) / 3` with the real Gen V formula using Attack/SpAttack vs Defense/SpDefense. `BattlePokemon` already has these stats via `Source`.

### Quick Win 4: Move constants file (30 minutes)
Create `MoveConstants.cs` with `public const int Tackle = 33; public const int Scratch = 10;` etc. Fix all hardcoded move ID references. Prevents future ID mismatches.

### Quick Win 5: Catch rate integration (1 hour)
`SpeciesData.CatchRate` is already in the DB. Wire it into a `CatchCalculator` that uses the standard formula: `catchRate * ballModifier * statusModifier * (3*maxHP - 2*currentHP) / (3*maxHP)`.

---

## File Inventory

### New Files
| File | Purpose |
|------|---------|
| `tools/seed-gamedata.mjs` | Species seeder (Node.js + better-sqlite3) |
| `tools/fetch-gamedata.py` | PokeAPI batch fetcher (Python + requests + tqdm) |
| `tools/package.json` | Tool dependencies |
| `src/PokemonGreen.Core/Data/GameDataDb.cs` | SQLite read-only accessor |
| `src/PokemonGreen.Core/Data/EvolutionData.cs` | Evolution record type |
| `src/PokemonGreen.Core.Tests/Data/GameDataDbTests.cs` | 88 data + pipeline tests |
| `src/PokemonGreen.Assets/Data/gamedata.db` | SQLite database (species, moves, learnsets, evolutions) |

### Modified Files
| File | Change |
|------|--------|
| `SpeciesRegistry.cs` | Rewritten as thin delegate to GameDataDb |
| `MoveRegistry.cs` | Rewritten to delegate to GameDataDb (was 12 hardcoded moves) |
| `MoveData.cs` | Added Priority property |
| `PartyPokemon.cs` | Moveset generation, move learning, evolution detection/execution, LevelUpResult |
| `BattleTurnManager.cs` | Post-victory message chain: EXP → level → moves → evolution |
| `PokemonGreen.Assets.csproj` | gamedata.db as Content output |
| `PokemonGreen.Core.Tests.csproj` | gamedata.db reference + Content copy |
