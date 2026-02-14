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
    public float MoveSpeed { get; set; } = 2.0f;

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
    /// Duration of each animation frame in seconds for the current state.
    /// </summary>
    /// <summary>
    /// Duration of each animation frame in seconds for the current state.
    /// Synced to movement speed: one full cycle = time to cross one tile.
    /// Walk: 0.5s / 9 frames, Run: 0.333s / 8 frames.
    /// </summary>
    public float FrameDuration => State switch
    {
        PlayerState.Idle => 0.6f,
        PlayerState.Walk => 1f / (MoveSpeed * FramesPerState()),            // ~0.056s at 2 tiles/sec
        PlayerState.Run => 1f / (MoveSpeed * RunMultiplier * FramesPerState()), // ~0.042s at 3 tiles/sec
        PlayerState.Jump => 0.1f,
        _ => 0.15f
    };

    /// <summary>
    /// Visual vertical offset during a jump (in tiles). 0 = on ground.
    /// </summary>
    public float JumpHeight { get; private set; }

    #endregion

    #region Movement State

    private float _targetX;
    private float _targetY;
    private bool _isMoving;
    private bool _isRunning;
    private float _jumpTimer;
    private float _jumpDuration;

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

        if (State == PlayerState.Jump)
        {
            UpdateJump(deltaTime);
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
        PlayerState.Idle => 2,
        PlayerState.Walk => 9,
        PlayerState.Run => 8,
        PlayerState.Jump => 5,
        PlayerState.Climb => 2,
        PlayerState.Combat => 6,
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
            int nextFrame = AnimationFrame + 1;

            // One-shot animations return to idle after the last frame
            if (nextFrame >= frameCount && IsOneShotState(State))
            {
                SetState(PlayerState.Idle);
                return;
            }

            AnimationFrame = nextFrame % frameCount;
        }
    }

    #endregion

    /// <summary>
    /// Starts a jump. Moves one tile forward in the facing direction if walkable.
    /// </summary>
    public void BeginJump(TileMap map)
    {
        _jumpDuration = 0.5f;
        _jumpTimer = 0f;
        JumpHeight = 0f;
        SetState(PlayerState.Jump);

        // Move forward one tile if possible
        if (!_isMoving)
        {
            var (dx, dy) = Facing.ToVector();
            int targetTileX = TileX + dx;
            int targetTileY = TileY + dy;
            if (CanMoveTo(targetTileX, targetTileY, map))
            {
                _targetX = targetTileX;
                _targetY = targetTileY;
                _isMoving = true;
                _isRunning = false;
            }
        }
    }

    private static bool IsOneShotState(PlayerState state) => state is PlayerState.Jump or PlayerState.Combat or PlayerState.Spellcast;

    #region Private Methods

    private void UpdateJump(float deltaTime)
    {
        _jumpTimer += deltaTime;
        if (_jumpTimer >= _jumpDuration)
        {
            // Land
            JumpHeight = 0f;
            SetState(PlayerState.Idle);
            return;
        }

        // Parabolic arc: peaks at 1.0 tile height at midpoint
        float t = _jumpTimer / _jumpDuration; // 0 to 1
        JumpHeight = 4f * t * (1f - t); // peaks at 1.0 when t=0.5
    }

    /// <summary>
    /// Snaps the player to the target tile immediately. Called when input is released.
    /// </summary>
    public void SnapToTarget()
    {
        if (_isMoving && State != PlayerState.Jump)
        {
            X = _targetX;
            Y = _targetY;
            _isMoving = false;
            SetState(PlayerState.Idle);
        }
    }

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
            // Don't interrupt one-shot animations (jump, combat, etc.)
            if (!IsOneShotState(State))
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
