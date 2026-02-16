# Health and Recovery Items Design

**Date:** 2026-02-15
**Sprint:** Item Effect System & Recovery Item Registry

---

## 1. Overview

This document defines the complete catalog of health and recovery items for PokemonGreen, the item effect system that applies them, and the code changes needed to integrate everything with the existing `ItemRegistry`, `PlayerInventory`, and battle/overworld systems.

### Scope

This covers five categories of consumable recovery items:
- **HP Healing** -- Potions, drinks, food items
- **Status Condition Cures** -- Antidote, Burn Heal, etc.
- **Revive Items** -- Revive fainted Pokemon
- **PP Restoration** -- Ether, Elixir, etc.
- **Healing Berries** -- Berry items with recovery effects

It does NOT cover Pokeballs, evolution stones, battle stat boosters (X Attack, etc.), TMs/HMs, held items, or key items. Those are separate design concerns.

---

## 2. What Exists Today

### Old Engine (PokemonGameEngine)

The old engine defines items via the `ItemType` enum in `D:\Projects\PokemonGameEngine\PokemonGameEngine\Item\ItemType.cs`. All health/recovery items relevant to us are defined there with Gen 5 (BW/BW2) IDs:

| Old ID | Name | Old ID | Name |
|--------|------|--------|------|
| 17 | Potion | 18 | Antidote |
| 19 | BurnHeal | 20 | IceHeal |
| 21 | Awakening | 22 | ParlyzHeal |
| 23 | FullRestore | 24 | MaxPotion |
| 25 | HyperPotion | 26 | SuperPotion |
| 27 | FullHeal | 28 | Revive |
| 29 | MaxRevive | 30 | FreshWater |
| 31 | SodaPop | 32 | Lemonade |
| 33 | MoomooMilk | 34 | EnergyPowder |
| 35 | EnergyRoot | 36 | HealPowder |
| 37 | RevivalHerb | 38 | Ether |
| 39 | MaxEther | 40 | Elixir |
| 41 | MaxElixir | 42 | LavaCookie |
| 43 | BerryJuice | 44 | SacredAsh |

Berries with healing effects (old engine IDs):

| Old ID | Name | Old ID | Name |
|--------|------|--------|------|
| 149 | CheriBerry | 150 | ChestoBerry |
| 151 | PechaBerry | 152 | RawstBerry |
| 153 | AspearBerry | 154 | LeppaBerry |
| 155 | OranBerry | 156 | PersimBerry |
| 157 | LumBerry | 158 | SitrusBerry |

The old engine delegates item effect logic entirely to the external `PokemonBattleEngine.dll` library. The game engine itself has no item effect system -- `BattleGUI_Actions.cs` line 299 shows `ActionsBuilder.PushItem(PBEItem.DuskBall)` as a temporary hardcoded placeholder for the bag action. The `PartyPokemon` class has `HealFully()`, `HealStatus()`, and `HealMoves()` methods used only by the `HealParty` script command (Pokemon Center healing), not by individual items.

The old engine's `ItemData.GetPouchType()` sorts items into pouches by string-matching item names (e.g., anything ending in `"Potion"` goes to `ItemPouchType.Medicine`).

### New Engine (PokemonGreen)

The current item system lives in `D:\Projects\PokemonGreen\src\PokemonGreen.Core\Items\`:

- **`ItemCategory.cs`** -- Enum with `Pokeball, Medicine, Battle, Berry, KeyItem, TM, HM, EvolutionStone, HeldItem, Valuable, Mail`
- **`ItemDefinition.cs`** -- Record: `Id, Name, SpriteName, Category, BuyPrice, SellPrice, UsableInBattle, UsableOverworld, Effect` (Effect is a nullable string)
- **`ItemData.cs`** -- JSON deserialization DTO class
- **`ItemRegistry.cs`** -- Static registry, loads from `items.json`, provides `GetItem(id)`, `GetItemsByCategory()`, `AllItems`
- **`InventorySlot.cs`** -- Simple `ItemId + Quantity` pair
- **`PlayerInventory.cs`** -- Dictionary of `ItemCategory -> List<InventorySlot>`, with `AddItem()`, `GetPouch()`, and a `CreateTestInventory()`

The current `items.json` at `D:\Projects\PokemonGreen\src\PokemonGreen.Assets\Data\items.json` has placeholder items:
- IDs 100-105: Potion, Super Potion, Hyper Potion, Max Potion, Full Restore, Full Heal
- IDs 200-204: Generic colored berries (no specific effects)
- IDs 500-504: Fruit items (heal_hp_10)
- IDs 600-601: Herb items (cure_poison, heal_hp_30)

**Critical gap:** There is no item effect processing system. The `Effect` field on `ItemDefinition` is a raw string like `"heal_hp_20"` with no code to parse or execute it. There is no way to use an item on a Pokemon.

The `PartyPokemon` class at `D:\Projects\PokemonGreen\src\PokemonGreen.Core\Pokemon\PartyPokemon.cs` has `CurrentHP`, `MaxHP`, `Status` (a nullable string), and `IsFainted`. It has no `HealHP()`, `CureStatus()`, or move/PP system at all.

---

## 3. Complete Health and Recovery Item Catalog

### ID Numbering Convention

Following the existing `items.json` pattern:
- **0-99:** Pokeballs
- **100-199:** Medicine (potions, status cures, revives, PP items)
- **200-299:** Berries
- **300-399:** Evolution Stones
- **400-499:** Valuables
- **500-599:** Fruits/Food
- **600-699:** Herbs
- **700-799:** Key Items

### 3a. HP Healing Items (Medicine)

| ID | Name | Effect | Heal Amount | Buy | Sell | Battle | Overworld | Description |
|----|------|--------|-------------|-----|------|--------|-----------|-------------|
| 100 | Potion | `heal_hp` | 20 | 300 | 150 | Yes | Yes | Restores 20 HP to one Pokemon. |
| 101 | Super Potion | `heal_hp` | 50 | 700 | 350 | Yes | Yes | Restores 50 HP to one Pokemon. |
| 102 | Hyper Potion | `heal_hp` | 200 | 1200 | 600 | Yes | Yes | Restores 200 HP to one Pokemon. |
| 103 | Max Potion | `heal_hp` | MAX | 2500 | 1250 | Yes | Yes | Fully restores HP of one Pokemon. |
| 104 | Full Restore | `heal_full` | MAX | 3000 | 1500 | Yes | Yes | Fully restores HP and cures all status conditions of one Pokemon. |
| 106 | Fresh Water | `heal_hp` | 50 | 200 | 100 | Yes | Yes | Restores 50 HP to one Pokemon. |
| 107 | Soda Pop | `heal_hp` | 60 | 300 | 150 | Yes | Yes | Restores 60 HP to one Pokemon. |
| 108 | Lemonade | `heal_hp` | 80 | 350 | 175 | Yes | Yes | Restores 80 HP to one Pokemon. |
| 109 | Moomoo Milk | `heal_hp` | 100 | 500 | 250 | Yes | Yes | Restores 100 HP to one Pokemon. |
| 110 | Berry Juice | `heal_hp` | 20 | 100 | 50 | Yes | Yes | Restores 20 HP to one Pokemon. |
| 111 | Energy Powder | `heal_hp` | 50 | 500 | 250 | Yes | Yes | Restores 50 HP but lowers friendship. |
| 112 | Energy Root | `heal_hp` | 200 | 800 | 400 | Yes | Yes | Restores 200 HP but lowers friendship. |
| 113 | Lava Cookie | `heal_status_all` | 0 | 200 | 100 | Yes | Yes | Cures all status conditions of one Pokemon. |
| 114 | Sweet Heart | `heal_hp` | 20 | 0 | 50 | Yes | Yes | Restores 20 HP to one Pokemon. |

### 3b. Status Condition Cures (Medicine)

| ID | Name | Effect | Cures | Buy | Sell | Battle | Overworld | Description |
|----|------|--------|-------|-----|------|--------|-----------|-------------|
| 105 | Full Heal | `cure_status_all` | All | 600 | 300 | Yes | Yes | Cures all status conditions of one Pokemon. |
| 120 | Antidote | `cure_status` | Poison | 100 | 50 | Yes | Yes | Cures poisoning of one Pokemon. |
| 121 | Burn Heal | `cure_status` | Burn | 250 | 125 | Yes | Yes | Cures a burn on one Pokemon. |
| 122 | Ice Heal | `cure_status` | Freeze | 250 | 125 | Yes | Yes | Thaws a frozen Pokemon. |
| 123 | Awakening | `cure_status` | Sleep | 250 | 125 | Yes | Yes | Wakes up a sleeping Pokemon. |
| 124 | Parlyz Heal | `cure_status` | Paralysis | 200 | 100 | Yes | Yes | Cures paralysis of one Pokemon. |
| 125 | Heal Powder | `cure_status_all` | All | 450 | 225 | Yes | Yes | Cures all status conditions but lowers friendship. |

### 3c. Revive Items (Medicine)

| ID | Name | Effect | Heal Amount | Buy | Sell | Battle | Overworld | Description |
|----|------|--------|-------------|-----|------|--------|-----------|-------------|
| 130 | Revive | `revive` | 50% MaxHP | 1500 | 750 | Yes | Yes | Revives a fainted Pokemon to half HP. |
| 131 | Max Revive | `revive` | 100% MaxHP | 0 | 2000 | Yes | Yes | Revives a fainted Pokemon to full HP. |
| 132 | Revival Herb | `revive` | 100% MaxHP | 2800 | 1400 | Yes | Yes | Revives a fainted Pokemon to full HP but lowers friendship. |
| 133 | Sacred Ash | `revive_all` | 100% MaxHP | 0 | 10000 | No | Yes | Revives all fainted Pokemon in the party to full HP. |

### 3d. PP Restoration Items (Medicine)

| ID | Name | Effect | Restore Amount | Buy | Sell | Battle | Overworld | Description |
|----|------|--------|----------------|-----|------|--------|-----------|-------------|
| 140 | Ether | `restore_pp` | 10 PP (one move) | 0 | 600 | Yes | Yes | Restores 10 PP of one move. |
| 141 | Max Ether | `restore_pp` | MAX PP (one move) | 0 | 1000 | Yes | Yes | Fully restores PP of one move. |
| 142 | Elixir | `restore_pp_all` | 10 PP (all moves) | 0 | 1500 | Yes | Yes | Restores 10 PP of all moves. |
| 143 | Max Elixir | `restore_pp_all` | MAX PP (all moves) | 0 | 2500 | Yes | Yes | Fully restores PP of all moves. |

### 3e. Healing Berries

| ID | Name | Effect | Details | Buy | Sell | Battle | Overworld | Description |
|----|------|--------|---------|-----|------|--------|-----------|-------------|
| 200 | Oran Berry | `heal_hp` | 10 HP | 20 | 10 | Yes | Yes | Restores 10 HP. Can be held for auto-use. |
| 201 | Sitrus Berry | `heal_hp` | 25% MaxHP | 20 | 10 | Yes | Yes | Restores 1/4 of max HP. Can be held for auto-use. |
| 210 | Cheri Berry | `cure_status` | Paralysis | 20 | 10 | Yes | Yes | Cures paralysis. Can be held for auto-use. |
| 211 | Chesto Berry | `cure_status` | Sleep | 20 | 10 | Yes | Yes | Cures sleep. Can be held for auto-use. |
| 212 | Pecha Berry | `cure_status` | Poison | 20 | 10 | Yes | Yes | Cures poisoning. Can be held for auto-use. |
| 213 | Rawst Berry | `cure_status` | Burn | 20 | 10 | Yes | Yes | Cures a burn. Can be held for auto-use. |
| 214 | Aspear Berry | `cure_status` | Freeze | 20 | 10 | Yes | Yes | Cures freezing. Can be held for auto-use. |
| 215 | Persim Berry | `cure_status` | Confusion | 20 | 10 | Yes | Yes | Cures confusion. Can be held for auto-use. |
| 216 | Lum Berry | `cure_status_all` | All | 20 | 10 | Yes | Yes | Cures all status conditions. Can be held for auto-use. |
| 220 | Leppa Berry | `restore_pp` | 10 PP (one move) | 20 | 10 | Yes | Yes | Restores 10 PP of one move. Can be held for auto-use. |

### 3f. Fruit/Food Items (Existing custom items, updated)

| ID | Name | Effect | Heal Amount | Buy | Sell | Battle | Overworld | Description |
|----|------|--------|-------------|-----|------|--------|-----------|-------------|
| 500 | Apple | `heal_hp` | 10 | 50 | 25 | Yes | Yes | Restores 10 HP to one Pokemon. |
| 501 | Green Apple | `heal_hp` | 10 | 50 | 25 | Yes | Yes | Restores 10 HP to one Pokemon. |
| 502 | Banana | `heal_hp` | 15 | 50 | 25 | Yes | Yes | Restores 15 HP to one Pokemon. |
| 503 | Grape | `heal_hp` | 10 | 50 | 25 | Yes | Yes | Restores 10 HP to one Pokemon. |
| 504 | Orange | `heal_hp` | 15 | 50 | 25 | Yes | Yes | Restores 15 HP to one Pokemon. |

### 3g. Herb Items (Existing custom items, updated)

| ID | Name | Effect | Details | Buy | Sell | Battle | Overworld | Description |
|----|------|--------|---------|-----|------|--------|-----------|-------------|
| 600 | Blue Herb | `cure_status` | Poison | 100 | 50 | Yes | Yes | Cures poisoning of one Pokemon. |
| 601 | Green Herb | `heal_hp` | 30 HP | 100 | 50 | Yes | Yes | Restores 30 HP to one Pokemon. |

---

## 4. Item Effect System Design

### 4a. Status Condition Enum

The current `PartyPokemon.Status` is a nullable string. This must be changed to a proper enum for the effect system to work correctly.

```csharp
// D:\Projects\PokemonGreen\src\PokemonGreen.Core\Pokemon\StatusCondition.cs (NEW FILE)
namespace PokemonGreen.Core.Pokemon;

public enum StatusCondition : byte
{
    None = 0,
    Poison,
    Burn,
    Freeze,
    Sleep,
    Paralysis,
    Confusion,
    Fainted    // Not a true status, but tracked for Revive logic
}
```

### 4b. Structured Item Effect Type

Replace the raw string `Effect` field with a structured effect system.

```csharp
// D:\Projects\PokemonGreen\src\PokemonGreen.Core\Items\ItemEffectType.cs (NEW FILE)
namespace PokemonGreen.Core.Items;

/// <summary>
/// Identifies what kind of effect an item produces when used.
/// </summary>
public enum ItemEffectType
{
    None,

    // HP Healing
    HealHP,           // Restores a fixed amount of HP
    HealHPFull,       // Restores HP to max
    HealHPPercent,    // Restores a percentage of max HP

    // Status Curing
    CureStatus,       // Cures a specific status condition
    CureStatusAll,    // Cures all status conditions

    // Full Recovery
    HealFull,         // Restores full HP AND cures all status

    // Revive
    Revive,           // Revives fainted Pokemon to a fraction of max HP
    ReviveAll,        // Revives all fainted Pokemon in party

    // PP Restoration
    RestorePP,        // Restores PP for one move
    RestorePPAll,     // Restores PP for all moves

    // Non-recovery (placeholders for other item types)
    CatchPokemon,
    EvolutionStone,
    BattleStat,
    TeachMove
}
```

### 4c. Item Effect Data Record

```csharp
// D:\Projects\PokemonGreen\src\PokemonGreen.Core\Items\ItemEffect.cs (NEW FILE)
using PokemonGreen.Core.Pokemon;

namespace PokemonGreen.Core.Items;

/// <summary>
/// Parsed, structured representation of an item's effect.
/// Built from the effect string in items.json at registry load time.
/// </summary>
public record ItemEffect(
    ItemEffectType Type,
    int Amount = 0,                           // HP amount, PP amount, or percentage
    StatusCondition TargetStatus = StatusCondition.None,  // Which status to cure (for CureStatus)
    bool LowersFriendship = false             // Bitter medicines
);
```

### 4d. Effect String Parser

The `items.json` file stores effects as human-readable strings. The parser converts these to `ItemEffect` records at load time.

```csharp
// D:\Projects\PokemonGreen\src\PokemonGreen.Core\Items\ItemEffectParser.cs (NEW FILE)
using PokemonGreen.Core.Pokemon;

namespace PokemonGreen.Core.Items;

/// <summary>
/// Parses effect strings from items.json into structured ItemEffect records.
/// </summary>
public static class ItemEffectParser
{
    public static ItemEffect? Parse(string? effectString)
    {
        if (string.IsNullOrEmpty(effectString))
            return null;

        // Split on underscore-delimited tokens
        // Examples: "heal_hp_20", "cure_status_poison", "revive_50",
        //           "heal_hp_full", "heal_full", "restore_pp_10",
        //           "restore_pp_all_10", "revive_all_100", "cure_status_all"

        var parts = effectString.Split('_');

        return effectString switch
        {
            // Full HP + status restore
            "heal_full" or "heal_full_status" =>
                new ItemEffect(ItemEffectType.HealFull, Amount: int.MaxValue),

            // Full HP restore (no status cure)
            "heal_hp_full" =>
                new ItemEffect(ItemEffectType.HealHPFull),

            // HP percentage restore
            var s when s.StartsWith("heal_hp_percent_") =>
                new ItemEffect(ItemEffectType.HealHPPercent, Amount: ParseInt(s, "heal_hp_percent_")),

            // Fixed HP restore
            var s when s.StartsWith("heal_hp_") =>
                new ItemEffect(ItemEffectType.HealHP, Amount: ParseInt(s, "heal_hp_")),

            // Cure all status conditions
            "cure_status_all" or "cure_all_status" or "heal_status_all" =>
                new ItemEffect(ItemEffectType.CureStatusAll),

            // Cure specific status
            "cure_poison" or "cure_status_poison" =>
                new ItemEffect(ItemEffectType.CureStatus, TargetStatus: StatusCondition.Poison),
            "cure_burn" or "cure_status_burn" =>
                new ItemEffect(ItemEffectType.CureStatus, TargetStatus: StatusCondition.Burn),
            "cure_freeze" or "cure_status_freeze" =>
                new ItemEffect(ItemEffectType.CureStatus, TargetStatus: StatusCondition.Freeze),
            "cure_sleep" or "cure_status_sleep" =>
                new ItemEffect(ItemEffectType.CureStatus, TargetStatus: StatusCondition.Sleep),
            "cure_paralysis" or "cure_status_paralysis" =>
                new ItemEffect(ItemEffectType.CureStatus, TargetStatus: StatusCondition.Paralysis),
            "cure_confusion" or "cure_status_confusion" =>
                new ItemEffect(ItemEffectType.CureStatus, TargetStatus: StatusCondition.Confusion),

            // Revive all party
            var s when s.StartsWith("revive_all_") =>
                new ItemEffect(ItemEffectType.ReviveAll, Amount: ParseInt(s, "revive_all_")),
            "revive_all" =>
                new ItemEffect(ItemEffectType.ReviveAll, Amount: 100),

            // Revive single Pokemon (percentage of max HP)
            var s when s.StartsWith("revive_") =>
                new ItemEffect(ItemEffectType.Revive, Amount: ParseInt(s, "revive_")),
            "revive" =>
                new ItemEffect(ItemEffectType.Revive, Amount: 50),

            // PP restore for all moves
            var s when s.StartsWith("restore_pp_all_") =>
                new ItemEffect(ItemEffectType.RestorePPAll, Amount: ParseInt(s, "restore_pp_all_")),
            "restore_pp_all" or "restore_pp_all_full" =>
                new ItemEffect(ItemEffectType.RestorePPAll, Amount: int.MaxValue),

            // PP restore for one move
            var s when s.StartsWith("restore_pp_full") =>
                new ItemEffect(ItemEffectType.RestorePP, Amount: int.MaxValue),
            var s when s.StartsWith("restore_pp_") =>
                new ItemEffect(ItemEffectType.RestorePP, Amount: ParseInt(s, "restore_pp_")),

            // Bitter medicine variants (lower friendship)
            var s when s.StartsWith("heal_hp_bitter_") =>
                new ItemEffect(ItemEffectType.HealHP, Amount: ParseInt(s, "heal_hp_bitter_"),
                    LowersFriendship: true),
            "cure_status_all_bitter" =>
                new ItemEffect(ItemEffectType.CureStatusAll, LowersFriendship: true),
            var s when s.StartsWith("revive_bitter_") =>
                new ItemEffect(ItemEffectType.Revive, Amount: ParseInt(s, "revive_bitter_"),
                    LowersFriendship: true),

            // Non-recovery effects (return null, handled by other systems)
            _ => null
        };
    }

    private static int ParseInt(string source, string prefix)
    {
        var numStr = source[prefix.Length..];
        return numStr == "full" ? int.MaxValue : int.Parse(numStr);
    }
}
```

### 4e. Item Use Handler

The core logic that applies an item effect to a Pokemon.

```csharp
// D:\Projects\PokemonGreen\src\PokemonGreen.Core\Items\ItemUseHandler.cs (NEW FILE)
using PokemonGreen.Core.Pokemon;

namespace PokemonGreen.Core.Items;

/// <summary>
/// Result of attempting to use an item on a Pokemon.
/// </summary>
public record ItemUseResult(
    bool Success,
    string Message,
    int HPRestored = 0,
    int PPRestored = 0,
    StatusCondition StatusCured = StatusCondition.None
);

/// <summary>
/// Applies item effects to Pokemon. Used by both overworld and battle contexts.
/// </summary>
public static class ItemUseHandler
{
    /// <summary>
    /// Check whether an item can be used on the target Pokemon.
    /// </summary>
    public static bool CanUseItem(ItemDefinition item, PartyPokemon target,
        bool inBattle, int? moveIndex = null)
    {
        if (item.ParsedEffect == null)
            return false;

        if (inBattle && !item.UsableInBattle)
            return false;
        if (!inBattle && !item.UsableOverworld)
            return false;

        var effect = item.ParsedEffect;
        return effect.Type switch
        {
            // HP healing: target must be alive and not at full HP
            ItemEffectType.HealHP or
            ItemEffectType.HealHPFull or
            ItemEffectType.HealHPPercent =>
                !target.IsFainted && target.CurrentHP < target.MaxHP,

            // Status cure: target must have matching status
            ItemEffectType.CureStatus =>
                !target.IsFainted && target.StatusCondition == effect.TargetStatus,

            // Cure all: target must have any status
            ItemEffectType.CureStatusAll =>
                !target.IsFainted && target.StatusCondition != StatusCondition.None,

            // Full heal: target must be alive and either missing HP or have a status
            ItemEffectType.HealFull =>
                !target.IsFainted &&
                (target.CurrentHP < target.MaxHP ||
                 target.StatusCondition != StatusCondition.None),

            // Revive: target must be fainted
            ItemEffectType.Revive =>
                target.IsFainted,

            // Revive all: at least one party member is fainted (checked externally)
            ItemEffectType.ReviveAll => true,

            // PP restore: target must have a move with less than max PP
            ItemEffectType.RestorePP =>
                moveIndex.HasValue && !target.IsFainted,
                // Full PP check requires move data, handled by caller

            // PP restore all: target must be alive
            ItemEffectType.RestorePPAll =>
                !target.IsFainted,

            _ => false
        };
    }

    /// <summary>
    /// Apply an item's effect to a Pokemon. Returns the result.
    /// Caller is responsible for decrementing inventory.
    /// </summary>
    public static ItemUseResult UseItem(ItemDefinition item, PartyPokemon target,
        int? moveIndex = null)
    {
        if (item.ParsedEffect == null)
            return new ItemUseResult(false, "This item has no effect.");

        var effect = item.ParsedEffect;

        switch (effect.Type)
        {
            case ItemEffectType.HealHP:
            {
                if (target.IsFainted)
                    return Fail($"{target.Nickname} has fainted!");
                if (target.CurrentHP >= target.MaxHP)
                    return Fail($"{target.Nickname} is already at full HP!");

                int healed = HealHP(target, effect.Amount);
                return new ItemUseResult(true,
                    $"{target.Nickname} recovered {healed} HP!",
                    HPRestored: healed);
            }

            case ItemEffectType.HealHPFull:
            {
                if (target.IsFainted)
                    return Fail($"{target.Nickname} has fainted!");
                if (target.CurrentHP >= target.MaxHP)
                    return Fail($"{target.Nickname} is already at full HP!");

                int healed = HealHP(target, target.MaxHP);
                return new ItemUseResult(true,
                    $"{target.Nickname}'s HP was fully restored!",
                    HPRestored: healed);
            }

            case ItemEffectType.HealHPPercent:
            {
                if (target.IsFainted)
                    return Fail($"{target.Nickname} has fainted!");
                if (target.CurrentHP >= target.MaxHP)
                    return Fail($"{target.Nickname} is already at full HP!");

                int amount = System.Math.Max(1, target.MaxHP * effect.Amount / 100);
                int healed = HealHP(target, amount);
                return new ItemUseResult(true,
                    $"{target.Nickname} recovered {healed} HP!",
                    HPRestored: healed);
            }

            case ItemEffectType.CureStatus:
            {
                if (target.IsFainted)
                    return Fail($"{target.Nickname} has fainted!");
                if (target.StatusCondition != effect.TargetStatus)
                    return Fail($"It won't have any effect on {target.Nickname}.");

                var cured = target.StatusCondition;
                target.StatusCondition = StatusCondition.None;
                return new ItemUseResult(true,
                    $"{target.Nickname} was cured of {StatusName(cured)}!",
                    StatusCured: cured);
            }

            case ItemEffectType.CureStatusAll:
            {
                if (target.IsFainted)
                    return Fail($"{target.Nickname} has fainted!");
                if (target.StatusCondition == StatusCondition.None)
                    return Fail($"{target.Nickname} is healthy.");

                var cured = target.StatusCondition;
                target.StatusCondition = StatusCondition.None;
                return new ItemUseResult(true,
                    $"{target.Nickname} was cured of {StatusName(cured)}!",
                    StatusCured: cured);
            }

            case ItemEffectType.HealFull:
            {
                if (target.IsFainted)
                    return Fail($"{target.Nickname} has fainted!");
                if (target.CurrentHP >= target.MaxHP &&
                    target.StatusCondition == StatusCondition.None)
                    return Fail($"It won't have any effect on {target.Nickname}.");

                int healed = HealHP(target, target.MaxHP);
                var cured = target.StatusCondition;
                target.StatusCondition = StatusCondition.None;

                string msg = healed > 0
                    ? $"{target.Nickname} was fully restored!"
                    : $"{target.Nickname} was cured of {StatusName(cured)}!";
                return new ItemUseResult(true, msg,
                    HPRestored: healed, StatusCured: cured);
            }

            case ItemEffectType.Revive:
            {
                if (!target.IsFainted)
                    return Fail($"{target.Nickname} is not fainted!");

                int restoreHP = effect.Amount >= 100
                    ? target.MaxHP
                    : System.Math.Max(1, target.MaxHP * effect.Amount / 100);
                target.CurrentHP = restoreHP;
                return new ItemUseResult(true,
                    $"{target.Nickname} was revived!",
                    HPRestored: restoreHP);
            }

            case ItemEffectType.ReviveAll:
            {
                // This is handled by the caller, which iterates the party.
                // This method handles a single target for consistency.
                if (!target.IsFainted)
                    return Fail($"{target.Nickname} is not fainted!");

                int restoreHP = effect.Amount >= 100
                    ? target.MaxHP
                    : System.Math.Max(1, target.MaxHP * effect.Amount / 100);
                target.CurrentHP = restoreHP;
                return new ItemUseResult(true,
                    $"{target.Nickname} was revived!",
                    HPRestored: restoreHP);
            }

            case ItemEffectType.RestorePP:
            case ItemEffectType.RestorePPAll:
                // PP system not yet implemented. Return success placeholder.
                return new ItemUseResult(true,
                    $"{target.Nickname}'s PP was restored!");

            default:
                return Fail("This item has no effect.");
        }
    }

    private static int HealHP(PartyPokemon target, int amount)
    {
        int before = target.CurrentHP;
        target.CurrentHP = System.Math.Min(target.MaxHP, target.CurrentHP + amount);
        return target.CurrentHP - before;
    }

    private static ItemUseResult Fail(string message)
        => new(false, message);

    private static string StatusName(StatusCondition status) => status switch
    {
        StatusCondition.Poison => "poisoning",
        StatusCondition.Burn => "its burn",
        StatusCondition.Freeze => "being frozen",
        StatusCondition.Sleep => "sleep",
        StatusCondition.Paralysis => "paralysis",
        StatusCondition.Confusion => "confusion",
        _ => "its condition"
    };
}
```

---

## 5. ItemDefinition Changes

### 5a. Add Parsed Effect to ItemDefinition

The current `ItemDefinition` record needs a `ParsedEffect` property so the effect is parsed once at load time rather than re-parsed every time the item is used.

```csharp
// Updated ItemDefinition.cs
public record ItemDefinition(
    int Id,
    string Name,
    string SpriteName,
    ItemCategory Category,
    int BuyPrice,
    int SellPrice,
    bool UsableInBattle,
    bool UsableOverworld,
    string? Effect = null
)
{
    /// <summary>Parsed effect, set by ItemRegistry at load time.</summary>
    public ItemEffect? ParsedEffect { get; init; } =
        ItemEffectParser.Parse(Effect);
}
```

### 5b. ItemRegistry Change

In `ItemRegistry.Initialize()`, the `ParsedEffect` is automatically populated via the `init` property on the record -- no change needed to the registry loading code. The `ItemEffectParser.Parse()` call happens inside the record constructor.

---

## 6. PartyPokemon Changes

### 6a. Replace String Status With Enum

```csharp
// Updated PartyPokemon.cs
using PokemonGreen.Core.Items; // for StatusCondition if it moves here

namespace PokemonGreen.Core.Pokemon;

public class PartyPokemon
{
    public string Nickname { get; set; } = "MissingNo";
    public string Species { get; set; } = "MissingNo";
    public int Level { get; set; } = 1;
    public int CurrentHP { get; set; } = 10;
    public int MaxHP { get; set; } = 10;
    public Gender Gender { get; set; } = Gender.Unknown;
    public StatusCondition StatusCondition { get; set; } = StatusCondition.None;
    public int? HeldItemId { get; set; }

    public float HPPercent => MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;
    public bool IsFainted => CurrentHP <= 0;

    /// <summary>Status display abbreviation for the UI.</summary>
    public string? StatusAbbreviation => StatusCondition switch
    {
        StatusCondition.None => null,
        StatusCondition.Poison => "PSN",
        StatusCondition.Burn => "BRN",
        StatusCondition.Freeze => "FRZ",
        StatusCondition.Sleep => "SLP",
        StatusCondition.Paralysis => "PAR",
        StatusCondition.Confusion => "CNF",
        StatusCondition.Fainted => "FNT",
        _ => null
    };

    /// <summary>Full heal (Pokemon Center style).</summary>
    public void HealFully()
    {
        CurrentHP = MaxHP;
        StatusCondition = StatusCondition.None;
    }
}
```

### 6b. Ripple Effect: Update References to `Status`

The old `Status` property (nullable string) is used in:
- `D:\Projects\PokemonGreen\src\PokemonGreen.Core\Pokemon\Party.cs` line 42: `Status = "FNT"` -- change to `StatusCondition = StatusCondition.Fainted` (but note: fainted is tracked by `CurrentHP <= 0`, so this may just be `CurrentHP = 0`)
- `D:\Projects\PokemonGreen\src\PokemonGreen.Core\Battle\BattlePokemon.cs` line 16: `Status` property -- update to use the same `StatusCondition` enum
- `D:\Projects\PokemonGreen\src\PokemonGreen.Core\UI\Screens\PartyScreen.cs` -- any status display rendering

---

## 7. Battle vs Overworld Usage

### Overworld Usage Flow

1. Player opens Bag (Start Menu -> Bag)
2. Player selects Medicine pouch
3. Player selects an item (e.g., Potion)
4. System calls `ItemUseHandler.CanUseItem(item, targetPokemon, inBattle: false)`
5. If the item targets a Pokemon, show the Party screen for selection
6. Player selects a Pokemon
7. System calls `ItemUseHandler.UseItem(item, targetPokemon)`
8. If `Success`, decrement `PlayerInventory` quantity, show result message
9. If the item requires a move selection (Ether), show move list first

### Battle Usage Flow

1. Player selects "Bag" from battle action menu
2. Opens Bag GUI (filtered to battle-usable items only)
3. Player selects an item
4. For items targeting a Pokemon (Potion, status cures): show party selection
5. For items targeting a move (Ether): show Pokemon selection, then move selection
6. System validates via `ItemUseHandler.CanUseItem(item, target, inBattle: true)`
7. The "use item" action is submitted as the turn action (uses the player's turn)
8. During turn resolution, `ItemUseHandler.UseItem()` is called
9. Item quantity decremented from `PlayerInventory`
10. Battle message displayed

### Key Differences

| Aspect | Overworld | Battle |
|--------|-----------|--------|
| Turn cost | None | Uses the player's turn |
| Item filter | `UsableOverworld == true` | `UsableInBattle == true` |
| Sacred Ash | Can be used | Cannot be used |
| Message display | Standard message box | Battle message window |
| PP items | Move selection via summary screen | Move selection via battle move list |
| Animation | None (instant) | Optional item-use animation |

---

## 8. Updated items.json Structure

The `items.json` effect strings need to be updated to use the structured format. Here is the complete health/recovery section with the new effect string format:

```json
[
  {"id": 100, "name": "Potion",        "sprite": "potion",        "category": "Medicine", "buyPrice": 300,  "sellPrice": 150,  "usableInBattle": true,  "usableOverworld": true,  "effect": "heal_hp_20"},
  {"id": 101, "name": "Super Potion",  "sprite": "superpotion",   "category": "Medicine", "buyPrice": 700,  "sellPrice": 350,  "usableInBattle": true,  "usableOverworld": true,  "effect": "heal_hp_50"},
  {"id": 102, "name": "Hyper Potion",  "sprite": "hyperpotion",   "category": "Medicine", "buyPrice": 1200, "sellPrice": 600,  "usableInBattle": true,  "usableOverworld": true,  "effect": "heal_hp_200"},
  {"id": 103, "name": "Max Potion",    "sprite": "maxpotion",     "category": "Medicine", "buyPrice": 2500, "sellPrice": 1250, "usableInBattle": true,  "usableOverworld": true,  "effect": "heal_hp_full"},
  {"id": 104, "name": "Full Restore",  "sprite": "fullrestore",   "category": "Medicine", "buyPrice": 3000, "sellPrice": 1500, "usableInBattle": true,  "usableOverworld": true,  "effect": "heal_full"},
  {"id": 105, "name": "Full Heal",     "sprite": "fullheal",      "category": "Medicine", "buyPrice": 600,  "sellPrice": 300,  "usableInBattle": true,  "usableOverworld": true,  "effect": "cure_status_all"},
  {"id": 106, "name": "Fresh Water",   "sprite": "freshwater",    "category": "Medicine", "buyPrice": 200,  "sellPrice": 100,  "usableInBattle": true,  "usableOverworld": true,  "effect": "heal_hp_50"},
  {"id": 107, "name": "Soda Pop",      "sprite": "sodapop",       "category": "Medicine", "buyPrice": 300,  "sellPrice": 150,  "usableInBattle": true,  "usableOverworld": true,  "effect": "heal_hp_60"},
  {"id": 108, "name": "Lemonade",      "sprite": "lemonade",      "category": "Medicine", "buyPrice": 350,  "sellPrice": 175,  "usableInBattle": true,  "usableOverworld": true,  "effect": "heal_hp_80"},
  {"id": 109, "name": "Moomoo Milk",   "sprite": "moomoomilk",    "category": "Medicine", "buyPrice": 500,  "sellPrice": 250,  "usableInBattle": true,  "usableOverworld": true,  "effect": "heal_hp_100"},
  {"id": 110, "name": "Berry Juice",   "sprite": "berryjuice",    "category": "Medicine", "buyPrice": 100,  "sellPrice": 50,   "usableInBattle": true,  "usableOverworld": true,  "effect": "heal_hp_20"},
  {"id": 111, "name": "Energy Powder", "sprite": "energypowder",  "category": "Medicine", "buyPrice": 500,  "sellPrice": 250,  "usableInBattle": true,  "usableOverworld": true,  "effect": "heal_hp_bitter_50"},
  {"id": 112, "name": "Energy Root",   "sprite": "energyroot",    "category": "Medicine", "buyPrice": 800,  "sellPrice": 400,  "usableInBattle": true,  "usableOverworld": true,  "effect": "heal_hp_bitter_200"},
  {"id": 113, "name": "Lava Cookie",   "sprite": "lavacookie",    "category": "Medicine", "buyPrice": 200,  "sellPrice": 100,  "usableInBattle": true,  "usableOverworld": true,  "effect": "cure_status_all"},
  {"id": 114, "name": "Sweet Heart",   "sprite": "sweetheart",    "category": "Medicine", "buyPrice": 0,    "sellPrice": 50,   "usableInBattle": true,  "usableOverworld": true,  "effect": "heal_hp_20"},

  {"id": 120, "name": "Antidote",      "sprite": "antidote",      "category": "Medicine", "buyPrice": 100,  "sellPrice": 50,   "usableInBattle": true,  "usableOverworld": true,  "effect": "cure_status_poison"},
  {"id": 121, "name": "Burn Heal",     "sprite": "burnheal",      "category": "Medicine", "buyPrice": 250,  "sellPrice": 125,  "usableInBattle": true,  "usableOverworld": true,  "effect": "cure_status_burn"},
  {"id": 122, "name": "Ice Heal",      "sprite": "iceheal",       "category": "Medicine", "buyPrice": 250,  "sellPrice": 125,  "usableInBattle": true,  "usableOverworld": true,  "effect": "cure_status_freeze"},
  {"id": 123, "name": "Awakening",     "sprite": "awakening",     "category": "Medicine", "buyPrice": 250,  "sellPrice": 125,  "usableInBattle": true,  "usableOverworld": true,  "effect": "cure_status_sleep"},
  {"id": 124, "name": "Parlyz Heal",   "sprite": "parlyzheal",    "category": "Medicine", "buyPrice": 200,  "sellPrice": 100,  "usableInBattle": true,  "usableOverworld": true,  "effect": "cure_status_paralysis"},
  {"id": 125, "name": "Heal Powder",   "sprite": "healpowder",    "category": "Medicine", "buyPrice": 450,  "sellPrice": 225,  "usableInBattle": true,  "usableOverworld": true,  "effect": "cure_status_all_bitter"},

  {"id": 130, "name": "Revive",        "sprite": "revive",        "category": "Medicine", "buyPrice": 1500, "sellPrice": 750,  "usableInBattle": true,  "usableOverworld": true,  "effect": "revive_50"},
  {"id": 131, "name": "Max Revive",    "sprite": "maxrevive",     "category": "Medicine", "buyPrice": 0,    "sellPrice": 2000, "usableInBattle": true,  "usableOverworld": true,  "effect": "revive_100"},
  {"id": 132, "name": "Revival Herb",  "sprite": "revivalherb",   "category": "Medicine", "buyPrice": 2800, "sellPrice": 1400, "usableInBattle": true,  "usableOverworld": true,  "effect": "revive_bitter_100"},
  {"id": 133, "name": "Sacred Ash",    "sprite": "sacredash",     "category": "Medicine", "buyPrice": 0,    "sellPrice": 10000,"usableInBattle": false, "usableOverworld": true,  "effect": "revive_all_100"},

  {"id": 140, "name": "Ether",         "sprite": "ether",         "category": "Medicine", "buyPrice": 0,    "sellPrice": 600,  "usableInBattle": true,  "usableOverworld": true,  "effect": "restore_pp_10"},
  {"id": 141, "name": "Max Ether",     "sprite": "maxether",      "category": "Medicine", "buyPrice": 0,    "sellPrice": 1000, "usableInBattle": true,  "usableOverworld": true,  "effect": "restore_pp_full"},
  {"id": 142, "name": "Elixir",        "sprite": "elixir",        "category": "Medicine", "buyPrice": 0,    "sellPrice": 1500, "usableInBattle": true,  "usableOverworld": true,  "effect": "restore_pp_all_10"},
  {"id": 143, "name": "Max Elixir",    "sprite": "maxelixir",     "category": "Medicine", "buyPrice": 0,    "sellPrice": 2500, "usableInBattle": true,  "usableOverworld": true,  "effect": "restore_pp_all_full"},

  {"id": 200, "name": "Oran Berry",    "sprite": "berry_oran",    "category": "Berry",    "buyPrice": 20,   "sellPrice": 10,   "usableInBattle": true,  "usableOverworld": true,  "effect": "heal_hp_10"},
  {"id": 201, "name": "Sitrus Berry",  "sprite": "berry_sitrus",  "category": "Berry",    "buyPrice": 20,   "sellPrice": 10,   "usableInBattle": true,  "usableOverworld": true,  "effect": "heal_hp_percent_25"},
  {"id": 210, "name": "Cheri Berry",   "sprite": "berry_cheri",   "category": "Berry",    "buyPrice": 20,   "sellPrice": 10,   "usableInBattle": true,  "usableOverworld": true,  "effect": "cure_status_paralysis"},
  {"id": 211, "name": "Chesto Berry",  "sprite": "berry_chesto",  "category": "Berry",    "buyPrice": 20,   "sellPrice": 10,   "usableInBattle": true,  "usableOverworld": true,  "effect": "cure_status_sleep"},
  {"id": 212, "name": "Pecha Berry",   "sprite": "berry_pecha",   "category": "Berry",    "buyPrice": 20,   "sellPrice": 10,   "usableInBattle": true,  "usableOverworld": true,  "effect": "cure_status_poison"},
  {"id": 213, "name": "Rawst Berry",   "sprite": "berry_rawst",   "category": "Berry",    "buyPrice": 20,   "sellPrice": 10,   "usableInBattle": true,  "usableOverworld": true,  "effect": "cure_status_burn"},
  {"id": 214, "name": "Aspear Berry",  "sprite": "berry_aspear",  "category": "Berry",    "buyPrice": 20,   "sellPrice": 10,   "usableInBattle": true,  "usableOverworld": true,  "effect": "cure_status_freeze"},
  {"id": 215, "name": "Persim Berry",  "sprite": "berry_persim",  "category": "Berry",    "buyPrice": 20,   "sellPrice": 10,   "usableInBattle": true,  "usableOverworld": true,  "effect": "cure_status_confusion"},
  {"id": 216, "name": "Lum Berry",     "sprite": "berry_lum",     "category": "Berry",    "buyPrice": 20,   "sellPrice": 10,   "usableInBattle": true,  "usableOverworld": true,  "effect": "cure_status_all"},
  {"id": 220, "name": "Leppa Berry",   "sprite": "berry_leppa",   "category": "Berry",    "buyPrice": 20,   "sellPrice": 10,   "usableInBattle": true,  "usableOverworld": true,  "effect": "restore_pp_10"}
]
```

---

## 9. Integration With Current Systems

### 9a. PlayerInventory -- RemoveItem Method

The current `PlayerInventory` has `AddItem()` but no `RemoveItem()`. Item usage requires removing one unit.

```csharp
// Add to PlayerInventory.cs
public bool RemoveItem(int itemId, int quantity = 1)
{
    var def = ItemRegistry.GetItem(itemId);
    if (def == null) return false;

    if (!_pouches.TryGetValue(def.Category, out var pouch))
        return false;

    var slot = pouch.FirstOrDefault(s => s.ItemId == itemId);
    if (slot == null || slot.Quantity < quantity)
        return false;

    slot.Quantity -= quantity;
    if (slot.Quantity <= 0)
        pouch.Remove(slot);
    return true;
}
```

### 9b. UseItem Convenience Method on PlayerInventory

```csharp
// Add to PlayerInventory.cs
public ItemUseResult? UseItemOnPokemon(int itemId, PartyPokemon target,
    bool inBattle, int? moveIndex = null)
{
    var def = ItemRegistry.GetItem(itemId);
    if (def == null)
        return new ItemUseResult(false, "Unknown item.");

    if (!ItemUseHandler.CanUseItem(def, target, inBattle, moveIndex))
        return new ItemUseResult(false, $"It won't have any effect.");

    var result = ItemUseHandler.UseItem(def, target, moveIndex);
    if (result.Success)
        RemoveItem(itemId, 1);
    return result;
}
```

### 9c. BattlePokemon Sync

After item use in battle, the `BattlePokemon` wrapper needs to sync from the underlying `PartyPokemon`. Add a method:

```csharp
// Add to BattlePokemon.cs
public void SyncFromParty(PartyPokemon source)
{
    CurrentHP = source.CurrentHP;
    Status = source.StatusAbbreviation;
    // DisplayHP will smoothly animate toward the new CurrentHP
}
```

### 9d. Updated Test Inventory

```csharp
// Updated CreateTestInventory in PlayerInventory.cs
public static PlayerInventory CreateTestInventory()
{
    var inv = new PlayerInventory();
    // Medicine
    inv.AddItem(100, 5);   // Potion
    inv.AddItem(101, 3);   // Super Potion
    inv.AddItem(102, 1);   // Hyper Potion
    inv.AddItem(120, 3);   // Antidote
    inv.AddItem(121, 2);   // Burn Heal
    inv.AddItem(124, 2);   // Parlyz Heal
    inv.AddItem(105, 1);   // Full Heal
    inv.AddItem(130, 2);   // Revive
    // Pokeballs
    inv.AddItem(0, 10);    // Poke Ball
    inv.AddItem(1, 3);     // Great Ball
    // Berries
    inv.AddItem(200, 5);   // Oran Berry
    inv.AddItem(201, 3);   // Sitrus Berry
    inv.AddItem(210, 2);   // Cheri Berry
    inv.AddItem(212, 2);   // Pecha Berry
    return inv;
}
```

---

## 10. Code Changes Summary

### New Files to Create

| File | Purpose |
|------|---------|
| `src/PokemonGreen.Core/Pokemon/StatusCondition.cs` | Status condition enum |
| `src/PokemonGreen.Core/Items/ItemEffectType.cs` | Item effect type enum |
| `src/PokemonGreen.Core/Items/ItemEffect.cs` | Parsed effect data record |
| `src/PokemonGreen.Core/Items/ItemEffectParser.cs` | Effect string to ItemEffect converter |
| `src/PokemonGreen.Core/Items/ItemUseHandler.cs` | Core item use logic, CanUseItem/UseItem |

### Files to Modify

| File | Change |
|------|--------|
| `src/PokemonGreen.Core/Items/ItemDefinition.cs` | Add `ParsedEffect` init property |
| `src/PokemonGreen.Core/Pokemon/PartyPokemon.cs` | Replace `string? Status` with `StatusCondition` enum, add `HealFully()`, add `StatusAbbreviation` |
| `src/PokemonGreen.Core/Items/PlayerInventory.cs` | Add `RemoveItem()`, add `UseItemOnPokemon()`, update `CreateTestInventory()` |
| `src/PokemonGreen.Core/Battle/BattlePokemon.cs` | Change `Status` to use enum, add `SyncFromParty()` |
| `src/PokemonGreen.Core/Pokemon/Party.cs` | Update test data to use `StatusCondition` enum instead of string |
| `src/PokemonGreen.Core/UI/Screens/PartyScreen.cs` | Update status display to use `StatusAbbreviation` instead of raw `Status` |
| `src/PokemonGreen.Assets/Data/items.json` | Replace current entries with complete catalog from Section 8 |

### Files NOT Changed (but will need future work)

| File | Future Need |
|------|-------------|
| Battle action menu | Add "Bag" option that opens item selection during battle |
| Bag GUI / UI screen | Item usage flow: select item -> select target -> show result |
| Move/PP system | `PartyPokemon` needs a moveset with PP tracking for Ether/Elixir to work |
| Friendship system | Bitter medicine items should lower friendship (not yet implemented) |
| Held item auto-use | Berries held by Pokemon should trigger automatically in battle |

---

## 11. Mapping to Old Engine

### Old Engine Item IDs vs New Engine Item IDs

| Old Engine ID | Old Name | New Engine ID | New Name |
|---------------|----------|---------------|----------|
| 17 | Potion | 100 | Potion |
| 26 | SuperPotion | 101 | Super Potion |
| 25 | HyperPotion | 102 | Hyper Potion |
| 24 | MaxPotion | 103 | Max Potion |
| 23 | FullRestore | 104 | Full Restore |
| 27 | FullHeal | 105 | Full Heal |
| 30 | FreshWater | 106 | Fresh Water |
| 31 | SodaPop | 107 | Soda Pop |
| 32 | Lemonade | 108 | Lemonade |
| 33 | MoomooMilk | 109 | Moomoo Milk |
| 43 | BerryJuice | 110 | Berry Juice |
| 34 | EnergyPowder | 111 | Energy Powder |
| 35 | EnergyRoot | 112 | Energy Root |
| 42 | LavaCookie | 113 | Lava Cookie |
| 134 | SweetHeart | 114 | Sweet Heart |
| 18 | Antidote | 120 | Antidote |
| 19 | BurnHeal | 121 | Burn Heal |
| 20 | IceHeal | 122 | Ice Heal |
| 21 | Awakening | 123 | Awakening |
| 22 | ParlyzHeal | 124 | Parlyz Heal |
| 36 | HealPowder | 125 | Heal Powder |
| 28 | Revive | 130 | Revive |
| 29 | MaxRevive | 131 | Max Revive |
| 37 | RevivalHerb | 132 | Revival Herb |
| 44 | SacredAsh | 133 | Sacred Ash |
| 38 | Ether | 140 | Ether |
| 39 | MaxEther | 141 | Max Ether |
| 40 | Elixir | 142 | Elixir |
| 41 | MaxElixir | 143 | Max Elixir |
| 155 | OranBerry | 200 | Oran Berry |
| 158 | SitrusBerry | 201 | Sitrus Berry |
| 149 | CheriBerry | 210 | Cheri Berry |
| 150 | ChestoBerry | 211 | Chesto Berry |
| 151 | PechaBerry | 212 | Pecha Berry |
| 152 | RawstBerry | 213 | Rawst Berry |
| 153 | AspearBerry | 214 | Aspear Berry |
| 156 | PersimBerry | 215 | Persim Berry |
| 157 | LumBerry | 216 | Lum Berry |
| 154 | LeppaBerry | 220 | Leppa Berry |

### Key Differences From Old Engine

1. **No PBE dependency.** The old engine delegates all item effect logic to `PokemonBattleEngine.dll`. Our new engine handles item effects directly in `ItemUseHandler`.

2. **Data-driven effects.** The old engine hardcodes item IDs in enums with no associated effect data. Our system stores effects as parseable strings in `items.json`, converted to structured `ItemEffect` records at load time.

3. **Unified effect handler.** The old engine has separate paths for overworld healing (`PartyPokemon.HealFully()`) and battle item usage (PBE library). Our `ItemUseHandler` serves both contexts.

4. **Numbered ID ranges.** The old engine uses Gen 5 sequential IDs (17=Potion, 18=Antidote...). Our system uses ranged IDs (100-199 for medicine) for clearer organization and room for expansion.

---

## 12. Implementation Priority

### Phase 1 (Minimum Viable)
- Create `StatusCondition` enum
- Update `PartyPokemon` to use the enum
- Create `ItemEffect`, `ItemEffectType`, `ItemEffectParser`
- Implement `ItemUseHandler` for HP healing and status curing only
- Update `items.json` with the complete medicine catalog
- Add `RemoveItem()` to `PlayerInventory`

### Phase 2 (Revive and Full Restore)
- Implement revive logic in `ItemUseHandler`
- Add Sacred Ash party-wide revive
- Add `UseItemOnPokemon()` convenience method

### Phase 3 (Battle Integration)
- Hook item usage into the battle action menu
- Sync `BattlePokemon` after item use
- Handle turn consumption for battle item use

### Phase 4 (PP System)
- Add moveset with PP tracking to `PartyPokemon`
- Implement Ether/Max Ether/Elixir/Max Elixir in `ItemUseHandler`

### Phase 5 (Berries and Held Items)
- Implement held berry auto-use in battle
- Add friendship modification for bitter medicines

---

## Appendix A: Canonical Heal Amounts (Gen 5 Reference)

These are the standard heal amounts from the mainline games that our catalog follows:

| Item | HP Restored |
|------|-------------|
| Potion | 20 |
| Super Potion | 50 |
| Hyper Potion | 200 |
| Max Potion | Full |
| Full Restore | Full + cure all |
| Fresh Water | 50 |
| Soda Pop | 60 |
| Lemonade | 80 |
| Moomoo Milk | 100 |
| Berry Juice | 20 |
| Energy Powder | 50 |
| Energy Root | 200 |
| Oran Berry | 10 |
| Sitrus Berry | 25% max HP |
| Sweet Heart | 20 |
| Lava Cookie | Cure all status |
| Revive | 50% max HP |
| Max Revive | 100% max HP |
| Revival Herb | 100% max HP |
| Sacred Ash | Revive all, 100% max HP |

## Appendix B: Effect String Grammar

```
effect_string := heal_effect | cure_effect | revive_effect | pp_effect | null

heal_effect   := "heal_hp_" amount
               | "heal_hp_full"
               | "heal_hp_percent_" number
               | "heal_hp_bitter_" amount
               | "heal_full"

cure_effect   := "cure_status_" status_name
               | "cure_status_all"
               | "cure_status_all_bitter"
               | "heal_status_all"

revive_effect := "revive_" number
               | "revive_all_" number
               | "revive_bitter_" number

pp_effect     := "restore_pp_" amount
               | "restore_pp_full"
               | "restore_pp_all_" amount
               | "restore_pp_all_full"

amount        := number | "full"
number        := [0-9]+
status_name   := "poison" | "burn" | "freeze" | "sleep" | "paralysis" | "confusion"
```
