# EXP and Battle Rewards Design

Design document for implementing experience points, level-up mechanics, stat recalculation, and evolution triggers in PokemonGreen, based on deep analysis of the old engine (`D:\Projects\PokemonGameEngine\`).

---

## Table of Contents

1. [Old Engine Architecture Summary](#1-old-engine-architecture-summary)
2. [EXP Formula](#2-exp-formula)
3. [EXP Yield Data Structure](#3-exp-yield-data-structure)
4. [Growth Rates and EXP Thresholds](#4-growth-rates-and-exp-thresholds)
5. [Level-Up Stat Recalculation](#5-level-up-stat-recalculation)
6. [Level-Up Move Learning](#6-level-up-move-learning)
7. [Evolution Triggers](#7-evolution-triggers)
8. [Friendship Adjustments](#8-friendship-adjustments)
9. [EXP Bar UI](#9-exp-bar-ui)
10. [Integration Points with Current BattleTurnManager](#10-integration-points-with-current-battleturnmanager)
11. [Code Changes Required](#11-code-changes-required)

---

## 1. Old Engine Architecture Summary

The old engine delegates all battle logic (damage, EXP gain, level-up, EV gain) to an external library: `PokemonBattleEngine.dll` (PBE). The game engine itself handles:

- **Display**: The GUI receives `PBEPkmnEXPChangedPacket` and `PBEPkmnLevelChangedPacket` events from PBE and updates the info bar display accordingly.
- **Post-battle sync**: `BattlePokemonParty.UpdateToParty()` copies EXP, Level, EVs, HP, status, and moves from PBE's `PBEBattlePokemon` back to the game's `PartyPokemon`.
- **Evolution check**: After `UpdateToParty()`, if the level changed, `Evolution.GetLevelUpEvolution()` is called and queued via `Evolution.AddPendingEvolution()`.
- **Evolution GUI**: `EvolutionGUI` handles the animation, species change, ability update, move learning, and Shedinja creation.

Since PokemonGreen does NOT use PBE and has its own `BattleTurnManager`, we must implement the EXP formula, stat recalculation, and evolution logic directly.

### Key Data Types in the Old Engine

| Old Engine Type | File | Purpose |
|----------------|------|---------|
| `PartyPokemon` | `Pkmn/PartyPokemon.cs` | Full Pokemon with EXP, Level, EVs, IVs, stats |
| `BaseStats` | `Pkmn/Pokedata/BaseStats.cs` | Per-species base stats, GrowthRate, BaseEXPYield |
| `LevelUpData` | `Pkmn/Pokedata/LevelUpData.cs` | Per-species level-up move list |
| `EvolutionData` | `Pkmn/Pokedata/EvolutionData.cs` | Per-species evolution methods and targets |
| `Evolution` | `Pkmn/Evolution.cs` | Evolution trigger logic (level-up, item, trade) |
| `Friendship` | `Pkmn/Friendship.cs` | Friendship adjustment per event |
| `EVs` / `IVs` | `Pkmn/EVsAndIVs.cs` | Effort Values and Individual Values |

---

## 2. EXP Formula

### Gen V EXP Gain Formula (Scaled)

The old engine uses PBE which implements the **Gen V scaled EXP formula**. Since we are building our own, here is the formula to implement:

```
EXP = (a * b * L) / (5 * s) * ((2*L + 10)^2.5 / (L + Lp + 10)^2.5) + 1
```

Where:
- **a** = 1.0 for wild battles, 1.5 for trainer battles
- **b** = defeated Pokemon's BaseEXPYield (from species data)
- **L** = defeated Pokemon's level
- **Lp** = victorious Pokemon's level
- **s** = number of non-fainted Pokemon that participated against the defeated Pokemon (EXP share counts as participating)
- The `+1` at the end ensures at least 1 EXP is always gained

### Modifiers (applied multiplicatively)

| Condition | Multiplier |
|-----------|-----------|
| Traded Pokemon (different OT) | x1.5 |
| Lucky Egg held | x1.5 |
| EXP Share holder (did not participate) | x0.5 (of the share, separate calc) |
| Affection >= 2 hearts (Gen 6+) | x1.2 |
| Pass Power / O-Power active | varies |

### Simplified Formula for PokemonGreen v1

For the initial implementation, use a simplified but faithful formula:

```csharp
public static uint CalculateEXPGain(
    int defeatedBaseEXPYield,
    int defeatedLevel,
    int victorLevel,
    bool isTrainerBattle,
    int participantCount)
{
    float a = isTrainerBattle ? 1.5f : 1.0f;
    float b = defeatedBaseEXPYield;
    float L = defeatedLevel;
    float Lp = victorLevel;
    float s = participantCount;

    // Gen V scaled formula
    float numerator = a * b * L;
    float denominator = 5.0f * s;
    float scaleFactor = MathF.Pow(2 * L + 10, 2.5f) / MathF.Pow(L + Lp + 10, 2.5f);
    float exp = (numerator / denominator) * scaleFactor + 1;

    return (uint)MathF.Floor(exp);
}
```

### EXP Distribution Rules

1. Only non-fainted, non-egg Pokemon that participated in the battle (were sent out against the defeated foe) earn EXP.
2. The "participated" set is tracked per foe Pokemon -- if multiple party members fought the same foe, they split EXP.
3. EXP Share (when implemented) gives EXP to all non-fainted party members, with participants getting full and non-participants getting half.

---

## 3. EXP Yield Data Structure

### Per-Species Data Required

Each species needs the following fields (sourced from old engine's `BaseStats.json` files):

```json
{
  "HP": 39,
  "Attack": 52,
  "Defense": 43,
  "SpAttack": 60,
  "SpDefense": 50,
  "Speed": 65,
  "Type1": "Fire",
  "Type2": "None",
  "CatchRate": 45,
  "BaseFriendship": 70,
  "GrowthRate": "MediumSlow",
  "BaseEXPYield": 62,
  "Ability1": "Blaze",
  "Ability2": "None",
  "AbilityH": "SolarPower",
  "FleeRate": 0,
  "Weight": 8.5
}
```

### Critical Fields for EXP System

| Field | Type | Purpose |
|-------|------|---------|
| `BaseEXPYield` | `ushort` | Base EXP yielded when this species is defeated |
| `GrowthRate` | `enum` | Determines EXP-to-level curve |
| Base Stats (HP/Atk/Def/SpA/SpD/Spe) | `byte` each | Used for stat recalculation on level-up |

### Example BaseEXPYield Values

| Species | BaseEXPYield |
|---------|-------------|
| Charmander | 62 |
| Pidgey | 50 |
| Caterpie | 39 |
| Geodude | 60 |
| Chansey | 395 |
| Blissey | 608 |

### New Data Class Needed

```csharp
public class SpeciesData
{
    public int SpeciesId { get; set; }
    public string Name { get; set; }

    // Base stats
    public int BaseHP { get; set; }
    public int BaseAttack { get; set; }
    public int BaseDefense { get; set; }
    public int BaseSpAttack { get; set; }
    public int BaseSpDefense { get; set; }
    public int BaseSpeed { get; set; }

    // EXP system
    public int BaseEXPYield { get; set; }
    public GrowthRate GrowthRate { get; set; }

    // Evolution
    public EvolutionEntry[]? Evolutions { get; set; }

    // Level-up moves
    public LevelUpMoveEntry[]? LevelUpMoves { get; set; }
}
```

---

## 4. Growth Rates and EXP Thresholds

### Growth Rate Enum

The old engine uses PBE's `PBEGrowthRate` enum. We need our own:

```csharp
public enum GrowthRate : byte
{
    Erratic,
    Fast,
    MediumFast,
    MediumSlow,
    Slow,
    Fluctuating
}
```

### EXP Required Per Level

Each growth rate has a formula for total EXP at a given level. The old engine calls `PBEDataProvider.Instance.GetEXPRequired(growthRate, level)`.

Standard formulas (n = level):

| Growth Rate | Formula | EXP at Lv100 |
|------------|---------|-------------|
| **Erratic** | Piecewise (see below) | 600,000 |
| **Fast** | 4n^3 / 5 | 800,000 |
| **MediumFast** | n^3 | 1,000,000 |
| **MediumSlow** | 6n^3/5 - 15n^2 + 100n - 140 | 1,059,860 |
| **Slow** | 5n^3 / 4 | 1,250,000 |
| **Fluctuating** | Piecewise (see below) | 1,640,000 |

### Implementation

```csharp
public static uint GetEXPForLevel(GrowthRate rate, int level)
{
    if (level <= 1) return 0;
    double n = level;
    return rate switch
    {
        GrowthRate.Fast => (uint)(4 * n * n * n / 5),
        GrowthRate.MediumFast => (uint)(n * n * n),
        GrowthRate.MediumSlow => (uint)(6 * n * n * n / 5 - 15 * n * n + 100 * n - 140),
        GrowthRate.Slow => (uint)(5 * n * n * n / 4),
        GrowthRate.Erratic => GetErraticEXP(level),
        GrowthRate.Fluctuating => GetFluctuatingEXP(level),
        _ => (uint)(n * n * n)
    };
}
```

### Erratic Growth Rate (piecewise)

```
n < 50:  n^3 * (100 - n) / 50
n < 68:  n^3 * (150 - n) / 100
n < 98:  n^3 * ((1911 - 10n) / 3) / 500
n >= 98: n^3 * (160 - n) / 100
```

### Fluctuating Growth Rate (piecewise)

```
n < 15:  n^3 * ((n + 1) / 3 + 24) / 50
n < 36:  n^3 * (n + 14) / 50
n >= 36: n^3 * (n / 2 + 32) / 50
```

### EXP Bar Percentage Calculation

From the old engine's `RenderUtils.EXP_SingleLine()`:

```csharp
public static float GetEXPPercent(uint currentEXP, int level, GrowthRate rate)
{
    if (level >= MaxLevel) return 0f;
    uint expPrev = GetEXPForLevel(rate, level);
    uint expNext = GetEXPForLevel(rate, level + 1);
    return (float)(currentEXP - expPrev) / (expNext - expPrev);
}
```

---

## 5. Level-Up Stat Recalculation

### Stat Formula (Gen III-V)

The old engine uses `PBEDataUtils.CalculateStat()`. The standard formula:

**HP:**
```
HP = floor((2 * Base + IV + floor(EV/4)) * Level / 100) + Level + 10
```
Exception: Shedinja always has 1 HP.

**Other Stats (Atk, Def, SpA, SpD, Spe):**
```
Stat = floor((floor((2 * Base + IV + floor(EV/4)) * Level / 100) + 5) * NatureMod)
```

Where `NatureMod` is:
- 1.1 if the nature boosts this stat
- 0.9 if the nature hinders this stat
- 1.0 otherwise

### Implementation

```csharp
public static int CalculateHP(int baseHP, int iv, int ev, int level, int speciesId)
{
    // Shedinja exception
    if (speciesId == SHEDINJA_ID) return 1;

    return (2 * baseHP + iv + ev / 4) * level / 100 + level + 10;
}

public static int CalculateStat(int baseStat, int iv, int ev, int level, float natureMod)
{
    int raw = (2 * baseStat + iv + ev / 4) * level / 100 + 5;
    return (int)(raw * natureMod);
}
```

### On Level-Up: HP Adjustment

From the old engine's `PartyPokemon.CalcMaxHPAndAdjustHP()`:

```csharp
private void CalcMaxHPAndAdjustHP()
{
    int oldMaxHP = MaxHP;
    MaxHP = CalculateHP(...);
    // Don't adjust current HP if fainted
    if (CurrentHP != 0 && MaxHP != oldMaxHP)
    {
        CurrentHP = Math.Max(1, CurrentHP + (MaxHP - oldMaxHP));
    }
}
```

This means when a Pokemon levels up, the HP difference (new max - old max) is added to current HP, keeping the same "damage taken" value.

---

## 6. Level-Up Move Learning

### Data Structure

From the old engine's `LevelUpData`:

```json
{
  "LevelUpMoves": [
    {"Move": "Scratch", "Level": 1},
    {"Move": "Growl", "Level": 1},
    {"Move": "Ember", "Level": 7},
    {"Move": "SmokeScreen", "Level": 10}
  ]
}
```

### Learning Flow

From `EvolutionGUI.CheckForMoreLearnableMoves()`:

1. On level-up, get all moves at the new level: `GetNewMoves(level)`.
2. For each new move:
   a. If the Pokemon has an empty move slot, auto-learn it.
   b. If all 4 slots are full, prompt: "Wants to learn X but already knows 4 moves. Forget a move?"
   c. If player says Yes, open summary screen to pick a move to replace.
   d. If player says No, confirm: "Give up on learning X?"
3. Process moves one at a time.

### Default Moves (for newly created Pokemon)

From `LevelUpData.GetDefaultMoves()`: Get the **last 4** distinct usable moves the Pokemon would have learned by its current level.

---

## 7. Evolution Triggers

### Evolution Methods

From the old engine's `EvoMethod` enum and `Evolution.GetLevelUpEvolution()`:

| Method | Param | Condition |
|--------|-------|-----------|
| `LevelUp` | level | Pokemon level >= param |
| `Friendship_LevelUp` | friendship | Friendship >= param |
| `Friendship_Day_LevelUp` | friendship | Day time + friendship >= param |
| `Friendship_Night_LevelUp` | friendship | Night time + friendship >= param |
| `Male_LevelUp` | level | Level >= param AND male |
| `Female_LevelUp` | level | Level >= param AND female |
| `ATK_GT_DEF_LevelUp` | level | Level >= param AND Attack > Defense |
| `ATK_EE_DEF_LevelUp` | level | Level >= param AND Attack == Defense |
| `ATK_LT_DEF_LevelUp` | level | Level >= param AND Attack < Defense |
| `Move_LevelUp` | move | Pokemon knows the specified move |
| `Item_Day_LevelUp` | item | Holds item AND day time |
| `Item_Night_LevelUp` | item | Holds item AND night time |
| `PartySpecies_LevelUp` | species | Another party member is that species |
| `Stone` | item | Use evolution stone |
| `Trade` | -- | Traded |
| `Item_Trade` | item | Traded while holding item |
| `Ninjask_LevelUp` | level | Standard level evo (also creates Shedinja) |

### Evolution Data Format

```json
{
  "BabySpecies": "Charmander",
  "Evolutions": [
    {
      "Method": "LevelUp",
      "LevelRequired": 16,
      "Species": "Charmeleon",
      "Form": null
    }
  ]
}
```

### Everstone Rule

From `Evolution.HeldEverstonePreventsEvolution()`:
- Everstone blocks level-up and trade evolutions.
- Exception: Kadabra can still evolve even while holding Everstone.
- Everstone does NOT block stone (item) evolutions.

### Shedinja Special Case

From `Evolution.TryCreateShedinja()`:
- When Nincada evolves into Ninjask, if the party has room AND the player has a Poke Ball in inventory, a Shedinja is created.
- Shedinja copies: PID, Pokerus, OT, Met info, EXP, Nature, Moveset, EVs, IVs.
- Shedinja does NOT copy: Nickname, Item, Gender, Ability (recalculated).

### Evolution Execution

From `PartyPokemon.Evolve()`:
1. Increment GameStat for evolved Pokemon.
2. If nickname matches the old species name (default nickname), update it to new species name.
3. Change Species and Form.
4. Recalculate ability based on AbilityType slot.
5. Recalculate MaxHP (and adjust current HP).

### Level-Up Evolution is Cancellable

From the old engine: level-up evolutions can be cancelled by pressing B during the animation. The `_canCancel` flag is set to `true` for level-up evolutions. Stone/trade evolutions cannot be cancelled.

---

## 8. Friendship Adjustments

### Battle-Related Friendship Events

From `Friendship.cs`:

| Event | Friendship 0-99 | Friendship 100-199 | Friendship 200+ |
|-------|-----------------|---------------------|------------------|
| `LevelUpBattle` | +5 | +4 | +3 |
| `Faint_L30` (opponent < 30 levels above) | -1 | -1 | -1 |
| `Faint_GE30` (opponent >= 30 levels above) | -5 | -5 | -10 |

### Modifiers

- **Luxury Ball**: +1 to positive changes.
- **Met Location matches current location**: +1 to positive changes.
- **Soothe Bell held**: x1.5 to positive changes (after above additions).

### When to Apply

From `BattleGUI`:
- `UpdateFriendshipForLevelUp()`: Called on `PBEPkmnLevelChangedPacket`.
- `UpdateFriendshipForFaint()`: Called on `PBEPkmnFaintedPacket`, checks if opponent was 30+ levels higher.

---

## 9. EXP Bar UI

### Current State in PokemonGreen

The new engine already has an EXP bar drawn in `BattleInfoBar.DrawAllyBar()` (line 143-148 of `BattleInfoBar.cs`) and `UIStyle.DrawEXPBar()`. However, it currently receives a **hardcoded 0.5f** `expPercent` value:

```csharp
// Game1.cs line 657 — hardcoded EXP percent
BattleInfoBar.DrawAllyBar(..., _allyPokemon, 0.5f, infoFontScale);
```

### Required Changes

1. **Calculate real EXP percent** from the ally Pokemon's current EXP, level, and growth rate.
2. **Animate EXP gain** after battle victory: smoothly fill the bar from current to new value.
3. **Handle level-up during animation**: When EXP bar fills to 100%, reset to 0% and increment the displayed level, then continue filling.

### EXP Bar Animation Flow

```
1. Foe faints
2. Calculate EXP gained per participant
3. For each Pokemon gaining EXP:
   a. Show message: "<Name> gained X EXP. Points!"
   b. Animate EXP bar filling
   c. If bar fills completely:
      - Show message: "<Name> grew to Lv. Y!"
      - Update level display
      - Recalculate stats
      - Check for new moves to learn
      - Reset bar and continue filling if more EXP remains
   d. After all EXP applied, check evolution triggers
4. Continue battle (or end if foe was last)
```

### EXP Bar Rendering (Already Implemented)

From `UIStyle.DrawEXPBar()`:
- 3px tall (at scale 1), 1px dark border, blue fill (`Color(0, 160, 255)`).
- Fill width = `innerWidth * expPercent`.
- This already matches the old engine's `RenderUtils.EXP_SingleLine()`.

---

## 10. Integration Points with Current BattleTurnManager

### Current Battle Flow (PokemonGreen)

From `BattleTurnManager.cs`:

```
Idle -> StartTurn(moveIndex)
  -> PlayerAttack -> AfterPlayerAttack
    -> if foe fainted: BattleOver -> "You win!" -> exitBattle
    -> else: FoeAttack -> AfterFoeAttack
      -> if ally fainted: BattleOver -> "You blacked out!" -> exitBattle
      -> else: Idle (return to main menu)
```

### Missing: EXP Reward Phase

A new phase needs to be inserted between "foe fainted" and "You win!":

```
AfterPlayerAttack:
  if foe fainted:
    Phase = EXPReward
    -> Show "Wild <foe> fainted!"
    -> Calculate EXP gain
    -> Show "<ally> gained X EXP. Points!"
    -> Animate EXP bar
    -> If level up:
      -> Show "<ally> grew to Lv. Y!"
      -> Check new moves
      -> Check evolution (queue for post-battle)
    -> If more foes (future double battles): continue
    -> Else: Show "You win!" -> exitBattle (with pending evolution check)
```

### New TurnPhase Values

```csharp
public enum TurnPhase
{
    Idle,
    PlayerAttack,
    FoeAttack,
    TurnEnd,
    EXPReward,      // NEW: awarding EXP, animating bar
    LevelUp,        // NEW: showing level-up message, learning moves
    BattleOver
}
```

### Post-Battle Evolution Check

The old engine processes evolutions after the battle fade-out, in `OverworldGUI.ReturnToFieldWithFadeInAfterEvolutionCheck()`. The new engine should:

1. After `_gameWorld.ExitBattle()`, check if any party Pokemon have pending evolutions.
2. If so, display the evolution screen before returning to the overworld.

---

## 11. Code Changes Required

### New Files to Create

| File | Purpose |
|------|---------|
| `src/PokemonGreen.Core/Pokemon/SpeciesData.cs` | Species base stats, EXP yield, growth rate |
| `src/PokemonGreen.Core/Pokemon/SpeciesRegistry.cs` | Registry/lookup for species data |
| `src/PokemonGreen.Core/Pokemon/GrowthRate.cs` | Growth rate enum and EXP-for-level formulas |
| `src/PokemonGreen.Core/Pokemon/StatCalculator.cs` | HP and stat calculation formulas |
| `src/PokemonGreen.Core/Pokemon/EXPCalculator.cs` | EXP gain formula |
| `src/PokemonGreen.Core/Pokemon/EvolutionData.cs` | Evolution methods and data |
| `src/PokemonGreen.Core/Pokemon/EvolutionChecker.cs` | Evolution trigger logic |
| `src/PokemonGreen.Core/Pokemon/LevelUpMoveData.cs` | Level-up move list per species |
| `src/PokemonGreen.Core/Pokemon/Nature.cs` | Nature enum with stat modifiers |
| `src/PokemonGreen.Core/Pokemon/EVs.cs` | Effort Values tracking |
| `src/PokemonGreen.Core/Pokemon/IVs.cs` | Individual Values |

### Files to Modify

#### `src/PokemonGreen.Core/Pokemon/PartyPokemon.cs`

Add the following properties:

```csharp
public class PartyPokemon
{
    // Existing properties...

    // NEW: EXP system
    public uint EXP { get; set; }
    public int SpeciesId { get; set; }         // numeric species ID for lookups
    public Nature Nature { get; set; }
    public EVs EffortValues { get; set; }
    public IVs IndividualValues { get; set; }
    public int Friendship { get; set; } = 70;  // default base friendship

    // Recalculate all stats from base stats + EVs + IVs + Nature
    public void RecalculateStats() { ... }

    // Recalculate MaxHP and adjust CurrentHP (don't revive fainted)
    public void RecalculateMaxHPAndAdjust() { ... }

    // Add EXP, handle level-up(s), return list of level changes
    public List<int> AddEXP(uint amount) { ... }
}
```

#### `src/PokemonGreen.Core/Battle/BattlePokemon.cs`

Add:

```csharp
public class BattlePokemon
{
    // Existing properties...

    // NEW: EXP display animation
    public float DisplayEXPPercent { get; set; }
    public float TargetEXPPercent { get; set; }

    // NEW: Track participation for EXP splitting
    public bool ParticipatedAgainstCurrentFoe { get; set; }

    // NEW: Link back to party
    public PartyPokemon? PartySource { get; set; }

    public void UpdateDisplayEXP(float deltaTime, float fillSpeed = 100f) { ... }
}
```

#### `src/PokemonGreen.Core/Battle/BattleTurnManager.cs`

Major additions:

1. Add `EXPReward` and `LevelUp` phases.
2. After foe faints, calculate and award EXP before showing "You win!".
3. Add callbacks for EXP animation and move-learning UI.
4. Track participation (which ally Pokemon were sent out against which foe).

```csharp
private void AfterPlayerAttack()
{
    if (_foe.IsFainted)
    {
        Phase = TurnPhase.EXPReward;
        _showMessage($"Wild {_foe.Nickname} fainted!", () => AwardEXP());
        return;
    }
    // ... existing foe attack logic
}

private void AwardEXP()
{
    var speciesData = SpeciesRegistry.Get(_foe.Species);
    uint expGain = EXPCalculator.Calculate(
        speciesData.BaseEXPYield,
        _foe.Level,
        _ally.Level,
        isTrainerBattle: false,
        participantCount: 1);

    _showMessage($"{_ally.Nickname} gained {expGain} EXP. Points!", () =>
    {
        // Apply EXP to party Pokemon, handle level-ups
        ApplyEXPToParty(expGain);
    });
}
```

#### `src/PokemonGreen.Core/Battle/BattleInfoBar.cs`

Change the `DrawAllyBar` call signature to accept the actual EXP percent from `BattlePokemon.DisplayEXPPercent` instead of a hardcoded value.

#### `src/PokemonGreen/Game1.cs`

1. **Line 657**: Replace hardcoded `0.5f` with actual EXP percent:
   ```csharp
   // Before:
   BattleInfoBar.DrawAllyBar(..., _allyPokemon, 0.5f, infoFontScale);
   // After:
   BattleInfoBar.DrawAllyBar(..., _allyPokemon, _allyPokemon.DisplayEXPPercent, infoFontScale);
   ```

2. **`EnterBattle()`**: Create `BattlePokemon` from the player's `PartyPokemon` with proper EXP and species data.

3. **`exitBattle` callback**: After returning from battle, check for pending evolutions.

4. **Update loop**: Add `_allyPokemon.UpdateDisplayEXP(deltaTime)` alongside the existing `UpdateDisplayHP()` call.

### Data Files to Create

Species data JSON files (can be seeded from the old engine's `Assets/Pokedata/*/BaseStats.json`):

```
Content/Data/Species/
  001_Bulbasaur.json
  004_Charmander.json
  007_Squirtle.json
  ...
```

Each file contains the `SpeciesData` fields listed in Section 3.

### Implementation Priority Order

1. **Phase 1 -- Core EXP**: `GrowthRate.cs`, `EXPCalculator.cs`, `StatCalculator.cs`, expand `PartyPokemon.cs`.
2. **Phase 2 -- Battle Integration**: Modify `BattleTurnManager` to award EXP, add EXP phases.
3. **Phase 3 -- UI Animation**: Wire up real EXP percent in `BattleInfoBar`, animate EXP bar fill.
4. **Phase 4 -- Level-Up Flow**: Move learning prompts, stat recalculation messages.
5. **Phase 5 -- Evolution**: `EvolutionChecker.cs`, evolution screen, post-battle evolution queue.
6. **Phase 6 -- Species Data**: Create `SpeciesRegistry` and populate JSON data files.
7. **Phase 7 -- IVs/EVs/Nature**: Full stat system with IVs, EVs, Natures.

---

## Appendix A: Nature Stat Modifiers

25 natures, each boosts one stat by 10% and hinders another by 10% (5 are neutral):

| Nature | +10% | -10% |
|--------|------|------|
| Hardy | -- | -- |
| Lonely | Attack | Defense |
| Brave | Attack | Speed |
| Adamant | Attack | SpAttack |
| Naughty | Attack | SpDefense |
| Bold | Defense | Attack |
| Docile | -- | -- |
| Relaxed | Defense | Speed |
| Impish | Defense | SpAttack |
| Lax | Defense | SpDefense |
| Timid | Speed | Attack |
| Hasty | Speed | Defense |
| Serious | -- | -- |
| Jolly | Speed | SpAttack |
| Naive | Speed | SpDefense |
| Modest | SpAttack | Attack |
| Mild | SpAttack | Defense |
| Quiet | SpAttack | Speed |
| Bashful | -- | -- |
| Rash | SpAttack | SpDefense |
| Calm | SpDefense | Attack |
| Gentle | SpDefense | Defense |
| Sassy | SpDefense | Speed |
| Careful | SpDefense | SpAttack |
| Quirky | -- | -- |

---

## Appendix B: EV Yield Per Defeated Pokemon

When a Pokemon is defeated, the victor gains EVs corresponding to the defeated species' EV yield. The old engine tracks this inside PBE. Example EV yields:

| Species | HP | Atk | Def | SpA | SpD | Spe |
|---------|----|-----|-----|-----|-----|-----|
| Charmander | 0 | 0 | 0 | 0 | 0 | 1 |
| Pidgey | 0 | 0 | 0 | 0 | 0 | 1 |
| Geodude | 0 | 0 | 1 | 0 | 0 | 0 |
| Chansey | 2 | 0 | 0 | 0 | 0 | 0 |

EV rules:
- Max 252 EVs in any single stat.
- Max 510 total EVs across all stats.
- Every 4 EVs = +1 stat point.
- Macho Brace doubles EV gain.
- Pokerus doubles EV gain (stacks with Macho Brace).
- Power items add +4 EVs to a specific stat per battle.

---

## Appendix C: Old Engine Post-Battle Flow Reference

From `BattlePokemonParty.UpdateToParty()` and `BattleGUI.CleanUpStuffAfterFadeOut()`:

```
1. Battle ends (PBEBattleState.Ended)
2. Fade out battle screen
3. For each trainer's party:
   a. Copy PBE data back to PartyPokemon:
      - HP, Status1, SleepTurns
      - Moveset (with PP updates)
      - Form (revert form)
      - Friendship
      - Item (may have been consumed)
      - Ability (revert ability)
      - EVs
      - Level and EXP
   b. Recalculate MaxHP
   c. Update forms (Burmy, Shaymin, seasonal)
   d. If level changed, check level-up evolution -> queue
4. Update player inventory (items used in battle)
5. Handle capture (if wild capture):
   - Set caught OT, met location
   - Friendship adjustment for Friend Ball
   - Full heal for Heal Ball
   - Add to party/PC
   - Set Pokedex caught flag
6. Try create/spread Pokerus
7. Fade in to overworld
8. Process pending evolutions (EvolutionGUI)
9. Return to field
```
