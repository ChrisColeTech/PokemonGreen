namespace PokemonGreen.Core;

public enum Direction
{
    Up = 0,
    Left = 1,
    Down = 2,
    Right = 3
}

public enum PlayerState
{
    Idle,
    Walk,
    Run,
    Jump,
    Climb,
    Combat,
    Spellcast
}

public class Player
{
    public float X { get; private set; }
    public float Y { get; private set; }
    public float Speed { get; set; } = 150f;
    public Direction Facing { get; private set; } = Direction.Down;
    public PlayerState State { get; private set; } = PlayerState.Idle;
    public int FrameIndex { get; private set; }
    public float AnimationProgress { get; private set; }
    public bool IsRunningToggled { get; private set; }
    public bool IsActionLocked => _currentAction.HasValue;

    private float _animationTime;
    private PlayerState? _currentAction;

    private const int IdleFrameCount = 2;
    private const int WalkFrameCount = 9;
    private const int RunFrameCount = 8;
    private const int JumpFrameCount = 5;
    private const int ClimbFrameCount = 6;
    private const int CombatFrameCount = 2;
    private const int SpellcastFrameCount = 6;

    private const float IdleDuration = 0.8f;
    private const float WalkDuration = 0.9f;
    private const float RunDuration = 0.56f;
    private const float JumpDuration = 0.5f;
    private const float ClimbDuration = 0.6f;
    private const float CombatDuration = 0.2f;
    private const float SpellcastDuration = 0.6f;

    public Player(float x, float y)
    {
        X = x;
        Y = y;
    }

    public void ToggleRun()
    {
        IsRunningToggled = !IsRunningToggled;
    }

    public void TriggerAction(PlayerState action)
    {
        if (action is not (PlayerState.Jump or PlayerState.Climb or PlayerState.Combat or PlayerState.Spellcast))
            return;
        if (_currentAction.HasValue)
            return;

        _currentAction = action;
        State = action;
        _animationTime = 0f;
    }

    public void Update(float dx, float dy, TileMap map, float deltaTime)
    {
        if (_currentAction.HasValue)
        {
            UpdateActionAnimation(deltaTime);
            return;
        }

        UpdateMovement(dx, dy, map, deltaTime);
        UpdateLoopingAnimation(deltaTime);
    }

    private void UpdateActionAnimation(float deltaTime)
    {
        var duration = GetAnimationDuration(_currentAction!.Value);
        var frameCount = GetFrameCount(_currentAction!.Value);

        _animationTime += deltaTime;

        if (_animationTime >= duration)
        {
            _currentAction = null;
            State = PlayerState.Idle;
            _animationTime = 0f;
            AnimationProgress = 0f;
            FrameIndex = 0;
            return;
        }

        AnimationProgress = _animationTime / duration;
        FrameIndex = Math.Clamp((int)(AnimationProgress * frameCount), 0, frameCount - 1);
    }

    private void UpdateMovement(float dx, float dy, TileMap map, float deltaTime)
    {
        if (dx != 0f || dy != 0f)
        {
            Facing = GetDirection(dx, dy);
            State = IsRunningToggled ? PlayerState.Run : PlayerState.Walk;

            var speed = IsRunningToggled ? Speed * 1.8f : Speed;
            var newX = X + dx * speed * deltaTime;
            var newY = Y + dy * speed * deltaTime;

            var tileX = (int)(newX / map.TileSize);
            var tileY = (int)(newY / map.TileSize);

            if (map.IsWalkable(tileX, tileY))
            {
                X = newX;
                Y = newY;
            }
        }
        else
        {
            State = PlayerState.Idle;
        }
    }

    private void UpdateLoopingAnimation(float deltaTime)
    {
        var duration = GetAnimationDuration(State);
        var frameCount = GetFrameCount(State);

        _animationTime += deltaTime;

        if (_animationTime >= duration)
        {
            _animationTime -= duration;
        }

        AnimationProgress = _animationTime / duration;
        FrameIndex = (int)(AnimationProgress * frameCount) % frameCount;
    }

    private static int GetFrameCount(PlayerState state)
    {
        return state switch
        {
            PlayerState.Idle => IdleFrameCount,
            PlayerState.Walk => WalkFrameCount,
            PlayerState.Run => RunFrameCount,
            PlayerState.Jump => JumpFrameCount,
            PlayerState.Climb => ClimbFrameCount,
            PlayerState.Combat => CombatFrameCount,
            PlayerState.Spellcast => SpellcastFrameCount,
            _ => IdleFrameCount
        };
    }

    private static float GetAnimationDuration(PlayerState state)
    {
        return state switch
        {
            PlayerState.Idle => IdleDuration,
            PlayerState.Walk => WalkDuration,
            PlayerState.Run => RunDuration,
            PlayerState.Jump => JumpDuration,
            PlayerState.Climb => ClimbDuration,
            PlayerState.Combat => CombatDuration,
            PlayerState.Spellcast => SpellcastDuration,
            _ => IdleDuration
        };
    }

    private static Direction GetDirection(float dx, float dy)
    {
        if (Math.Abs(dy) >= Math.Abs(dx))
            return dy < 0 ? Direction.Up : Direction.Down;
        return dx < 0 ? Direction.Left : Direction.Right;
    }
}
