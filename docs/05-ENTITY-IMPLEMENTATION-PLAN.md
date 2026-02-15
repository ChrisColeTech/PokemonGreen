# Entity & Tile Asset Implementation Plan

**Date:** 2026-02-14
**Goal:** Complete both tile-based and entity-based NPC/Item systems

---

## Overview

Two parallel systems for NPCs and Items:

| System | Use Case | Complexity |
|--------|----------|------------|
| **Tile-Based** | Static decorations, simple pickups | Low |
| **Entity-Based** | Moving NPCs, dialogue, complex items | High |

---

## Phase 1: Complete Tile Registry

**Goal:** Wire up existing TileRegistry NPC/Item IDs with sprites and basic rendering.

### 1.1 Map TileRegistry IDs to Sprites

Update `TileRegistry.cs` to reference actual sprites:

```
NPCs (48-55):
- 48: NPC → npc_villager_male sprite
- 49: ServiceNPC → npc_clerk sprite  
- 50: Nurse → npc_nurse sprite
- 51: Clerk → npc_clerk sprite
- 52: GymGuide → npc_gym_guide sprite
- 53: Rival → npc_rival sprite
- 54: Professor → npc_professor sprite
- 55: Mom → npc_mom sprite

Trainers (56-71):
- 56-59: Trainer directions → npc_trainer sprite
- 60: GymTrainer → npc_gym_trainer sprite
- 61: GymLeader → npc_gym_leader sprite
- etc.

Items (96-103):
- 96: Pokeball → pokeball sprite
- 97: Potion → potion sprite
- etc.
```

### 1.2 Add Tile-to-Sprite Mapping

Create `TileSpriteMapping.cs`:

```csharp
public static class TileSpriteMapping
{
    private static readonly Dictionary<int, string> _sprites = new()
    {
        // NPCs
        [48] = "npc_villager_male",
        [50] = "npc_nurse",
        [51] = "npc_clerk",
        // Trainers
        [56] = "npc_trainer",
        [60] = "npc_gym_trainer",
        // Items
        [96] = "pokeball",
        [97] = "potion",
        // ... etc
    };
    
    public static string? GetSpriteName(int tileId) =>
        _sprites.TryGetValue(tileId, out var name) ? name : null;
}
```

### 1.3 Update TileRenderer

Modify `TileRenderer.DrawTile()` to handle NPCs/Items:

```csharp
// In DrawTile(), after existing tileId == 72 check:

if (tileId >= 48 && tileId <= 71) // NPCs and Trainers
{
    var spriteName = TileSpriteMapping.GetSpriteName(tileId);
    if (spriteName != null)
    {
        var npcTexture = TextureStore.GetTexture(spriteName);
        // Draw NPC centered on tile, slightly larger
    }
    return;
}

if (tileId >= 96 && tileId <= 103) // Items
{
    var spriteName = TileSpriteMapping.GetSpriteName(tileId);
    if (spriteName != null)
    {
        var itemTexture = TextureStore.GetItemTexture(tileId, spriteName);
        // Draw item centered on tile
    }
    return;
}
```

### 1.4 Copy Remaining NPC Sprites

Copy NPC spritesheets from `Assets/NPCs/` to `src/PokemonGreen.Assets/NPCs/`:

```
npc_villager_male.png
npc_clerk.png
npc_gym_trainer.png
npc_gym_leader.png
npc_rival.png
npc_professor.png
npc_mom.png
```

### 1.5 Deliverables

- [ ] `TileSpriteMapping.cs` created
- [ ] TileRenderer handles NPC tiles (48-71)
- [ ] TileRenderer handles Item tiles (96-103)
- [ ] Core NPC sprites copied
- [ ] NPCs render on map when placed as overlay tiles
- [ ] Items render on map when placed as overlay tiles

---

## Phase 2: Entity System Foundation

**Goal:** Create entity infrastructure for dynamic NPCs and Items.

### 2.1 Entity Base Class

```csharp
// src/PokemonGreen.Core/Entities/Entity.cs
namespace PokemonGreen.Core.Entities;

public abstract class Entity
{
    public int Id { get; }
    public string Name { get; }
    public float X { get; set; }  // World position (pixels)
    public float Y { get; set; }
    public int TileX => (int)(X / GameWorld.TileSize);
    public int TileY => (int)(Y / GameWorld.TileSize);
    public string SpriteName { get; }
    public bool IsActive { get; set; } = true;
    
    protected Entity(int id, string name, string spriteName, float x, float y)
    {
        Id = id;
        Name = name;
        SpriteName = spriteName;
        X = x;
        Y = y;
    }
    
    public abstract void Update(float deltaTime);
    public abstract void Interact(Player player);
}
```

### 2.2 NPC Entity

```csharp
// src/PokemonGreen.Core/Entities/NPCEntity.cs
namespace PokemonGreen.Core.Entities;

public class NPCEntity : Entity
{
    public Direction Facing { get; set; } = Direction.Down;
    public string[] Dialogue { get; set; } = Array.Empty<string>();
    public bool CanMove { get; set; }
    public NPCRole Role { get; }
    
    public NPCEntity(int id, string name, string spriteName, float x, float y, NPCRole role)
        : base(id, name, spriteName, x, y)
    {
        Role = role;
    }
    
    public override void Update(float deltaTime)
    {
        // Idle animation, wandering, etc.
    }
    
    public override void Interact(Player player)
    {
        // Face player, trigger dialogue
    }
}
```

### 2.3 Item Entity

```csharp
// src/PokemonGreen.Core/Entities/ItemEntity.cs
namespace PokemonGreen.Core.Entities;

public class ItemEntity : Entity
{
    public int ItemId { get; }
    public int Quantity { get; }
    public bool Visible { get; set; } = true;
    
    public ItemEntity(int id, int itemId, int quantity, float x, float y)
        : base(id, $"Item_{itemId}", ItemRegistry.GetItem(itemId)?.SpriteName ?? "pokeball", x, y)
    {
        ItemId = itemId;
        Quantity = quantity;
    }
    
    public override void Update(float deltaTime) { }
    
    public override void Interact(Player player)
    {
        // Add to inventory, remove from world
        IsActive = false;
    }
}
```

### 2.4 EntityManager

```csharp
// src/PokemonGreen.Core/Entities/EntityManager.cs
namespace PokemonGreen.Core.Entities;

public class EntityManager
{
    private readonly List<Entity> _entities = new();
    private int _nextId = 0;
    
    public IReadOnlyList<Entity> Entities => _entities;
    
    public NPCEntity SpawnNPC(string name, string spriteName, int tileX, int tileY, NPCRole role)
    {
        var npc = new NPCEntity(_nextId++, name, spriteName, 
            tileX * GameWorld.TileSize, tileY * GameWorld.TileSize, role);
        _entities.Add(npc);
        return npc;
    }
    
    public ItemEntity SpawnItem(int itemId, int quantity, int tileX, int tileY)
    {
        var item = new ItemEntity(_nextId++, itemId, quantity,
            tileX * GameWorld.TileSize, tileY * GameWorld.TileSize);
        _entities.Add(item);
        return item;
    }
    
    public void Update(float deltaTime)
    {
        foreach (var entity in _entities.Where(e => e.IsActive))
            entity.Update(deltaTime);
        
        _entities.RemoveAll(e => !e.IsActive);
    }
    
    public Entity? GetEntityAtTile(int tileX, int tileY)
    {
        return _entities.FirstOrDefault(e => e.IsActive && e.TileX == tileX && e.TileY == tileY);
    }
    
    public IEnumerable<Entity> GetEntitiesInBounds(Rectangle bounds)
    {
        return _entities.Where(e => e.IsActive && bounds.Contains((int)e.X, (int)e.Y));
    }
    
    public void Clear() => _entities.Clear();
}
```

### 2.5 EntityRenderer

```csharp
// src/PokemonGreen.Core/Rendering/EntityRenderer.cs
namespace PokemonGreen.Core.Rendering;

public static class EntityRenderer
{
    public static void DrawEntities(
        SpriteBatch spriteBatch,
        EntityManager entityManager,
        Systems.Camera camera,
        int tileSize)
    {
        var bounds = camera.Bounds;
        
        foreach (var entity in entityManager.Entities.Where(e => e.IsActive))
        {
            // Culling
            if (entity.X < bounds.Left - tileSize || entity.X > bounds.Right + tileSize ||
                entity.Y < bounds.Top - tileSize || entity.Y > bounds.Bottom + tileSize)
                continue;
            
            var texture = GetEntityTexture(entity);
            if (texture == null) continue;
            
            // Center on tile
            int drawX = (int)entity.X - texture.Width / 2 + tileSize / 2;
            int drawY = (int)entity.Y - texture.Height + tileSize;
            
            spriteBatch.Draw(texture, new Vector2(drawX, drawY), Color.White);
        }
    }
    
    private static Texture2D? GetEntityTexture(Entity entity)
    {
        return entity switch
        {
            NPCEntity npc => TextureStore.GetNPCTexture(npc.Id, npc.SpriteName),
            ItemEntity item => TextureStore.GetItemTexture(item.ItemId, item.SpriteName),
            _ => null
        };
    }
}
```

### 2.6 Deliverables

- [ ] `Entity.cs` base class
- [ ] `NPCEntity.cs` 
- [ ] `ItemEntity.cs`
- [ ] `EntityManager.cs`
- [ ] `EntityRenderer.cs`
- [ ] Entities render with Y-sorting relative to player
- [ ] Basic interaction framework

---

## Phase 3: Spawn Point System

**Goal:** Parse spawn markers from map data and create entities at runtime.

### 3.1 Spawn Point Definitions

Add to `TileRegistry.cs` or create `SpawnPoints.cs`:

```csharp
public static class SpawnPoints
{
    // Spawn marker tiles (invisible at runtime)
    public const int NPCSpawnStart = 48;      // NPCs 48-55 are spawn markers
    public const int TrainerSpawnStart = 56;  // Trainers 56-71 are spawn markers
    public const int ItemSpawnStart = 96;     // Items 96-103 are spawn markers
    
    public static bool IsNPCSpawn(int tileId) => tileId >= 48 && tileId <= 55;
    public static bool IsTrainerSpawn(int tileId) => tileId >= 56 && tileId <= 71;
    public static bool IsItemSpawn(int tileId) => tileId >= 96 && tileId <= 103;
}
```

### 3.2 Spawn Data Format

Option A: Embedded in map (simple)
```
Overlay tile at (5, 10) = ID 50 (Nurse)
→ On load, create Nurse entity at that position, clear tile
```

Option B: Separate spawn file (flexible)
```
Maps/spawn_testmap.json:
{
  "npcs": [
    { "tileX": 5, "tileY": 10, "type": "nurse", "dialogue": ["Welcome!"] }
  ],
  "items": [
    { "tileX": 12, "tileY": 8, "itemId": 0, "quantity": 1 }
  ]
}
```

### 3.3 Map Load Integration

```csharp
// In GameWorld or MapLoader
public void LoadMap(TileMap map, EntityManager entityManager)
{
    // Scan overlay layer for spawn points
    for (int y = 0; y < map.Height; y++)
    {
        for (int x = 0; x < map.Width; x++)
        {
            int overlayId = map.GetOverlayTile(x, y);
            
            if (SpawnPoints.IsNPCSpawn(overlayId))
            {
                var npcType = GetNPCTypeFromTileId(overlayId);
                entityManager.SpawnNPC(npcType.Name, npcType.Sprite, x, y, npcType.Role);
                map.SetOverlayTile(x, y, -1); // Clear spawn marker
            }
            else if (SpawnPoints.IsItemSpawn(overlayId))
            {
                var itemId = GetItemIdFromTileId(overlayId);
                entityManager.SpawnItem(itemId, 1, x, y);
                map.SetOverlayTile(x, y, -1);
            }
        }
    }
}
```

### 3.4 Deliverables

- [ ] `SpawnPoints.cs` with marker detection
- [ ] Spawn parsing on map load
- [ ] Spawn markers converted to entities
- [ ] Spawn markers cleared from tile data
- [ ] Entities persist across map transitions

---

## Phase 4: Y-Sorted Rendering Integration

**Goal:** Render entities with proper depth relative to tiles and player.

### 4.1 Draw Order

```
1. DrawBaseTiles()        - Terrain
2. DrawOverlaysBehindPlayer() - Overlays above player's feet
3. DrawEntitiesBehindPlayer() - Entities with Y < playerTileY
4. DrawPlayer()           - Player sprite
5. DrawEntitiesInFrontOfPlayer() - Entities with Y >= playerTileY  
6. DrawOverlaysInFrontOfPlayer() - Overlays at/below player's feet
```

### 4.2 EntityRenderer Update

```csharp
public static void DrawEntitiesBehindPlayer(
    SpriteBatch spriteBatch,
    EntityManager entityManager,
    Systems.Camera camera,
    int playerTileY,
    int tileSize)
{
    foreach (var entity in entityManager.Entities
        .Where(e => e.IsActive && e.TileY < playerTileY))
    {
        DrawEntity(spriteBatch, entity, tileSize);
    }
}

public static void DrawEntitiesInFrontOfPlayer(
    SpriteBatch spriteBatch,
    EntityManager entityManager,
    Systems.Camera camera,
    int playerTileY,
    int tileSize)
{
    foreach (var entity in entityManager.Entities
        .Where(e => e.IsActive && e.TileY >= playerTileY))
    {
        DrawEntity(spriteBatch, entity, tileSize);
    }
}
```

### 4.3 Game1.cs Integration

```csharp
protected override void Draw(GameTime gameTime)
{
    _graphicsDevice.Clear(Color.Black);
    
    _spriteBatch.Begin(/* ... */);
    
    TileRenderer.DrawBaseTiles(_spriteBatch, _map, _camera);
    TileRenderer.DrawOverlaysBehindPlayer(_spriteBatch, _map, _camera, _player.TileX, _player.TileY);
    EntityRenderer.DrawEntitiesBehindPlayer(_spriteBatch, _entityManager, _camera, _player.TileY, TileSize);
    _playerRenderer.Draw(_spriteBatch, _player);
    EntityRenderer.DrawEntitiesInFrontOfPlayer(_spriteBatch, _entityManager, _camera, _player.TileY, TileSize);
    TileRenderer.DrawOverlaysInFrontOfPlayer(_spriteBatch, _map, _camera, _player.TileX, _player.TileY);
    
    _spriteBatch.End();
}
```

### 4.4 Deliverables

- [ ] EntityRenderer split into behind/front methods
- [ ] Game1.cs draw order updated
- [ ] Entities correctly occluded by player
- [ ] Entities correctly occlude player
- [ ] Entities correctly occluded by overlays

---

## Phase 5: Interaction System

**Goal:** Player can interact with NPCs and pick up items.

### 5.1 Interaction Detection

```csharp
// In Player.cs or InputManager.cs
public Entity? GetFacingEntity(EntityManager entityManager)
{
    int facingTileX = TileX;
    int facingTileY = TileY;
    
    switch (Facing)
    {
        case Direction.Up: facingTileY--; break;
        case Direction.Down: facingTileY++; break;
        case Direction.Left: facingTileX--; break;
        case Direction.Right: facingTileX++; break;
    }
    
    return entityManager.GetEntityAtTile(facingTileX, facingTileY);
}
```

### 5.2 Input Handling

```csharp
// In Game1.cs or PlayerController.cs
if (_inputManager.IsActionPressed(InputAction.Interact))
{
    var facingEntity = _player.GetFacingEntity(_entityManager);
    if (facingEntity != null)
    {
        facingEntity.Interact(_player);
    }
}

// Collision with item
var itemOnTile = _entityManager.GetEntityAtTile(_player.TileX, _player.TileY);
if (itemOnTile is ItemEntity item)
{
    item.Interact(_player);
}
```

### 5.3 Dialogue System (Basic)

```csharp
// In NPCEntity.Interact()
public override void Interact(Player player)
{
    Facing = player.Facing.Opposite(); // Face player
    // Trigger dialogue UI with Dialogue array
}
```

### 5.4 Deliverables

- [ ] Press key to interact with facing NPC
- [ ] NPC faces player when interacted
- [ ] Walk into item to pick up
- [ ] Items removed from world on pickup
- [ ] Basic dialogue trigger (UI not required yet)

---

## Phase 6: Copy Remaining Assets

**Goal:** All NPCs and Items from source assets available in game.

### 6.1 Items

Copy all item PNGs (already done for 48 items). Add to ItemRegistry:

- All berries (5)
- All crystals (7)
- All stones (10)
- All pokeballs (8)
- All potions/medicine (7)
- Fruits, herbs, mega items

### 6.2 NPCs

Copy remaining NPC spritesheets:

**Civilians:**
- pokemon_civilian_child_a/b
- pokemon_civilian_elder_a/b
- pokemon_civilian_tourist_a/b
- pokemon_civilian_villager_a/b/c

**Trainers:**
- pokemon_trainer_bug_catcher_a/b
- pokemon_trainer_camper_a
- pokemon_trainer_hiker_a/b
- pokemon_trainer_lass_a/b
- pokemon_trainer_youngster_a/b

**Story:**
- pokemon_story_professor_a/b
- pokemon_story_rival_a/b/c
- pokemon_story_mom_a
- etc.

### 6.3 Update Registries

Expand `ItemRegistry` and `NPCRegistry` with all entries.

### 6.4 Deliverables

- [ ] All 48 item PNGs in PokemonGreen.Assets/Items
- [ ] 30+ NPC spritesheets in PokemonGreen.Assets/NPCs
- [ ] ItemRegistry expanded with all items
- [ ] NPCRegistry expanded with all NPCs
- [ ] TileSpriteMapping updated for all entries

---

## Implementation Order

| Phase | Priority | Dependencies |
|-------|----------|--------------|
| Phase 1: Tile Registry | High | None |
| Phase 2: Entity System | High | Phase 1 |
| Phase 3: Spawn Points | Medium | Phase 2 |
| Phase 4: Y-Sorting | Medium | Phase 2, 3 |
| Phase 5: Interaction | Medium | Phase 2, 3 |
| Phase 6: Full Assets | Low | Phase 1, 2 |

---

## File Structure After Completion

```
src/PokemonGreen.Core/
├── Entities/
│   ├── Entity.cs
│   ├── NPCEntity.cs
│   ├── ItemEntity.cs
│   └── EntityManager.cs
├── Items/
│   ├── ItemCategory.cs
│   ├── ItemDefinition.cs
│   ├── ItemRegistry.cs
│   └── TileSpriteMapping.cs
├── NPCs/
│   ├── NPCRole.cs
│   ├── NPCDefinition.cs
│   └── NPCRegistry.cs
├── Maps/
│   ├── SpawnPoints.cs
│   └── (existing files)
└── Rendering/
    ├── EntityRenderer.cs
    └── (existing files)

src/PokemonGreen.Assets/
├── Items/          # 48 item PNGs
├── NPCs/           # 30+ NPC spritesheets + JSON
└── Sprites/        # Existing tile sprites
```
