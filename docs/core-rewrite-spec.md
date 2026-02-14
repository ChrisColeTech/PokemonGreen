# Core Rewrite Spec: Camera, Player, GameWorld

## Camera

**File:** `src/PokemonGreen.Core/Systems/Camera.cs`

### What it does
- Follows the player. Player is always centered on screen.
- Zoom is set once when a map loads. Never changes on window resize.
- Small maps get zoomed in enough to fill the entire screen (no empty space).
- Clamps to map edges so you don't see past the map.
- Produces a transform matrix for SpriteBatch rendering.

### On window resize
- Viewport dimensions update.
- Zoom stays the same.
- Result: smaller window = fewer tiles visible = more zoomed in. Bigger window = more tiles visible.

---

## Player

**File:** `src/PokemonGreen.Core/Player/Player.cs`

### Movement
- Continuous, smooth movement. NOT tile-to-tile snapping.
- WASD/arrows move the player fluidly in that direction.
- Collisions checked against the tile grid, but movement itself is smooth pixel-by-pixel.
- Walk and run speeds. Shift toggles run.
- When input stops, player stops immediately. No coasting, no snapping to a grid.

### Jump
- Space makes the player jump UP (vertical hop in place).
- If moving when you jump, you keep your momentum and glide through the air.
- Sine arc for height. Lands where momentum carries you.
- Cannot jump again until landing.

### Animation
- States: Idle, Walk, Run, Jump, Climb, Combat, Spellcast
- Frame counts match sprite sheets (Idle=2, Walk=9, Run=8, Jump=5, Combat=6)
- One-shot states (Jump, Combat, Spellcast) play once then return to Idle.
- Looping states (Idle, Walk, Run) wrap around.

---

## GameWorld

**File:** `src/PokemonGreen.Core/GameWorld.cs`

### What it does
- Coordinates input, player, and camera each frame. That's it.
- Loads maps and positions the player.
- Handles map transitions (warps and edge connections) with a fade.

### Update loop
1. If fading between maps, update fade and return.
2. Read input.
3. If interact key pressed, check for door warp on the tile the player faces.
4. Pass movement input to player.
5. If jump pressed, tell player to jump.
6. Update player.
7. Check if player is on a step warp tile.
8. If player walks into the map edge, try transitioning to the neighbor map.
9. Camera follows player.

### Map transitions
- Warps: tile-based. Step warps trigger when the player is on the tile. Interact warps trigger on E/Enter.
- Edge transitions: walking off the map edge looks up the neighbor by worldX/worldY grid.
- Fade to black, load new map, fade in. Input blocked during fade.

### Zoom
- Calculated once on map load. Based on the viewport size at that moment.
- Does NOT change on window resize.
- Small maps get extra zoom so they fill the screen.

---

## Dependencies (not being rewritten)

- `Player/Direction.cs`, `Player/PlayerState.cs`
- `Systems/InputManager.cs`
- `Maps/MapDefinition.cs`, `Maps/MapCatalog.cs`, `Maps/TileMap.cs`, `Maps/WarpConnection.cs`
