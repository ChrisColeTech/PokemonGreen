using PokemonGreen.Core.Maps;

namespace PokemonGreen.Core.Player;

/// <summary>
/// Main player class handling position, movement, state, and animation.
/// </summary>
public class Player
{
    #region Position Properties

    /// <summary>
    /// X position in tiles (can be fractional for smooth movement).
    /// </summary>
    public float X { get; private set; }

    /// <summary>
    /// Y position in tiles (can be fractional for smooth movement).
    /// </summary>
    public float Y { get; private set; }

    /// <summary>
    /// Computed integer tile X position.
    /// </summary>
    public int TileX => (int)MathF.Floor(X);

    /// <summary>
    /// Computed integer tile Y position.
    /// </summary>
    public int TileY => (int)MathF.Floor(Y);

    #endregion

    #region State Properties

    /// <summary>
    /// The direction the player is currently facing.
    /// </summary>
    public Direction Facing { get; private set; } = Direction.Down;

    /// <summary>
    /// The current state of the player.
    /// </summary>
    public PlayerState State { get; private set; } = PlayerState.Idle;

    /// <summary>
    /// Movement speed in tiles per second.
    /// </summary>
    public float MoveSpeed { get; set; } = 4.0f;

    /// <summary>
    /// Speed multiplier when running.
    /// </summary>
    public float RunMultiplier { get; set; } = 1.5f;

    #endregion

    #region Animation Properties

    /// <summary>
    /// Current animation frame index.
    /// </summary>
    public int AnimationFrame { get; private set; }

    /// <summary>
    /// Timer tracking animation progress.
    /// </summary>
    public float AnimationTimer { get; private set; }

    /// <summary>
    /// Duration of each animation frame in seconds.
    /// </summary>
    public float FrameDuration { get; set; } = 0.15f;

    #endregion

    #region Movement State

    private float _targetX;
    private float _targetY;
    private bool _isMoving;
    private bool _isRunning;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new player at the specified tile position.
    /// </summary>
    /// <param name="startX">Starting X tile position.</param>
    /// <param name="startY">Starting Y tile position.</param>
    public Player(float startX = 0, float startY = 0)
    {
        X = startX;
        Y = startY;
        _targetX = startX;
        _targetY = startY;
    }

    #endregion

    #region State Machine Methods

    /// <summary>
    /// Main update method called each frame.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
    /// <param name="map">The current tile map for collision checks.</param>
    public void Update(float deltaTime, TileMap map)
    {
        if (_isMoving)
        {
            UpdateMovement(deltaTime);
        }

        UpdateAnimation(deltaTime);
    }

    /// <summary>
    /// Sets the player to a new state.
    /// </summary>
    /// <param name="newState">The new state to transition to.</param>
    public void SetState(PlayerState newState)
    {
        if (State != newState)
        {
            State = newState;
            AnimationFrame = 0;
            AnimationTimer = 0;
        }
    }

    /// <summary>
    /// Initiates movement in the specified direction.
    /// </summary>
    /// <param name="dir">The direction to move.</param>
    /// <param name="running">Whether the player is running.</param>
    /// <param name="map">The current tile map for collision checks.</param>
    /// <returns>True if movement was initiated, false if blocked.</returns>
    public bool Move(Direction dir, bool running, TileMap map)
    {
        // Always update facing direction
        Facing = dir;

        // Don't start new movement if already moving
        if (_isMoving)
        {
            return false;
        }

        var (dx, dy) = dir.ToVector();
        int targetTileX = TileX + dx;
        int targetTileY = TileY + dy;

        if (!CanMoveTo(targetTileX, targetTileY, map))
        {
            return false;
        }

        // Start movement
        _targetX = targetTileX;
        _targetY = targetTileY;
        _isMoving = true;
        _isRunning = running;

        SetState(running ? PlayerState.Run : PlayerState.Walk);

        return true;
    }

    /// <summary>
    /// Checks if the player can move to the specified tile.
    /// </summary>
    /// <param name="x">Target tile X coordinate.</param>
    /// <param name="y">Target tile Y coordinate.</param>
    /// <param name="map">The current tile map for collision checks.</param>
    /// <returns>True if the tile is walkable, false otherwise.</returns>
    public bool CanMoveTo(int x, int y, TileMap map)
    {
        // Check map bounds
        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
        {
            return false;
        }

        // Check if tile is walkable
        return map.IsWalkable(x, y);
    }

    #endregion

    #region Animation Methods

    /// <summary>
    /// Gets the number of animation frames for the current state.
    /// </summary>
    /// <returns>Number of frames for the current state.</returns>
    public int FramesPerState() => State switch
    {
        PlayerState.Idle => 1,
        PlayerState.Walk => 4,
        PlayerState.Run => 4,
        PlayerState.Jump => 3,
        PlayerState.Climb => 2,
        PlayerState.Combat => 4,
        PlayerState.Spellcast => 6,
        _ => 1
    };

    /// <summary>
    /// Updates the animation timer and frame.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
    public void UpdateAnimation(float deltaTime)
    {
        AnimationTimer += deltaTime;

        int frameCount = FramesPerState();
        if (frameCount <= 1)
        {
            AnimationFrame = 0;
            return;
        }

        while (AnimationTimer >= FrameDuration)
        {
            AnimationTimer -= FrameDuration;
            AnimationFrame = (AnimationFrame + 1) % frameCount;
        }
    }

    #endregion

    #region Private Methods

    private void UpdateMovement(float deltaTime)
    {
        float speed = MoveSpeed * (_isRunning ? RunMultiplier : 1.0f);
        float movement = speed * deltaTime;

        // Calculate direction to target
        float dx = _targetX - X;
        float dy = _targetY - Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);

        if (distance <= movement)
        {
            // Reached target
            X = _targetX;
            Y = _targetY;
            _isMoving = false;
            SetState(PlayerState.Idle);
        }
        else
        {
            // Move toward target
            float ratio = movement / distance;
            X += dx * ratio;
            Y += dy * ratio;
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Teleports the player to the specified tile position.
    /// </summary>
    /// <param name="x">Target X tile position.</param>
    /// <param name="y">Target Y tile position.</param>
    public void SetPosition(float x, float y)
    {
        X = x;
        Y = y;
        _targetX = x;
        _targetY = y;
        _isMoving = false;
    }

    #endregion
}
