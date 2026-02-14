using PokemonGreen.Core.Maps;
using PokemonGreen.Core.Systems;
using PlayerDirection = PokemonGreen.Core.Player.Direction;
using PlayerClass = PokemonGreen.Core.Player.Player;

namespace PokemonGreen.Core;

/// <summary>
/// Main orchestrator that coordinates all game systems including maps, player, camera, and input.
/// </summary>
public class GameWorld
{
    /// <summary>
    /// Size of each tile in pixels.
    /// </summary>
    public const int TileSize = 16;

    /// <summary>
    /// The currently loaded tile map.
    /// </summary>
    public TileMap? CurrentMap { get; private set; }

    /// <summary>
    /// The player character.
    /// </summary>
    public PlayerClass Player { get; }

    /// <summary>
    /// The camera for viewport management.
    /// </summary>
    public Camera Camera { get; }

    /// <summary>
    /// The input manager for handling player input.
    /// </summary>
    public InputManager Input { get; }

    /// <summary>
    /// Indicates whether the game world has been initialized with a map.
    /// </summary>
    public bool IsInitialized => CurrentMap != null;

    /// <summary>
    /// Creates a new GameWorld with the specified viewport dimensions.
    /// </summary>
    /// <param name="viewportWidth">Width of the viewport in pixels.</param>
    /// <param name="viewportHeight">Height of the viewport in pixels.</param>
    public GameWorld(int viewportWidth, int viewportHeight)
    {
        Camera = new Camera(viewportWidth, viewportHeight);
        Input = new InputManager();
        Player = new PlayerClass(0, 0);
    }

    /// <summary>
    /// Loads a map from a map definition and positions the player at the spawn point.
    /// </summary>
    /// <param name="mapDef">The map definition to load.</param>
    public void LoadMap(MapDefinition mapDef)
    {
        LoadMap(mapDef.CreateTileMap());
    }

    /// <summary>
    /// Loads a tile map directly and positions the player at the spawn point.
    /// </summary>
    /// <param name="map">The tile map to load.</param>
    public void LoadMap(TileMap map)
    {
        CurrentMap = map;

        // Position player at spawn point (center of map by default)
        float spawnX = CurrentMap.Width / 2f;
        float spawnY = CurrentMap.Height / 2f;
        Player.SetPosition(spawnX, spawnY);

        // Center camera on player
        Camera.X = spawnX * TileSize;
        Camera.Y = spawnY * TileSize;
    }

    /// <summary>
    /// Main update loop that processes input, updates player, and updates camera.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
    public void Update(float deltaTime)
    {
        if (CurrentMap == null)
            return;

        // 1. Update input state
        Input.Update();

        // 2. Handle player movement from input
        var moveDir = Input.MoveDirection;
        if (moveDir.HasValue)
        {
            // Convert from Systems.Direction to Player.Direction
            var playerDir = ConvertDirection(moveDir.Value);
            Player.Move(playerDir, Input.IsRunning, CurrentMap);
        }

        // 3. Update player
        Player.Update(deltaTime, CurrentMap);

        // 4. Update camera to follow player
        Camera.Update(
            Player.X * TileSize,
            Player.Y * TileSize,
            CurrentMap.Width,
            CurrentMap.Height,
            TileSize);
    }

    /// <summary>
    /// Teleports the player to the specified position.
    /// </summary>
    /// <param name="x">Target X position in tiles.</param>
    /// <param name="y">Target Y position in tiles.</param>
    public void SetPlayerPosition(float x, float y)
    {
        Player.SetPosition(x, y);
    }

    /// <summary>
    /// Converts from Systems.Direction to Player.Direction.
    /// </summary>
    private static PlayerDirection ConvertDirection(Direction dir)
    {
        return dir switch
        {
            Direction.Up => PlayerDirection.Up,
            Direction.Down => PlayerDirection.Down,
            Direction.Left => PlayerDirection.Left,
            Direction.Right => PlayerDirection.Right,
            _ => PlayerDirection.Down
        };
    }
}
