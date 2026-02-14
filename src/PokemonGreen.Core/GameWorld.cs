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
    /// Duration of the fade-out and fade-in in seconds.
    /// </summary>
    public const float FadeDuration = 0.3f;

    /// <summary>
    /// The currently loaded tile map.
    /// </summary>
    public TileMap? CurrentMap { get; private set; }

    /// <summary>
    /// The currently loaded map definition (null if loaded from raw TileMap).
    /// </summary>
    public MapDefinition? CurrentMapDefinition { get; private set; }

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
    /// Fade overlay opacity (0 = fully visible, 1 = fully black).
    /// Game1 should read this to draw a black overlay.
    /// </summary>
    public float FadeAlpha { get; private set; }

    /// <summary>
    /// Whether a transition is currently in progress.
    /// </summary>
    public bool IsTransitioning => _transitionState != TransitionState.None;

    private enum TransitionState { None, FadingOut, FadingIn }

    private TransitionState _transitionState;
    private float _fadeTimer;
    private WarpConnection? _pendingWarp;
    private bool _wasPlayerMoving;

    /// <summary>
    /// Creates a new GameWorld with the specified viewport dimensions.
    /// </summary>
    public GameWorld(int viewportWidth, int viewportHeight)
    {
        Camera = new Camera(viewportWidth, viewportHeight);
        Input = new InputManager();
        Player = new PlayerClass(0, 0);
    }

    /// <summary>
    /// Loads a map from a map definition and positions the player at the default spawn point.
    /// </summary>
    public void LoadMap(MapDefinition mapDef)
    {
        LoadMap(mapDef, null, null);
    }

    /// <summary>
    /// Loads a map from a map definition with an optional specific spawn position.
    /// </summary>
    public void LoadMap(MapDefinition mapDef, float? spawnX, float? spawnY)
    {
        CurrentMapDefinition = mapDef;
        CurrentMap = mapDef.CreateTileMap();

        float px = spawnX ?? CurrentMap.Width / 2f;
        float py = spawnY ?? CurrentMap.Height / 2f;
        Player.SetPosition(px, py);

        SnapCamera(px, py);
    }

    /// <summary>
    /// Loads a tile map directly and positions the player at the spawn point.
    /// </summary>
    public void LoadMap(TileMap map)
    {
        CurrentMapDefinition = null;
        CurrentMap = map;

        float spawnX = CurrentMap.Width / 2f;
        float spawnY = CurrentMap.Height / 2f;
        Player.SetPosition(spawnX, spawnY);

        SnapCamera(spawnX, spawnY);
    }

    /// <summary>
    /// Main update loop that processes input, updates player, and updates camera.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (CurrentMap == null)
            return;

        // Handle transition animation
        if (_transitionState != TransitionState.None)
        {
            UpdateTransition(deltaTime);
            return;
        }

        // 1. Update input state
        Input.Update();

        // 2. Handle interact warps (player faces a tile and presses E/Enter)
        if (Input.InteractPressed && CurrentMapDefinition != null)
        {
            var (dx, dy) = PokemonGreen.Core.Player.DirectionExtensions.ToVector(Player.Facing);
            int facingX = Player.TileX + dx;
            int facingY = Player.TileY + dy;

            var interactWarp = CurrentMapDefinition.GetWarp(facingX, facingY, WarpTrigger.Interact);
            if (interactWarp != null)
            {
                BeginTransition(interactWarp);
                return;
            }
        }

        // 3. Handle jump (allowed any time except already jumping)
        if (Input.JumpPressed && Player.State != PokemonGreen.Core.Player.PlayerState.Jump)
        {
            Player.BeginJump(CurrentMap);
        }

        // 4. Handle player movement from input (skip if jumping)
        var moveDir = Input.MoveDirection;
        if (moveDir.HasValue && Player.State != PokemonGreen.Core.Player.PlayerState.Jump)
        {
            var playerDir = ConvertDirection(moveDir.Value);

            // Check if this movement would go off the map edge
            if (CurrentMapDefinition != null && !Player.CanMoveTo(
                    Player.TileX + PokemonGreen.Core.Player.DirectionExtensions.ToVector(playerDir).dx,
                    Player.TileY + PokemonGreen.Core.Player.DirectionExtensions.ToVector(playerDir).dy,
                    CurrentMap))
            {
                var edge = DirectionToEdge(moveDir.Value);
                if (edge.HasValue && TryEdgeTransition(edge.Value, Player.TileX, Player.TileY))
                    return;
            }

            Player.Move(playerDir, Input.IsRunning, CurrentMap);
        }

        // Snap to tile when movement keys released
        if (!moveDir.HasValue)
            Player.SnapToTarget();

        // 5. Update player
        bool isMovingNow = Player.State != PokemonGreen.Core.Player.PlayerState.Idle || IsPlayerBetweenTiles();
        Player.Update(deltaTime, CurrentMap);
        bool stoppedThisFrame = _wasPlayerMoving && !IsPlayerBetweenTiles() && Player.State == PokemonGreen.Core.Player.PlayerState.Idle;
        _wasPlayerMoving = isMovingNow;

        // 5. Check step warps after player arrives at a new tile
        if (stoppedThisFrame && CurrentMapDefinition != null)
        {
            var stepWarp = CurrentMapDefinition.GetWarp(Player.TileX, Player.TileY, WarpTrigger.Step);
            if (stepWarp != null)
            {
                BeginTransition(stepWarp);
                return;
            }
        }

        // 6. Update camera to follow player
        Camera.Update(
            Player.X * TileSize,
            Player.Y * TileSize,
            CurrentMap.Width,
            CurrentMap.Height,
            TileSize);
    }

    /// <summary>
    /// Called when the viewport is resized. Updates camera dimensions and recalculates zoom.
    /// </summary>
    public void OnViewportResized(int newWidth, int newHeight)
    {
        Camera.ViewportWidth = newWidth;
        Camera.ViewportHeight = newHeight;

        if (CurrentMap != null)
            SnapCamera(Player.X, Player.Y);
    }

    /// <summary>
    /// Teleports the player to the specified position.
    /// </summary>
    public void SetPlayerPosition(float x, float y)
    {
        Player.SetPosition(x, y);
    }

    /// <summary>
    /// Starts a fade-out → map swap → fade-in transition.
    /// </summary>
    private void BeginTransition(WarpConnection warp)
    {
        _pendingWarp = warp;
        _transitionState = TransitionState.FadingOut;
        _fadeTimer = 0f;
    }

    private void UpdateTransition(float deltaTime)
    {
        _fadeTimer += deltaTime;

        if (_transitionState == TransitionState.FadingOut)
        {
            FadeAlpha = Math.Min(_fadeTimer / FadeDuration, 1f);

            if (_fadeTimer >= FadeDuration)
            {
                // Screen is fully black — swap the map
                if (_pendingWarp != null)
                {
                    if (MapCatalog.TryGetMap(_pendingWarp.TargetMapId, out var targetMap) && targetMap != null)
                    {
                        LoadMap(targetMap, _pendingWarp.TargetX, _pendingWarp.TargetY);
                    }
                    else
                    {
                        System.Console.Error.WriteLine($"Warp target map not found: {_pendingWarp.TargetMapId}");
                    }
                    _pendingWarp = null;
                }

                // Begin fade-in
                _transitionState = TransitionState.FadingIn;
                _fadeTimer = 0f;
            }
        }
        else if (_transitionState == TransitionState.FadingIn)
        {
            FadeAlpha = 1f - Math.Min(_fadeTimer / FadeDuration, 1f);

            if (_fadeTimer >= FadeDuration)
            {
                FadeAlpha = 0f;
                _transitionState = TransitionState.None;
            }
        }
    }

    /// <summary>
    /// Instantly positions the camera on the player and clamps to map bounds.
    /// </summary>
    private void SnapCamera(float playerTileX, float playerTileY)
    {
        // Auto-zoom so the map fills the viewport
        float mapWorldWidth = CurrentMap!.Width * TileSize;
        float mapWorldHeight = CurrentMap.Height * TileSize;
        float zoomX = Camera.ViewportWidth / mapWorldWidth;
        float zoomY = Camera.ViewportHeight / mapWorldHeight;
        Camera.Zoom = MathF.Min(zoomX, zoomY);

        Camera.X = playerTileX * TileSize;
        Camera.Y = playerTileY * TileSize;
        Camera.ClampToMap(CurrentMap.Width, CurrentMap.Height, TileSize);
        _wasPlayerMoving = false;
    }

    private bool IsPlayerBetweenTiles()
    {
        float fracX = Player.X - MathF.Floor(Player.X);
        float fracY = Player.Y - MathF.Floor(Player.Y);
        return fracX > 0.001f || fracY > 0.001f;
    }

    /// <summary>
    /// Checks for an edge connection and begins a transition if one exists.
    /// Returns true if a transition was started.
    /// </summary>
    private bool TryEdgeTransition(MapEdge edge, int playerX, int playerY)
    {
        var conn = CurrentMapDefinition!.GetConnection(edge);
        if (conn == null)
            return false;

        if (!MapCatalog.TryGetMap(conn.TargetMapId, out var targetMap) || targetMap == null)
            return false;

        // Calculate arrival position on the target map
        float targetX, targetY;
        switch (edge)
        {
            case MapEdge.North:
                targetX = playerX + conn.Offset;
                targetY = targetMap.Height - 1;
                break;
            case MapEdge.South:
                targetX = playerX + conn.Offset;
                targetY = 0;
                break;
            case MapEdge.West:
                targetX = targetMap.Width - 1;
                targetY = playerY + conn.Offset;
                break;
            case MapEdge.East:
                targetX = 0;
                targetY = playerY + conn.Offset;
                break;
            default:
                return false;
        }

        // Clamp to target map bounds
        targetX = Math.Clamp(targetX, 0, targetMap.Width - 1);
        targetY = Math.Clamp(targetY, 0, targetMap.Height - 1);

        BeginTransition(new WarpConnection(0, 0, conn.TargetMapId, (int)targetX, (int)targetY));
        return true;
    }

    private static MapEdge? DirectionToEdge(Direction dir)
    {
        return dir switch
        {
            Direction.Up => MapEdge.North,
            Direction.Down => MapEdge.South,
            Direction.Left => MapEdge.West,
            Direction.Right => MapEdge.East,
            _ => null
        };
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
