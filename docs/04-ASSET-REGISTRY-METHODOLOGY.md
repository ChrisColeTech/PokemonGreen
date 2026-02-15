# Asset Registry Methodology

**Date:** 2026-02-14
**Purpose:** Establish a consistent pattern for wiring up game assets (Items, NPCs, etc.)

---

## 1. Overview

This document defines the methodology for adding new asset types to PokemonGreen. The goal is to create a repeatable, documented process that makes it easy to add items, NPCs, and other game entities without ad-hoc solutions.

### Core Principles

1. **Registry Pattern** - All entities use a static registry with ID-based lookups (like `TileRegistry`)
2. **Definition Records** - Each entity type has a `*Definition` record describing its properties
3. **Embedded Resources** - Assets are compiled into the assembly via `PokemonGreen.Assets` project
4. **AssetLoader Integration** - Consistent loading through `AssetLoader` with caching in `TextureStore`

---

## 2. Directory Structure

```
PokemonGreen/
├── Assets/                          # Source assets (not compiled)
│   ├── Items/                       # Item SVGs
│   │   ├── pokeball.svg
│   │   ├── potion.svg
│   │   └── ...
│   ├── NPCs/                        # NPC spritesheets + metadata
│   │   ├── pokemon_youngster.preset.spritesheet.png
│   │   ├── pokemon_youngster.preset.character.json
│   │   └── ...
│   ├── Sprites/                     # Tile/decoration SVGs
│   └── player/                      # Player animation frames
│
├── src/
│   ├── PokemonGreen.Assets/         # Embedded resources project
│   │   ├── Sprites/                 # Tile PNGs (embedded)
│   │   ├── Player/                  # Player PNGs (embedded)
│   │   ├── Items/                   # Item PNGs (embedded) ← NEW
│   │   └── NPCs/                    # NPC PNGs + JSON (embedded) ← NEW
│   │
│   └── PokemonGreen.Core/
│       ├── Items/                   # Item system ← NEW
│       │   ├── ItemCategory.cs
│       │   ├── ItemDefinition.cs
│       │   └── ItemRegistry.cs
│       │
│       ├── NPCs/                    # NPC system ← NEW
│       │   ├── NPCRole.cs
│       │   ├── NPCDefinition.cs
│       │   └── NPCRegistry.cs
│       │
│       └── Rendering/
│           ├── AssetLoader.cs       # Add LoadItemSprite(), LoadNPCSprite()
│           └── TextureStore.cs      # Add GetItemTexture(), GetNPCTexture()
│
└── tools/
    └── convert_sprites.js           # Extend for Items/NPCs
```

---

## 3. Asset Categories

### 3.1 Items

**Definition:** Single-sprite entities that can be collected, used, sold, or held.

**ID Range:** 0-999 (organized by category)

| Category | ID Range | Examples |
|----------|----------|----------|
| Pokeballs | 0-99 | Pokeball, Great Ball, Ultra Ball, Master Ball |
| Medicine | 100-199 | Potion, Super Potion, Hyper Potion, Full Restore |
| Battle | 200-299 | X Attack, X Defend, Guard Spec |
| Berries | 300-399 | Oran Berry, Pecha Berry, Sitrus Berry |
| Key Items | 400-499 | Bicycle, Fishing Rod, Town Map |
| TMs/HMs | 500-599 | TM01, HM01 Cut, HM02 Fly |
| Evolution | 600-699 | Fire Stone, Water Stone, Thunder Stone |
| Held | 700-799 | Leftovers, Quick Claw, King's Rock |
| Valuables | 800-899 | Nugget, Pearl, Rare Candy |
| Mail | 900-999 | Orange Mail, Harbor Mail |

**ItemDefinition Fields:**
```csharp
public record ItemDefinition(
    int Id,
    string Name,
    string SpriteName,
    ItemCategory Category,
    int Price,           // Buy price (0 = not purchasable)
    int SellPrice,       // Sell price (typically Price / 2)
    bool UsableInBattle,
    bool UsableInField,
    string? Effect       // Effect identifier (e.g., "heal_20_hp", "catch_rate_1x")
);
```

### 3.2 NPCs

**Definition:** Characters in the world with spritesheets and animation data.

**ID Range:** 0-999 (organized by role)

| Role | ID Range | Examples |
|------|----------|----------|
| Civilian | 0-99 | Villager, Child, Elder, Tourist |
| Service | 100-199 | Nurse, Clerk, Daycare Staff |
| Trainer | 200-299 | Youngster, Lass, Hiker, Bug Catcher |
| Story | 300-399 | Rival, Professor, Mom, Friend |
| Gym | 400-499 | Gym Leader, Gym Trainer |
| Elite | 500-599 | Elite Four, Champion |
| Villain | 600-699 | Team Rocket Grunt, Boss |
| Specialist | 700-799 | Scientist, Fisher, Ranger |

**NPCDefinition Fields:**
```csharp
public record NPCDefinition(
    int Id,
    string Name,
    string SpriteName,       // Base sprite name without extension
    NPCRole Role,
    int FrameWidth,          // Pixels per frame (from JSON)
    int FrameHeight,         // Pixels per frame (from JSON)
    int FramesPerDirection,  // Usually 4 or 8
    string[] Animations      // Available animations (walk, idle, run, etc.)
);
```

---

## 4. Implementation Steps

### Step 1: Convert Source Assets

For **Items** (SVG → PNG):
```bash
# Add to convert_sprites.js or create convert_items.js
node tools/convert_sprites.js --items
```

For **NPCs** (already PNG + JSON):
```bash
# Copy directly to src/PokemonGreen.Assets/NPCs/
cp Assets/NPCs/pokemon_youngster.preset.spritesheet.png src/PokemonGreen.Assets/NPCs/youngster.png
cp Assets/NPCs/pokemon_youngster.preset.character.json src/PokemonGreen.Assets/NPCs/youngster.json
```

### Step 2: Create Definition Record

```csharp
// src/PokemonGreen.Core/Items/ItemDefinition.cs
namespace PokemonGreen.Core.Items;

public enum ItemCategory
{
    Pokeball = 0,
    Medicine = 1,
    Battle = 2,
    Berry = 3,
    KeyItem = 4,
    TM = 5,
    HM = 6,
    Evolution = 7,
    Held = 8,
    Valuable = 9,
    Mail = 10
}

public record ItemDefinition(
    int Id,
    string Name,
    string SpriteName,
    ItemCategory Category,
    int Price,
    int SellPrice,
    bool UsableInBattle,
    bool UsableInField,
    string? Effect = null
);
```

### Step 3: Create Registry

```csharp
// src/PokemonGreen.Core/Items/ItemRegistry.cs
namespace PokemonGreen.Core.Items;

public static class ItemRegistry
{
    private static readonly Dictionary<int, ItemDefinition> _items = new()
    {
        // Pokeballs (0-99)
        [0] = new(0, "Poke Ball", "pokeball", ItemCategory.Pokeball, 200, 100, false, false, "catch_rate_1x"),
        [1] = new(1, "Great Ball", "greatball", ItemCategory.Pokeball, 600, 300, false, false, "catch_rate_1.5x"),
        [2] = new(2, "Ultra Ball", "ultraball", ItemCategory.Pokeball, 1200, 600, false, false, "catch_rate_2x"),
        [3] = new(3, "Master Ball", "masterball", ItemCategory.Pokeball, 0, 0, false, false, "catch_rate_infinite"),
        
        // Medicine (100-199)
        [100] = new(100, "Potion", "potion", ItemCategory.Medicine, 300, 150, true, true, "heal_20_hp"),
        [101] = new(101, "Super Potion", "superpotion", ItemCategory.Medicine, 700, 350, true, true, "heal_50_hp"),
        [102] = new(102, "Hyper Potion", "hyperpotion", ItemCategory.Medicine, 1500, 750, true, true, "heal_200_hp"),
        [103] = new(103, "Full Restore", "fullrestore", ItemCategory.Medicine, 3000, 1500, true, true, "heal_full"),
        
        // Add more items...
    };
    
    public static ItemDefinition? GetItem(int id) =>
        _items.TryGetValue(id, out var item) ? item : null;
    
    public static IEnumerable<ItemDefinition> GetItemsByCategory(ItemCategory category) =>
        _items.Values.Where(i => i.Category == category);
    
    public static IEnumerable<ItemDefinition> AllItems => _items.Values;
}
```

### Step 4: Update AssetLoader

```csharp
// Add to AssetLoader.cs

public static Stream? GetItemSpriteStream(string name)
{
    var assembly = typeof(AssetLoader).Assembly;
    var resourceName = $"PokemonGreen.Assets.Items.{name}.png";
    return assembly.GetManifestResourceStream(resourceName);
}

public static Texture2D? LoadItemSprite(string name)
{
    if (_graphicsDevice == null)
        throw new InvalidOperationException("AssetLoader must be initialized first.");
    
    using var stream = GetItemSpriteStream(name);
    if (stream == null)
        return null;
    
    using var memStream = new MemoryStream();
    stream.CopyTo(memStream);
    memStream.Position = 0;
    
    return LoadTextureFromPngStream(memStream, _graphicsDevice, $"Items.{name}");
}

public static Stream? GetNPCDataStream(string name)
{
    var assembly = typeof(AssetLoader).Assembly;
    var resourceName = $"PokemonGreen.Assets.NPCs.{name}.json";
    return assembly.GetManifestResourceStream(resourceName);
}

public static Texture2D? LoadNPCSprite(string name)
{
    if (_graphicsDevice == null)
        throw new InvalidOperationException("AssetLoader must be initialized first.");
    
    using var stream = GetNPCSpriteStream(name);
    if (stream == null)
        return null;
    
    using var memStream = new MemoryStream();
    stream.CopyTo(memStream);
    memStream.Position = 0;
    
    return LoadTextureFromPngStream(memStream, _graphicsDevice, $"NPCs.{name}");
}

private static Stream? GetNPCSpriteStream(string name)
{
    var assembly = typeof(AssetLoader).Assembly;
    var resourceName = $"PokemonGreen.Assets.NPCs.{name}.png";
    return assembly.GetManifestResourceStream(resourceName);
}
```

### Step 5: Update TextureStore

```csharp
// Add to TextureStore.cs

public static Texture2D? GetItemTexture(int itemId, string spriteName)
{
    string cacheKey = $"item_{itemId}";
    if (_cache.TryGetValue(cacheKey, out var cached))
        return cached;
    
    var texture = AssetLoader.LoadItemSprite(spriteName);
    if (texture != null)
        _cache[cacheKey] = texture;
    
    return texture;
}

public static Texture2D? GetNPCTexture(int npcId, string spriteName)
{
    string cacheKey = $"npc_{npcId}";
    if (_cache.TryGetValue(cacheKey, out var cached))
        return cached;
    
    var texture = AssetLoader.LoadNPCSprite(spriteName);
    if (texture != null)
        _cache[cacheKey] = texture;
    
    return texture;
}
```

---

## 5. Core Assets to Implement First

### Items (8 items)

| ID | Name | Category | Purpose |
|----|------|----------|---------|
| 0 | Poke Ball | Pokeball | Basic catching |
| 1 | Great Ball | Pokeball | Better catch rate |
| 2 | Ultra Ball | Pokeball | Best catch rate |
| 3 | Master Ball | Pokeball | Guaranteed catch |
| 100 | Potion | Medicine | Heal 20 HP |
| 101 | Super Potion | Medicine | Heal 50 HP |
| 102 | Hyper Potion | Medicine | Heal 200 HP |
| 103 | Full Restore | Medicine | Full heal + status |

### NPCs (3 NPCs)

| ID | Name | Role | Purpose |
|----|------|------|---------|
| 0 | Youngster | Trainer | Basic trainer type |
| 100 | Nurse | Service | Healing station NPC |
| 200 | Hiker | Trainer | Gym trainer type |

---

## 6. Naming Conventions

### File Names

| Asset Type | Source Pattern | Embedded Pattern |
|------------|----------------|------------------|
| Items | `pokeball.svg` | `pokeball.png` |
| NPCs | `pokemon_youngster.preset.spritesheet.png` | `youngster.png` |
| NPC Data | `pokemon_youngster.preset.character.json` | `youngster.json` |

### Code Names

| Pattern | Example |
|---------|---------|
| Sprite names | `pokeball`, `greatball`, `youngster`, `nurse_a` |
| Registry IDs | Sequential within category (0, 1, 2... or 100, 101, 102...) |
| Effect strings | `heal_20_hp`, `catch_rate_1.5x`, `cure_poison` |

---

## 7. Future Extensions

### Pokemon Registry (Next Phase)

```csharp
public record PokemonDefinition(
    int Id,              // Pokedex number
    string Name,
    string SpriteName,
    int BaseHP,
    int BaseAttack,
    int BaseDefense,
    int BaseSpAttack,
    int BaseSpDefense,
    int BaseSpeed,
    string Type1,
    string? Type2,
    int CatchRate,
    int BaseExpYield
);
```

### Move Registry (Future)

```csharp
public record MoveDefinition(
    int Id,
    string Name,
    string Type,
    string Category,     // Physical, Special, Status
    int Power,
    int Accuracy,
    int PP,
    string? Effect
);
```

---

## 8. Checklist for Adding New Assets

1. [ ] Add source files to `Assets/<Category>/`
2. [ ] Convert/copy to `src/PokemonGreen.Assets/<Category>/`
3. [ ] Verify embedded resource names with `AssetLoader.GetEmbeddedResourceNames()`
4. [ ] Add `*Definition` record in `src/PokemonGreen.Core/<Category>/`
5. [ ] Add enum for categories/roles if needed
6. [ ] Create `*Registry.cs` with static dictionary
7. [ ] Add loader method to `AssetLoader.cs`
8. [ ] Add getter to `TextureStore.cs`
9. [ ] Test loading via debug output
10. [ ] Update this document with new ID assignments

---

## 9. Related Documents

- `00-REBUILD-PLAN.md` - Overall project structure
- `02-ASSET-REFACTOR-LESSONS.md` - Asset pipeline history
- `03-UI-RENDERING-LESSONS.md` - Rendering and transparency
