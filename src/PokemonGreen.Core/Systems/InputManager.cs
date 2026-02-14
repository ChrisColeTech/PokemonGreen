using Microsoft.Xna.Framework.Input;

namespace PokemonGreen.Core.Systems;

public enum Direction
{
    Up,
    Down,
    Left,
    Right
}

public class InputManager
{
    private KeyboardState _previousState;
    private KeyboardState _currentState;
    private bool _runToggled;

    public Direction? MoveDirection
    {
        get
        {
            if (_currentState.IsKeyDown(Keys.W) || _currentState.IsKeyDown(Keys.Up))
                return Direction.Up;
            if (_currentState.IsKeyDown(Keys.S) || _currentState.IsKeyDown(Keys.Down))
                return Direction.Down;
            if (_currentState.IsKeyDown(Keys.A) || _currentState.IsKeyDown(Keys.Left))
                return Direction.Left;
            if (_currentState.IsKeyDown(Keys.D) || _currentState.IsKeyDown(Keys.Right))
                return Direction.Right;
            return null;
        }
    }

    public bool IsRunning => _runToggled;

    public bool JumpPressed =>
        _currentState.IsKeyDown(Keys.Space) && !_previousState.IsKeyDown(Keys.Space);

    public bool InteractPressed =>
        (_currentState.IsKeyDown(Keys.E) && !_previousState.IsKeyDown(Keys.E)) ||
        (_currentState.IsKeyDown(Keys.Enter) && !_previousState.IsKeyDown(Keys.Enter));

    public bool RunTogglePressed =>
        (_currentState.IsKeyDown(Keys.LeftShift) && !_previousState.IsKeyDown(Keys.LeftShift)) ||
        (_currentState.IsKeyDown(Keys.RightShift) && !_previousState.IsKeyDown(Keys.RightShift));

    public InputManager()
    {
        _currentState = Keyboard.GetState();
        _previousState = _currentState;
    }

    public void Update()
    {
        _previousState = _currentState;
        _currentState = Keyboard.GetState();

        if (RunTogglePressed)
            _runToggled = !_runToggled;
    }

    public bool IsKeyDown(Keys key) => _currentState.IsKeyDown(key);
    public bool IsKeyPressed(Keys key) => _currentState.IsKeyDown(key) && !_previousState.IsKeyDown(key);
    public bool IsKeyReleased(Keys key) => !_currentState.IsKeyDown(key) && _previousState.IsKeyDown(key);
}
