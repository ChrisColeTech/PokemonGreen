# 13 - Pokemon Encounter System Design

**Date:** 2026-02-15
**Depends on:** Doc 10 (Old Engine Architecture), Doc 12 (Battle BG & Encounter Handoff)
**Status:** Design

---

## Table of Contents

1. [Overview](#1-overview)
2. [Old Engine Analysis](#2-old-engine-analysis)
3. [Encounter Table Data Structure](#3-encounter-table-data-structure)
4. [Encounter Rate Calculation](#4-encounter-rate-calculation)
5. [Level Scaling Formula](#5-level-scaling-formula)
6. [Type-Based Pokemon Categorization](#6-type-based-pokemon-categorization)
7. [Map/Route Encounter Definitions](#7-maproute-encounter-definitions)
8. [Progress-Gating System](#8-progress-gating-system)
9. [EncounterRegistry Design](#9-encounterregistry-design)
10. [Integration with Current Map/Tile System](#10-integration-with-current-maptile-system)
11. [Code Changes Required](#11-code-changes-required)
12. [Example Data Files](#12-example-data-files)
13. [Future Extensions](#13-future-extensions)

---

## 1. Overview

The encounter system determines which wild Pokemon appear when the player steps on an encounter tile. It must answer five questions for every encounter:

1. **Should an encounter happen?** (encounter rate per step)
2. **What species appears?** (encounter table weighted selection)
3. **What level is it?** (level range + scaling)
4. **What tile type triggered it?** (grass, cave, water, etc.)
5. **Is the player allowed to see this Pokemon yet?** (progress gating)

### Design Principles

- **Data-driven**: All encounter definitions live in JSON files, not hardcoded in C#.
- **Map-scoped**: Each map defines its own encounter tables. No global tables.
- **Tile-type-keyed**: A single map can have different encounter pools for grass, cave floor, water, etc.
- **Progress-aware**: Encounter tables can be unlocked, replaced, or augmented based on badges and story flags.
- **Extensible**: Adding a new encounter type (e.g., "swarm", "time-of-day variant") requires only data changes and a small registry addition, not architectural changes.

---

## 2. Old Engine Analysis

### How the Old Engine Worked

The Kermalis PokemonGameEngine (at `D:\Projects\PokemonGameEngine\`) implements encounters through these key files:

| File | Purpose |
|---|---|
| `World/EncounterMaker.cs` | Core encounter logic: rate checks, species selection, ability modifiers |
| `World/Data/EncounterData.cs` | Runtime data structures: `EncounterTable`, `EncounterGroups` |
| `World/WorldConstants.cs` | `EncounterType` enum, `BlocksetBlockBehavior` enum |
| `World/Maps/Map.cs` | Maps store `EncounterGroups` loaded when map becomes current |
| `MapEditor/Core/Encounter.cs` | Editor-side JSON serialization of encounter tables |
| `Core/BattleMaker.cs` | Creates battle instances from encounter results |
| `Assets/Encounter/*.json` | JSON encounter table definitions |

### Old Engine Data Model

```
Map
  -> EncounterGroups (loaded when map is current)
       -> EncounterGroup[] (array of type+table pairs)
            -> EncounterType (Default, Surf, DarkGrass, Cave, etc.)
            -> EncounterTable (loaded by ID from file)
                 -> ChanceOfPhenomenon (byte, 0-255 = encounter rate)
                 -> Encounter[] (list of species entries)
                      -> Chance (byte, relative weight)
                      -> MinLevel (byte)
                      -> MaxLevel (byte)
                      -> Species (enum)
                      -> Form (enum)
```

### Old Engine Encounter Flow

1. **Tile Check** (`TryGetEncounterType`): When the player moves, the block behavior is checked:
   - `Grass_Encounter`, `Cave_Encounter`, `AllowElevationChange_Cave_Encounter` -> `EncounterType.Default`
   - `Grass_SpecialEncounter` -> `EncounterType.DarkGrass`
   - `Surf` -> `EncounterType.Surf`

2. **Table Lookup** (`map.Encounters.GetEncounterTable(type)`): The map's `EncounterGroups` is searched for a group matching the encounter type. If none exists, no encounter.

3. **Rate Roll** (`ChanceOfPhenomenon / 255`): A random roll determines if an encounter occurs this step. The chance is a byte value where 255 = 100%. Example values from actual data:
   - `26` = ~10.2% per step (standard grass)
   - `13` = ~5.1% per step (cave)
   - `51` = ~20% per step (headbutt/honey trees)
   - `102` = ~40% per step (rare encounter phenomenon)
   - `230` = ~90.2% per step (honey tree -- nearly guaranteed)

4. **Rate Modifiers** (`GetAffectedChance`): Lead Pokemon ability and item modify the rate:
   - ArenaTrap / Illuminate / NoGuard: chance x2
   - QuickFeet / Stench / WhiteSmoke: chance /2
   - SandVeil (in sandstorm) / SnowCloak (in hail): chance /2
   - CleanseTag: chance x2/3
   - Biking: chance x4/5

5. **Species Selection** (`RollEncounter`): Weighted random from the table. Each entry has a `Chance` byte value. The combined chance is the sum of all entries' Chance values. A random number 1..combined is rolled and iterated through entries.

6. **Ability-Affected Selection** (`GetAffectedEncounter`): 50% chance for lead abilities to bias selection:
   - MagnetPull: prefer Steel-type encounters
   - Static: prefer Electric-type encounters

7. **Level Determination** (`GetAffectedLevel`): Random between MinLevel and MaxLevel, with ability override:
   - Hustle / Pressure / VitalSpirit: 50% chance to force MaxLevel

8. **Encounter Cancellation** (`ShouldCancelEncounter`): Intimidate / KeenEye can cancel if lead is 5+ levels higher (50% chance).

9. **Form, Gender, Nature Modifiers**: Various abilities affect the encountered Pokemon's form, gender, and nature (CuteCharm, Synchronize, etc.).

10. **Battle Format**: DarkGrass triggers double battle (2 wild Pokemon); all others are single.

### Old Engine Encounter Type Enum

```csharp
enum EncounterType : byte
{
    Default,        // Standard grass/cave walking
    Surf,           // Surfing on water
    SuperRod,       // Fishing with Super Rod
    DarkGrass,      // Special dark grass (double battle)
    RareDefault,    // Rustling grass / dust clouds (phenomenon)
    RareSurf,       // Rippling water
    RareSuperRod,   // Rippling water fishing
    HeadbuttTree,   // Headbutt on special trees
    HoneyTree       // Honey applied to trees
}
```

### Key Observations from Old Engine

1. **No level scaling**: Levels are fixed per encounter entry. No adjustment based on player progress.
2. **No progress gating**: All encounters on a map are always available. Gating is done by map access (the player simply cannot reach late-game maps early).
3. **No type categorization for pools**: Pokemon types are only used for MagnetPull/Static bias, not for organizing encounter pools.
4. **Simple rate model**: Single byte per table, no per-tile-type rate variation within a map.
5. **Per-map encounter data**: Each map has its own set of encounter groups. This is the correct granularity.

---

## 3. Encounter Table Data Structure

### EncounterEntry

The atomic unit of encounter data. Represents a single species that can be encountered.

```csharp
public record EncounterEntry(
    string SpeciesId,       // e.g., "Pidgey", "Geodude"
    int MinLevel,           // Minimum level before scaling
    int MaxLevel,           // Maximum level before scaling
    int Weight,             // Relative probability weight (1-255)
    string? FormId = null,  // Optional form (e.g., "Alolan", "Galarian")
    string[]? RequiredFlags = null,  // Story flags that must be set
    int RequiredBadges = 0  // Minimum badge count to encounter
);
```

### EncounterTable

A named collection of encounter entries for a specific encounter type on a specific map.

```csharp
public class EncounterTable
{
    public string Id { get; set; }                // Unique ID (e.g., "route1_grass")
    public string MapId { get; set; }             // Map this table belongs to
    public string EncounterType { get; set; }     // "tall_grass", "cave_floor", "surf", etc.
    public int BaseEncounterRate { get; set; }     // Base chance per step (0-255)
    public EncounterEntry[] Entries { get; set; }  // Species pool
}
```

### EncounterType Values

Expanding from the old engine's types to support the new engine's tile-based system:

| EncounterType String | Tile IDs | Old Engine Equivalent | Battle Format |
|---|---|---|---|
| `tall_grass` | 72 (TallGrass) | Default (Grass_Encounter) | Single |
| `rare_grass` | 73 (RareGrass) | RareDefault | Single |
| `dark_grass` | 74 (DarkGrass) | DarkGrass | Double |
| `cave_floor` | 75 (CaveEncounter) | Default (Cave_Encounter) | Single |
| `water_surface` | 76 (WaterEncounter) | Surf | Single |
| `surf` | 77 (SurfEncounter) | Surf | Single |
| `fishing` | 78 (FishingSpot) | SuperRod | Single |
| `headbutt` | 79 (Headbutt) | HeadbuttTree | Single |
| `fire_terrain` | 121 (Flames) | N/A (new) | Single |

### Encounter Rate Interpretation

The `BaseEncounterRate` is a value 0-255 interpreted as a probability per step:

| Value | Probability | Use Case |
|---|---|---|
| 0 | 0% (disabled) | Table exists but is inactive |
| 13 | ~5% | Low-frequency areas (deep caves) |
| 26 | ~10% | Standard grass/route encounters |
| 40 | ~16% | Dense encounter areas |
| 51 | ~20% | Headbutt trees, special encounters |
| 102 | ~40% | Rare phenomenon triggers |
| 230 | ~90% | Honey tree (nearly guaranteed) |

---

## 4. Encounter Rate Calculation

### Base Formula

```
EffectiveRate = BaseEncounterRate / 255.0
ShouldEncounter = Random.NextDouble() < EffectiveRate
```

### Rate Modifiers (Future Implementation)

The following modifiers stack multiplicatively on the base rate, matching the old engine's approach:

```csharp
public static float CalculateEncounterRate(
    int baseRate,
    string? leadAbility,
    string? leadHeldItem,
    bool isBiking,
    string? currentWeather)
{
    float rate = baseRate / 255f;

    // Lead Pokemon ability modifiers
    rate *= leadAbility switch
    {
        "ArenaTrap" or "Illuminate" or "NoGuard" => 2.0f,
        "QuickFeet" or "Stench" or "WhiteSmoke" => 0.5f,
        "SandVeil" when currentWeather == "Sandstorm" => 0.5f,
        "SnowCloak" when currentWeather == "Hail" => 0.5f,
        _ => 1.0f
    };

    // Held item modifiers
    rate *= leadHeldItem switch
    {
        "CleanseTag" => 0.667f,  // Reduce by 1/3
        _ => 1.0f
    };

    // Biking reduces encounters by 20%
    if (isBiking)
        rate *= 0.8f;

    return Math.Clamp(rate, 0f, 1f);
}
```

### Step Counter Approach (Alternative to Pure Random)

To prevent frustrating streaks of no encounters or too-frequent encounters, use a step counter with variance:

```csharp
// Instead of pure random each step:
int stepsUntilEncounter = CalculateStepsUntilEncounter(baseRate);

int CalculateStepsUntilEncounter(int baseRate)
{
    if (baseRate <= 0) return int.MaxValue;

    float avgSteps = 255f / baseRate;  // e.g., rate=26 -> ~9.8 steps
    float minSteps = avgSteps * 0.5f;
    float maxSteps = avgSteps * 1.5f;
    return Random.Next((int)minSteps, (int)maxSteps + 1);
}
```

This is optional but recommended for player experience. The current `GameWorld.cs` uses `1 in EncounterChance (10)` per step, which is simpler but can produce long droughts or rapid back-to-back encounters.

---

## 5. Level Scaling Formula

The old engine uses fixed level ranges with no scaling. The new system adds optional scaling based on player progress while preserving the authored level range as the baseline.

### Scaling Factors

```csharp
public static int CalculateEncounterLevel(
    int minLevel,
    int maxLevel,
    int partyHighestLevel,
    int badgeCount,
    float progressMultiplier = 0.0f)  // 0.0 = no scaling, 1.0 = full scaling
{
    // 1. Base level: random within authored range
    int baseLevel = Random.Next(minLevel, maxLevel + 1);

    if (progressMultiplier <= 0f)
        return baseLevel;

    // 2. Calculate progress-based adjustment
    //    Badge scaling: each badge allows +2 levels above authored max
    int badgeBonus = badgeCount * 2;

    // 3. Party-relative scaling: encounters scale toward party level
    //    but never exceed authored max + badge bonus
    int scaledLevel = baseLevel;
    if (partyHighestLevel > maxLevel)
    {
        int partyDelta = partyHighestLevel - maxLevel;
        int adjustment = (int)(partyDelta * progressMultiplier * 0.5f);
        scaledLevel = baseLevel + adjustment;
    }

    // 4. Clamp: never below authored min, never above authored max + badge bonus
    int ceiling = maxLevel + badgeBonus;
    return Math.Clamp(scaledLevel, minLevel, ceiling);
}
```

### Scaling Behavior Examples

For an encounter entry with `MinLevel=5, MaxLevel=10`:

| Badges | Party Lv | progressMultiplier | Possible Range | Notes |
|---|---|---|---|---|
| 0 | 8 | 0.0 | 5-10 | No scaling, pure authored range |
| 0 | 20 | 0.5 | 5-10 | No scaling, 0 badges = 0 ceiling bonus |
| 2 | 20 | 0.5 | 5-14 | 2 badges = +4 ceiling, party delta scaled |
| 4 | 30 | 0.5 | 5-18 | 4 badges = +8 ceiling |
| 8 | 60 | 0.5 | 5-26 | 8 badges = +16 ceiling |
| 8 | 60 | 0.0 | 5-10 | Scaling disabled = pure authored range |

### Per-Map Scaling Configuration

Each map's encounter definition can set its own `progressMultiplier`:

- **Early routes** (Route 1, 2): `0.0` -- Players should experience these at authored levels
- **Mid-game routes**: `0.3` -- Mild scaling so revisits aren't trivial
- **Late-game areas**: `0.5` -- Moderate scaling
- **Post-game areas**: `0.8` -- Strong scaling, always challenging
- **Level-locked areas** (e.g., Victory Road): `0.0` with high authored levels

---

## 6. Type-Based Pokemon Categorization

### Purpose

Type categorization serves two purposes:
1. **Encounter pool organization**: Encounter tables for fire-themed areas should have mostly Fire-type Pokemon.
2. **Ability-biased selection**: MagnetPull (Steel), Static (Electric) bias the encounter toward matching types.

### PokemonType Enum

```csharp
public enum PokemonType
{
    Normal, Fire, Water, Electric, Grass,
    Ice, Fighting, Poison, Ground, Flying,
    Psychic, Bug, Rock, Ghost, Dragon,
    Dark, Steel, Fairy
}
```

### Species Data Structure

Each Pokemon species has associated type data used by the encounter system:

```csharp
public record SpeciesData(
    string Id,              // "Pidgey"
    string Name,            // "Pidgey"
    PokemonType Type1,      // Flying
    PokemonType? Type2,     // Normal
    int BaseHP,
    int BaseAtk,
    int BaseDef,
    int BaseSpAtk,
    int BaseSpDef,
    int BaseSpeed
);
```

### Type-Biased Selection

When the lead Pokemon has an ability that biases encounters by type, the encounter system filters the table:

```csharp
public static EncounterEntry SelectEncounter(
    EncounterTable table,
    PokemonType? biasType,
    float biasChance = 0.5f)  // 50% chance to activate bias
{
    var entries = table.GetFilteredEntries();  // Apply progress gating first
    if (entries.Length == 0) return null;

    // Check ability bias
    if (biasType.HasValue && Random.NextDouble() < biasChance)
    {
        var typeEntries = entries
            .Where(e => SpeciesRegistry.HasType(e.SpeciesId, biasType.Value))
            .ToArray();

        if (typeEntries.Length > 0)
            return WeightedSelect(typeEntries);
    }

    return WeightedSelect(entries);
}
```

### Thematic Encounter Pools

When designing encounter tables, Pokemon should be grouped thematically by tile type:

| Tile Type | Preferred Types | Example Pokemon |
|---|---|---|
| `tall_grass` | Normal, Bug, Grass, Flying | Pidgey, Caterpie, Oddish, Rattata |
| `dark_grass` | Grass, Bug, Poison | Tangela, Beedrill, Gloom |
| `cave_floor` | Rock, Ground, Steel, Dark | Geodude, Zubat, Aron, Diglett |
| `water_surface` / `surf` | Water | Tentacool, Wingull, Psyduck |
| `fishing` | Water | Magikarp, Goldeen, Poliwag |
| `fire_terrain` (Flames) | Fire, Ground, Rock | Slugma, Numel, Houndour |
| `headbutt` | Bug, Flying, Normal | Pineco, Heracross, Aipom |

This is a guideline for content design, not enforced by the system. Any species can appear in any encounter type.

---

## 7. Map/Route Encounter Definitions

### JSON File Format

Each map has a corresponding encounter definition file. Encounter data lives in `Content/Data/Encounters/`:

```
Content/
  Data/
    Encounters/
      route_1.json
      route_2.json
      viridian_forest.json
      mt_moon.json
      ...
```

### File Structure

```json
{
  "mapId": "route_1",
  "progressMultiplier": 0.0,
  "encounterGroups": [
    {
      "encounterType": "tall_grass",
      "baseEncounterRate": 26,
      "entries": [
        {
          "speciesId": "Pidgey",
          "minLevel": 3,
          "maxLevel": 5,
          "weight": 40
        },
        {
          "speciesId": "Rattata",
          "minLevel": 2,
          "maxLevel": 4,
          "weight": 35
        },
        {
          "speciesId": "Caterpie",
          "minLevel": 3,
          "maxLevel": 5,
          "weight": 15
        },
        {
          "speciesId": "Pikachu",
          "minLevel": 4,
          "maxLevel": 6,
          "weight": 5,
          "requiredBadges": 0
        },
        {
          "speciesId": "Bulbasaur",
          "minLevel": 5,
          "maxLevel": 7,
          "weight": 5,
          "requiredBadges": 3,
          "requiredFlags": ["starter_quest_complete"]
        }
      ]
    },
    {
      "encounterType": "rare_grass",
      "baseEncounterRate": 102,
      "entries": [
        {
          "speciesId": "Audino",
          "minLevel": 4,
          "maxLevel": 7,
          "weight": 40
        },
        {
          "speciesId": "Chansey",
          "minLevel": 5,
          "maxLevel": 8,
          "weight": 10,
          "requiredBadges": 4
        }
      ]
    }
  ]
}
```

### Complex Example: Multi-Type Area (Cave with Water)

```json
{
  "mapId": "mt_moon",
  "progressMultiplier": 0.2,
  "encounterGroups": [
    {
      "encounterType": "cave_floor",
      "baseEncounterRate": 13,
      "entries": [
        { "speciesId": "Zubat", "minLevel": 8, "maxLevel": 12, "weight": 40 },
        { "speciesId": "Geodude", "minLevel": 8, "maxLevel": 11, "weight": 30 },
        { "speciesId": "Paras", "minLevel": 9, "maxLevel": 12, "weight": 15 },
        { "speciesId": "Clefairy", "minLevel": 10, "maxLevel": 13, "weight": 10 },
        { "speciesId": "Clefable", "minLevel": 14, "maxLevel": 16, "weight": 5, "requiredBadges": 2 }
      ]
    },
    {
      "encounterType": "surf",
      "baseEncounterRate": 26,
      "entries": [
        { "speciesId": "Psyduck", "minLevel": 10, "maxLevel": 15, "weight": 50 },
        { "speciesId": "Goldeen", "minLevel": 10, "maxLevel": 14, "weight": 30 },
        { "speciesId": "Seel", "minLevel": 12, "maxLevel": 16, "weight": 20 }
      ]
    }
  ]
}
```

### Tile-to-EncounterType Mapping

The mapping from tile overlay behavior to encounter type string is defined in the resolver. This extends the existing `BattleBackgroundResolver` pattern:

| Tile OverlayBehavior | EncounterType String |
|---|---|
| `wild_encounter` | `tall_grass` |
| `rare_encounter` | `rare_grass` |
| `double_encounter` | `dark_grass` |
| `cave_encounter` | `cave_floor` |
| `water_encounter` | `water_surface` |
| `surf_encounter` | `surf` |
| `fishing` | `fishing` |
| `headbutt` | `headbutt` |
| `fire_encounter` | `fire_terrain` |

---

## 8. Progress-Gating System

### Overview

Progress gating controls which encounter entries are available to the player. It operates at the entry level, not the table level -- meaning a single table can have some entries available from the start and others unlocked later.

### Badge Gating

Each `EncounterEntry` has an optional `RequiredBadges` field (default 0). An entry is only eligible if:

```csharp
bool IsAvailable(EncounterEntry entry, int playerBadges, HashSet<string> playerFlags)
{
    if (playerBadges < entry.RequiredBadges)
        return false;

    if (entry.RequiredFlags != null)
    {
        foreach (string flag in entry.RequiredFlags)
        {
            if (!playerFlags.Contains(flag))
                return false;
        }
    }

    return true;
}
```

### Flag Gating

Story flags (e.g., `"defeated_team_rocket"`, `"obtained_national_dex"`) enable encounter entries that represent story-locked Pokemon. Flags are string-based for extensibility.

### PlayerProgress Class

A new class to track progress state consumed by the encounter system:

```csharp
public class PlayerProgress
{
    public int BadgeCount { get; set; }
    public int HighestPartyLevel { get; set; }
    public int AveragePartyLevel { get; set; }
    public HashSet<string> StoryFlags { get; } = new();

    // Computed from party
    public void UpdateFromParty(Party party)
    {
        if (party.Count == 0) return;

        HighestPartyLevel = party.Max(p => p.Level);
        AveragePartyLevel = (int)party.Average(p => p.Level);
    }
}
```

### Gating Design Patterns

| Pattern | RequiredBadges | RequiredFlags | Use Case |
|---|---|---|---|
| Always available | 0 | null | Common Pokemon on early routes |
| Badge-gated | 3 | null | Evolved forms appear after 3 badges |
| Flag-gated | 0 | `["event_x"]` | Event Pokemon after story event |
| Combined | 6 | `["national_dex"]` | Post-game Pokemon |

### Fallback Behavior

If all entries in a table are gated and none are available, the encounter simply does not trigger (the rate roll passes, but no species can be selected). This prevents errors and lets early-game areas have placeholder tables for future content.

---

## 9. EncounterRegistry Design

### Architecture

The `EncounterRegistry` is the central service that loads, caches, and queries encounter data. It is data-driven and extensible.

```csharp
public static class EncounterRegistry
{
    // Cache: mapId -> list of encounter tables for that map
    private static readonly Dictionary<string, MapEncounterData> _cache = new();

    // Loaded encounter data for a map
    private static MapEncounterData? _currentMapEncounters;

    /// <summary>
    /// Load encounter data for a map. Called when the map becomes current.
    /// </summary>
    public static void LoadForMap(string mapId)
    {
        if (_cache.TryGetValue(mapId, out var cached))
        {
            _currentMapEncounters = cached;
            return;
        }

        string path = Path.Combine("Content", "Data", "Encounters", $"{mapId}.json");
        if (!File.Exists(path))
        {
            _currentMapEncounters = null;
            return;
        }

        var data = JsonSerializer.Deserialize<MapEncounterData>(
            File.ReadAllText(path));
        _cache[mapId] = data;
        _currentMapEncounters = data;
    }

    /// <summary>
    /// Get the encounter table for the given encounter type on the current map.
    /// Returns null if the map has no encounters for that type.
    /// </summary>
    public static EncounterTable? GetTable(string encounterType)
    {
        if (_currentMapEncounters == null)
            return null;

        foreach (var group in _currentMapEncounters.EncounterGroups)
        {
            if (group.EncounterType == encounterType)
                return group;
        }
        return null;
    }

    /// <summary>
    /// Get the progress multiplier for the current map.
    /// </summary>
    public static float GetProgressMultiplier()
    {
        return _currentMapEncounters?.ProgressMultiplier ?? 0f;
    }

    /// <summary>
    /// Roll for a wild encounter on the current tile.
    /// Returns null if no encounter should happen.
    /// </summary>
    public static WildEncounterResult? TryEncounter(
        string encounterType,
        PlayerProgress progress,
        string? leadAbility = null,
        string? leadItem = null,
        bool isBiking = false,
        string? weather = null)
    {
        var table = GetTable(encounterType);
        if (table == null)
            return null;

        // 1. Rate check
        float rate = CalculateEncounterRate(
            table.BaseEncounterRate, leadAbility, leadItem, isBiking, weather);
        if (Random.NextDouble() >= rate)
            return null;

        // 2. Filter entries by progress
        var available = table.Entries
            .Where(e => IsAvailable(e, progress.BadgeCount, progress.StoryFlags))
            .ToArray();
        if (available.Length == 0)
            return null;

        // 3. Select species (with optional type bias)
        PokemonType? biasType = GetAbilityBiasType(leadAbility);
        var entry = SelectEncounter(available, biasType);
        if (entry == null)
            return null;

        // 4. Calculate level
        int level = CalculateEncounterLevel(
            entry.MinLevel,
            entry.MaxLevel,
            progress.HighestPartyLevel,
            progress.BadgeCount,
            GetProgressMultiplier());

        // 5. Build result
        return new WildEncounterResult(
            entry.SpeciesId,
            entry.FormId,
            level,
            encounterType);
    }

    /// <summary>
    /// Clear the cache (e.g., when starting a new game).
    /// </summary>
    public static void ClearCache()
    {
        _cache.Clear();
        _currentMapEncounters = null;
    }
}
```

### WildEncounterResult

```csharp
public record WildEncounterResult(
    string SpeciesId,
    string? FormId,
    int Level,
    string EncounterType
);
```

### MapEncounterData (JSON Deserialization Target)

```csharp
public class MapEncounterData
{
    public string MapId { get; set; } = "";
    public float ProgressMultiplier { get; set; }
    public EncounterTable[] EncounterGroups { get; set; } = [];
}
```

### EncounterTypeResolver

Maps tile overlay behavior strings to encounter type strings. Replaces the scattered switch statements:

```csharp
public static class EncounterTypeResolver
{
    private static readonly Dictionary<string, string> _map = new()
    {
        ["wild_encounter"] = "tall_grass",
        ["rare_encounter"] = "rare_grass",
        ["double_encounter"] = "dark_grass",
        ["cave_encounter"] = "cave_floor",
        ["water_encounter"] = "water_surface",
        ["surf_encounter"] = "surf",
        ["fishing"] = "fishing",
        ["headbutt"] = "headbutt",
        ["fire_encounter"] = "fire_terrain",
    };

    public static string? FromOverlayBehavior(string? behavior)
    {
        if (behavior == null) return null;
        return _map.TryGetValue(behavior, out var type) ? type : null;
    }

    /// <summary>
    /// Get the battle format for the encounter type.
    /// </summary>
    public static BattleFormat GetBattleFormat(string encounterType)
    {
        return encounterType switch
        {
            "dark_grass" => BattleFormat.Double,
            _ => BattleFormat.Single
        };
    }
}
```

---

## 10. Integration with Current Map/Tile System

### Current Encounter Detection Flow

From `GameWorld.cs` (current implementation):

```
Player moves -> TileX/TileY changes
  -> IsEncounterTile(x, y) checks TileCategory.Encounter
  -> If encounter: _encounterCheckPending = true
  -> When player reaches sub-tile hitbox center:
     -> 1-in-10 random roll
     -> BeginBattleTransition() -> enters battle state
```

### Proposed Modified Flow

The encounter detection stays in `GameWorld.cs` but delegates species selection to `EncounterRegistry`:

```
Player moves -> TileX/TileY changes
  -> IsEncounterTile(x, y) checks TileCategory.Encounter (unchanged)
  -> If encounter: _encounterCheckPending = true (unchanged)
  -> When player reaches sub-tile hitbox center:
     -> Get tile's OverlayBehavior
     -> EncounterTypeResolver.FromOverlayBehavior(behavior)
     -> EncounterRegistry.TryEncounter(encounterType, progress, ...)
     -> If result != null: BeginBattleTransition(result)
     -> If result == null: no encounter this step (table empty or rate failed)
```

### Key Integration Points

1. **`GameWorld.cs`** -- Replace the hardcoded `_encounterRandom.Next(EncounterChance) == 0` with `EncounterRegistry.TryEncounter()`.

2. **`GameWorld.LoadMap()`** -- Call `EncounterRegistry.LoadForMap(mapDef.Id)` when loading a new map.

3. **`Game1.EnterBattle()`** -- Receive the `WildEncounterResult` and use it to create the wild Pokemon (instead of `BattlePokemon.CreateTestFoe()`).

4. **`BattleBackgroundResolver`** -- Continue to map encounter behavior to battle backgrounds, no changes needed.

5. **`TileRegistry`** -- No changes. The existing tile definitions with `TileCategory.Encounter` and `OverlayBehavior` strings are the foundation.

### File Locations in New Engine

```
src/PokemonGreen.Core/
  Encounters/                    # NEW DIRECTORY
    EncounterEntry.cs            # Data record
    EncounterTable.cs            # Table with entries
    MapEncounterData.cs          # Per-map root object
    EncounterRegistry.cs         # Central service
    EncounterTypeResolver.cs     # Overlay behavior -> encounter type
    WildEncounterResult.cs       # Result of TryEncounter
    PlayerProgress.cs            # Player state for gating/scaling
  Pokemon/
    PokemonType.cs               # Type enum (new)
    SpeciesData.cs               # Species base data (new)
    SpeciesRegistry.cs           # Species lookup (new)

Content/
  Data/
    Encounters/                  # JSON encounter files
      route_1.json
      test_map_center.json
      test_map_b.json
      ...
    Species/                     # JSON species data
      species.json               # All species base data
```

---

## 11. Code Changes Required

### New Files

| File | Purpose |
|---|---|
| `Core/Encounters/EncounterEntry.cs` | `EncounterEntry` record |
| `Core/Encounters/EncounterTable.cs` | `EncounterTable` class |
| `Core/Encounters/MapEncounterData.cs` | `MapEncounterData` deserialization class |
| `Core/Encounters/EncounterRegistry.cs` | Central encounter service with `TryEncounter()` |
| `Core/Encounters/EncounterTypeResolver.cs` | Maps overlay behaviors to encounter type strings |
| `Core/Encounters/WildEncounterResult.cs` | Result record |
| `Core/Encounters/PlayerProgress.cs` | Player progress tracker |
| `Core/Pokemon/PokemonType.cs` | Type enum |
| `Core/Pokemon/SpeciesData.cs` | Species base data record |
| `Core/Pokemon/SpeciesRegistry.cs` | Species data loader/lookup |
| `Content/Data/Encounters/*.json` | Per-map encounter data files |
| `Content/Data/Species/species.json` | Species base data |

### Modified Files

| File | Change |
|---|---|
| `GameWorld.cs` | Replace hardcoded encounter chance with `EncounterRegistry.TryEncounter()`. Call `EncounterRegistry.LoadForMap()` in `LoadMap()`. Store `WildEncounterResult` for `EnterBattle()`. |
| `Game1.cs` | Pass `WildEncounterResult` to `EnterBattle()`. Create wild `BattlePokemon` from result instead of `CreateTestFoe()`. |
| `BattlePokemon.cs` | Add factory method `CreateFromEncounter(WildEncounterResult result)` or extend `CreateTestFoe` with parameters. |
| `Party.cs` | Add methods to compute highest/average level for `PlayerProgress.UpdateFromParty()`. |

### Minimal Implementation Order

1. **Phase 1 -- Data Layer**: Create `EncounterEntry`, `EncounterTable`, `MapEncounterData`, `WildEncounterResult` records/classes. Create `EncounterRegistry` with `LoadForMap()` and `TryEncounter()`. Create `EncounterTypeResolver`.

2. **Phase 2 -- Integration**: Modify `GameWorld.cs` to use `EncounterRegistry`. Create test JSON encounter files for existing maps.

3. **Phase 3 -- Species Data**: Create `PokemonType`, `SpeciesData`, `SpeciesRegistry`. Wire into `BattlePokemon` creation.

4. **Phase 4 -- Progress Gating**: Create `PlayerProgress`. Add badge/flag tracking. Wire gating into `EncounterRegistry.TryEncounter()`.

5. **Phase 5 -- Level Scaling**: Implement `CalculateEncounterLevel()` with progress multiplier.

6. **Phase 6 -- Ability Modifiers**: Implement rate and selection modifiers based on lead Pokemon ability.

### GameWorld.cs Specific Changes

Current code in `GameWorld.cs` (around line 199):

```csharp
if (_encounterRandom.Next(EncounterChance) == 0)
{
    BeginBattleTransition(_pendingEncTileX, _pendingEncTileY);
    return;
}
```

Replace with:

```csharp
string? behavior = GetEncounterBehavior(_pendingEncTileX, _pendingEncTileY);
string? encounterType = EncounterTypeResolver.FromOverlayBehavior(behavior);
if (encounterType != null)
{
    var result = EncounterRegistry.TryEncounter(encounterType, _playerProgress);
    if (result != null)
    {
        _pendingEncounterResult = result;
        BeginBattleTransition(_pendingEncTileX, _pendingEncTileY);
        return;
    }
}
```

---

## 12. Example Data Files

### test_map_center.json (Starting Town -- No Encounters)

```json
{
  "mapId": "test_map_center",
  "progressMultiplier": 0.0,
  "encounterGroups": []
}
```

### test_map_a.json (Route with Grass)

```json
{
  "mapId": "test_map_a",
  "progressMultiplier": 0.0,
  "encounterGroups": [
    {
      "encounterType": "tall_grass",
      "baseEncounterRate": 26,
      "entries": [
        { "speciesId": "Pidgey", "minLevel": 3, "maxLevel": 5, "weight": 40 },
        { "speciesId": "Rattata", "minLevel": 2, "maxLevel": 4, "weight": 35 },
        { "speciesId": "Caterpie", "minLevel": 3, "maxLevel": 5, "weight": 15 },
        { "speciesId": "Pikachu", "minLevel": 4, "maxLevel": 6, "weight": 5 },
        { "speciesId": "Mankey", "minLevel": 4, "maxLevel": 6, "weight": 5 }
      ]
    }
  ]
}
```

### test_map_b.json (Fire Area)

```json
{
  "mapId": "test_map_b",
  "progressMultiplier": 0.3,
  "encounterGroups": [
    {
      "encounterType": "fire_terrain",
      "baseEncounterRate": 26,
      "entries": [
        { "speciesId": "Slugma", "minLevel": 12, "maxLevel": 16, "weight": 30 },
        { "speciesId": "Numel", "minLevel": 14, "maxLevel": 18, "weight": 25 },
        { "speciesId": "Houndour", "minLevel": 13, "maxLevel": 17, "weight": 20 },
        { "speciesId": "Torkoal", "minLevel": 16, "maxLevel": 20, "weight": 10 },
        { "speciesId": "Magby", "minLevel": 15, "maxLevel": 19, "weight": 10 },
        {
          "speciesId": "Magmar",
          "minLevel": 20,
          "maxLevel": 24,
          "weight": 5,
          "requiredBadges": 3
        }
      ]
    }
  ]
}
```

### test_map_d.json (Cave with Multiple Encounter Types)

```json
{
  "mapId": "test_map_d",
  "progressMultiplier": 0.2,
  "encounterGroups": [
    {
      "encounterType": "cave_floor",
      "baseEncounterRate": 13,
      "entries": [
        { "speciesId": "Zubat", "minLevel": 8, "maxLevel": 12, "weight": 35 },
        { "speciesId": "Geodude", "minLevel": 8, "maxLevel": 11, "weight": 30 },
        { "speciesId": "Machop", "minLevel": 9, "maxLevel": 12, "weight": 15 },
        { "speciesId": "Onix", "minLevel": 10, "maxLevel": 14, "weight": 10 },
        { "speciesId": "Aron", "minLevel": 11, "maxLevel": 15, "weight": 8 },
        {
          "speciesId": "Sableye",
          "minLevel": 14, "maxLevel": 18, "weight": 2,
          "requiredBadges": 2
        }
      ]
    },
    {
      "encounterType": "surf",
      "baseEncounterRate": 26,
      "entries": [
        { "speciesId": "Psyduck", "minLevel": 10, "maxLevel": 15, "weight": 50 },
        { "speciesId": "Goldeen", "minLevel": 10, "maxLevel": 14, "weight": 30 },
        { "speciesId": "Seel", "minLevel": 12, "maxLevel": 16, "weight": 20 }
      ]
    }
  ]
}
```

---

## 13. Future Extensions

### Time-of-Day Encounters

Add an optional `timeOfDay` field to `EncounterEntry`:

```json
{ "speciesId": "Hoothoot", "minLevel": 4, "maxLevel": 7, "weight": 30, "timeOfDay": ["night", "evening"] }
```

The system already has `DayNightCycle` in the new engine, so time data is available.

### Swarm Events

A global override that temporarily adds a species to all grass encounters on a specific route:

```json
{
  "swarmId": "pikachu_swarm_route_1",
  "mapId": "route_1",
  "encounterType": "tall_grass",
  "species": "Pikachu",
  "minLevel": 5,
  "maxLevel": 10,
  "weight": 60,
  "durationDays": 1
}
```

### Weather-Dependent Encounters

Add optional `requiredWeather` to entries:

```json
{ "speciesId": "Lotad", "minLevel": 5, "maxLevel": 8, "weight": 20, "requiredWeather": ["rain"] }
```

### Repel System

Repels suppress encounters below a certain level. The encounter system would filter entries where `CalculateEncounterLevel(entry) < repelLevel` before selection.

### Shiny Encounter Rate

The encounter system does not determine shininess -- that is a property of the Pokemon creation step. However, items like the Shiny Charm could be checked in `WildEncounterResult` creation.

### Encounter Chains

Track consecutive encounters of the same species on the same route. Higher chains increase shiny odds and force higher IVs. Requires a `_chainCount` and `_chainSpecies` field in the encounter system.

### Roaming Pokemon

Special encounters that move between routes each day. Implemented as a separate system that injects into `EncounterRegistry.TryEncounter()` with a pre-check:

```csharp
// Before normal encounter logic:
var roaming = RoamingRegistry.CheckForRoamer(currentMapId);
if (roaming != null && Random.NextDouble() < 0.02)  // 2% chance
    return roaming;
```

---

## Appendix A: Weight/Probability Reference

Given a table with entries of weights [40, 35, 15, 5, 5], the combined weight is 100:

| Species | Weight | Per-Encounter % | Per-Step % (rate=26) |
|---|---|---|---|
| Pidgey | 40 | 40.0% | 4.08% |
| Rattata | 35 | 35.0% | 3.57% |
| Caterpie | 15 | 15.0% | 1.53% |
| Pikachu | 5 | 5.0% | 0.51% |
| Mankey | 5 | 5.0% | 0.51% |

Per-step probability = (weight / combinedWeight) * (baseRate / 255)

## Appendix B: Old Engine vs New System Comparison

| Feature | Old Engine | New System |
|---|---|---|
| Data format | Binary (.bin) compiled from JSON | JSON loaded at runtime |
| Encounter rate | Per-table `ChanceOfPhenomenon` byte | Per-table `BaseEncounterRate` (same semantics) |
| Level determination | Fixed random in [min, max] | Scaled by progress + badges |
| Progress gating | None (map access only) | Per-entry badge + flag requirements |
| Type bias (abilities) | MagnetPull, Static (50% chance) | Same, preserved from old engine |
| Rate modifiers | 6 abilities + item + biking | Same, preserved from old engine |
| Encounter cancel | Intimidate, KeenEye | Same, preserved from old engine |
| Map association | Loaded in map binary data | Separate JSON per map |
| Species data | PBESpecies enum from battle engine | String-based species ID (flexible) |
| Form support | PBEForm enum | String-based form ID (flexible) |
| Encounter types | 9 enum values | String-based (extensible) |

## Appendix C: Migration Path from Test Code

The current `Game1.EnterBattle()` creates test Pokemon with `BattlePokemon.CreateTestFoe()`. The migration path:

1. Add `EncounterRegistry` and `EncounterTypeResolver`.
2. Create JSON files for existing test maps.
3. Store `WildEncounterResult` in `GameWorld` when encounter triggers.
4. Pass result to `Game1.EnterBattle()`.
5. Create `BattlePokemon.CreateFromEncounter(result)` that uses the species/level from the result.
6. Remove `CreateTestFoe()` and `CreateTestAlly()` (or keep as debug shortcuts).

The battle system itself (turn manager, moves, damage) does not need to change. Only the entry point -- what species/level the wild Pokemon is -- changes.
