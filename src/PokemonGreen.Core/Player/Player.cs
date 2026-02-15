using PokemonGreen.Core.Maps;

namespace PokemonGreen.Core.Player;

public class Player
{
    // Position (in tile coordinates, float for smooth movement)
    public float X { get; private set; }
    public float Y { get; private set; }
    public int TileX => (int)MathF.Floor(X);
    public int TileY => (int)MathF.Floor(Y);

    // State
    public Direction Facing { get; private set; } = Direction.Down;
    public PlayerState State { get; private set; } = PlayerState.Idle;

    // Movement tuning
    public float MoveSpeed { get; set; } = 4.0f;
    public float RunMultiplier { get; set; } = 2.0f;

    // Animation
    public int AnimationFrame { get; private set; }
    public float JumpHeight { get; private set; }

    public float FrameDuration => State switch
    {
        PlayerState.Idle => 0.6f,
        PlayerState.Walk => 0.1f,
        PlayerState.Run => 0.06f,
        PlayerState.Jump => 0.08f,
        _ => 0.15f
    };

    // Movement intent (set by Move, consumed by Update)
    private bool _hasMovementIntent;
    private Direction _moveDirection;
    private bool _isRunning;

    // Jump state
    private bool _isJumping;
    private float _jumpTimer;
    private const float JumpDuration = 0.4f;
    private const float JumpPeakHeight = 0.8f;

    // Momentum stored at jump start (for air drift)
    private float _jumpMomentumDx;
    private float _jumpMomentumDy;
    private float _jumpMomentumSpeed;

    // Animation timer
    private float _animationTimer;

    public Player(float startX = 0, float startY = 0)
    {
        X = startX;
        Y = startY;
    }

    /// <summary>
    /// Called each frame by GameWorld to declare movement intent.
    /// Actual position change happens in Update with proper deltaTime.
    /// </summary>
    public void Move(Direction direction, bool isRunning, TileMap map)
    {
        if (_isJumping) return;

        Facing = direction;
        _hasMovementIntent = true;
        _moveDirection = direction;
        _isRunning = isRunning;

        SetState(isRunning ? PlayerState.Run : PlayerState.Walk);
    }

    public void StopMoving()
    {
        _hasMovementIntent = false;
        if (!_isJumping)
        {
            SetState(PlayerState.Idle);
        }
    }

    public void BeginJump(TileMap map)
    {
        if (_isJumping) return;

        _isJumping = true;
        _jumpTimer = 0f;
        JumpHeight = 0f;

        // Capture current momentum: if the player was moving, carry that into the jump
        if (_hasMovementIntent)
        {
            var (dx, dy) = _moveDirection.ToVector();
            _jumpMomentumDx = dx;
            _jumpMomentumDy = dy;
            _jumpMomentumSpeed = MoveSpeed * (_isRunning ? RunMultiplier : 1f);
        }
        else
        {
            _jumpMomentumDx = 0;
            _jumpMomentumDy = 0;
            _jumpMomentumSpeed = 0;
        }

        SetState(PlayerState.Jump);
    }

    public void Update(float deltaTime, TileMap map)
    {
        if (_isJumping)
        {
            UpdateJump(deltaTime, map);
        }
        else if (_hasMovementIntent)
        {
            ApplyMovement(deltaTime, map);
        }

        UpdateAnimation(deltaTime);
    }

    public void SetPosition(float x, float y)
    {
        X = x;
        Y = y;
    }

    public bool CanMoveTo(int x, int y, TileMap map)
    {
        return map.IsInBounds(x, y) && map.IsWalkable(x, y);
    }

    /// <summary>
    /// Checks if the position (px, py) is blocked, using reduced collision areas
    /// for decoration objects like trees and boulders so their hitbox is smaller
    /// than a full tile.
    /// </summary>
    private bool IsBlocked(float px, float py, TileMap map)
    {
        int tileX = (int)MathF.Floor(px);
        int tileY = (int)MathF.Floor(py);

        if (!map.IsInBounds(tileX, tileY))
            return true;

        if (map.IsWalkable(tileX, tileY))
            return false;

        int overlayId = map.GetOverlayTile(tileX, tileY);
        var (insetX, insetY) = GetObjectCollisionInsets(overlayId);
        if (insetX <= 0f && insetY <= 0f)
            return true;

        float localX = px - tileX;
        float localY = py - tileY;
        return localX >= insetX && localX < 1f - insetX &&
               localY >= insetY && localY < 1f - insetY;
    }

    private static (float x, float y) GetObjectCollisionInsets(int tileId) => tileId switch
    {
        16 => (0.3f, 0.2f),   // Tree
        17 => (0.25f, 0.25f), // Rock
        20 => (0.25f, 0.25f), // Bush
        22 => (0.25f, 0.25f), // Boulder
        19 => (0.2f, 0.2f),   // Statue
        123 => (0.25f, 0.25f), // FireRock - same as Rock
        124 => (0.25f, 0.25f), // FireBoulder - same as Boulder
        125 => (0.25f, 0.25f), // LavaSplatter
        _ => (0f, 0f)
    };

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

    // --- Private implementation ---

    private void ApplyMovement(float deltaTime, TileMap map)
    {
        var (dx, dy) = _moveDirection.ToVector();
        float speed = MoveSpeed * (_isRunning ? RunMultiplier : 1f);

        float newX = X + dx * speed * deltaTime;
        float newY = Y + dy * speed * deltaTime;

        // Clamp to keep player within tile centers (prevents visual off-map on bottom/right)
        newX = Math.Clamp(newX, 0, map.Width - 1f);
        newY = Math.Clamp(newY, 0, map.Height - 1f);

        // Try full movement first (using centered collision box)
        if (!IsBlocked(newX, newY, map))
        {
            X = newX;
            Y = newY;
            return;
        }

        // Axis-aligned sliding: try X alone
        if (dx != 0 && !IsBlocked(newX, Y, map))
        {
            X = newX;
            return;
        }

        // Try Y alone
        if (dy != 0 && !IsBlocked(X, newY, map))
        {
            Y = newY;
        }
    }

    private void UpdateJump(float deltaTime, TileMap map)
    {
        _jumpTimer += deltaTime;
        float t = MathF.Min(_jumpTimer / JumpDuration, 1f);

        // Sine arc for vertical height
        JumpHeight = MathF.Sin(t * MathF.PI) * JumpPeakHeight;

        // Apply horizontal momentum drift with collision checks
        if (_jumpMomentumSpeed > 0)
        {
            float driftX = _jumpMomentumDx * _jumpMomentumSpeed * deltaTime;
            float driftY = _jumpMomentumDy * _jumpMomentumSpeed * deltaTime;
            float proposedX = X + driftX;
            float proposedY = Y + driftY;

            if (!IsBlocked(proposedX, proposedY, map))
            {
                X = proposedX;
                Y = proposedY;
            }
            else
            {
                // Try sliding along each axis
                if (_jumpMomentumDx != 0 && !IsBlocked(proposedX, Y, map))
                {
                    X = proposedX;
                }
                else if (_jumpMomentumDy != 0 && !IsBlocked(X, proposedY, map))
                {
                    Y = proposedY;
                }
            }
        }

        // Land when arc completes
        if (_jumpTimer >= JumpDuration)
        {
            JumpHeight = 0f;
            _isJumping = false;

            // If still receiving movement input, go back to walk/run; otherwise idle
            if (_hasMovementIntent)
            {
                SetState(_isRunning ? PlayerState.Run : PlayerState.Walk);
            }
            else
            {
                SetState(PlayerState.Idle);
            }
        }
    }

    private void SetState(PlayerState newState)
    {
        if (State != newState)
        {
            State = newState;
            AnimationFrame = 0;
            _animationTimer = 0f;
        }
    }

    private void UpdateAnimation(float deltaTime)
    {
        int frameCount = FramesPerState();
        if (frameCount <= 1)
        {
            AnimationFrame = 0;
            return;
        }

        _animationTimer += deltaTime;

        while (_animationTimer >= FrameDuration)
        {
            _animationTimer -= FrameDuration;
            int nextFrame = AnimationFrame + 1;

            if (nextFrame >= frameCount && IsOneShotState(State))
            {
                // One-shot animation finished; return to idle
                SetState(PlayerState.Idle);
                return;
            }

            AnimationFrame = nextFrame % frameCount;
        }
    }

    private static bool IsOneShotState(PlayerState state) =>
        state is PlayerState.Jump or PlayerState.Combat or PlayerState.Spellcast;
}
