using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace PokemonGreen.Core.Systems;

public readonly record struct ThirdPersonInputState(
    bool ExitRequested,
    float MoveX,
    float MoveZ,
    float Turn,
    float Pitch,
    float Zoom,
    bool JumpPressed,
    bool IsRunning);

public sealed class ThirdPersonInputMapper
{
    private KeyboardState _currentState;
    private KeyboardState _previousState;
    private bool _runToggled;

    public ThirdPersonInputState State { get; private set; }

    public ThirdPersonInputMapper()
    {
        _currentState = Keyboard.GetState();
        _previousState = _currentState;
        State = new ThirdPersonInputState(false, 0f, 0f, 0f, 0f, 0f, false, false);
    }

    public void Update()
    {
        _previousState = _currentState;
        _currentState = Keyboard.GetState();

        if (IsPressed(Keys.LeftShift) || IsPressed(Keys.RightShift))
            _runToggled = !_runToggled;

        var moveX = Axis(Keys.A, Keys.Left, Keys.D, Keys.Right);
        var moveZ = Axis(Keys.S, Keys.Down, Keys.W, Keys.Up);
        var moveLength = MathF.Sqrt(moveX * moveX + moveZ * moveZ);
        if (moveLength > 1f)
        {
            moveX /= moveLength;
            moveZ /= moveLength;
        }

        var turn = Axis(Keys.Q, null, Keys.E, null);
        var pitch = Axis(Keys.R, null, Keys.F, null);
        var zoom = Axis(Keys.PageDown, null, Keys.PageUp, null);

        State = new ThirdPersonInputState(
            ExitRequested: _currentState.IsKeyDown(Keys.Escape),
            MoveX: moveX,
            MoveZ: moveZ,
            Turn: turn,
            Pitch: pitch,
            Zoom: zoom,
            JumpPressed: IsPressed(Keys.Space),
            IsRunning: _runToggled);
    }

    /// <summary>
    /// Synchronize keyboard state without emitting one-frame actions.
    /// Use this while gameplay input is blocked by overlays/transitions.
    /// </summary>
    public void Consume()
    {
        _currentState = Keyboard.GetState();
        _previousState = _currentState;

        State = new ThirdPersonInputState(
            ExitRequested: false,
            MoveX: 0f,
            MoveZ: 0f,
            Turn: 0f,
            Pitch: 0f,
            Zoom: 0f,
            JumpPressed: false,
            IsRunning: _runToggled);
    }

    private bool IsPressed(Keys key) => _currentState.IsKeyDown(key) && !_previousState.IsKeyDown(key);

    private float Axis(Keys negativePrimary, Keys? negativeAlt, Keys positivePrimary, Keys? positiveAlt)
    {
        var value = 0f;
        if (_currentState.IsKeyDown(negativePrimary) || (negativeAlt.HasValue && _currentState.IsKeyDown(negativeAlt.Value)))
            value -= 1f;
        if (_currentState.IsKeyDown(positivePrimary) || (positiveAlt.HasValue && _currentState.IsKeyDown(positiveAlt.Value)))
            value += 1f;
        return MathHelper.Clamp(value, -1f, 1f);
    }
}
