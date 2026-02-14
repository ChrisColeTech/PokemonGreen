using Microsoft.Xna.Framework.Input;

namespace PokemonGreen.Core.Systems;

/// <summary>
/// Represents the four cardinal movement directions.
/// </summary>
public enum Direction
{
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// Manages keyboard input with edge detection for single-press actions.
/// </summary>
public class InputManager
{
    private KeyboardState _previousState;
    private KeyboardState _currentState;

    /// <summary>
    /// Gets the current movement direction based on WASD or arrow keys.
    /// Returns null if no movement key is pressed.
    /// Priority: Up > Down > Left > Right (for simultaneous presses).
    /// </summary>
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

    /// <summary>
    /// Gets whether the run modifier (Shift) is currently held.
    /// </summary>
    public bool IsRunning =>
        _currentState.IsKeyDown(Keys.LeftShift) || _currentState.IsKeyDown(Keys.RightShift);

    /// <summary>
    /// Gets whether the jump key (Space) was just pressed this frame.
    /// Single-press detection: returns true only on the frame the key is first pressed.
    /// </summary>
    public bool JumpPressed =>
        _currentState.IsKeyDown(Keys.Space) && !_previousState.IsKeyDown(Keys.Space);

    /// <summary>
    /// Gets whether the interact key (E or Enter) was just pressed this frame.
    /// Single-press detection: returns true only on the frame the key is first pressed.
    /// </summary>
    public bool InteractPressed =>
        (_currentState.IsKeyDown(Keys.E) && !_previousState.IsKeyDown(Keys.E)) ||
        (_currentState.IsKeyDown(Keys.Enter) && !_previousState.IsKeyDown(Keys.Enter));

    /// <summary>
    /// Creates a new InputManager and initializes keyboard state.
    /// </summary>
    public InputManager()
    {
        _currentState = Keyboard.GetState();
        _previousState = _currentState;
    }

    /// <summary>
    /// Updates the input state. Must be called once per frame at the start of Update.
    /// </summary>
    public void Update()
    {
        _previousState = _currentState;
        _currentState = Keyboard.GetState();
    }

    /// <summary>
    /// Checks if a specific key is currently held down.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key is currently pressed.</returns>
    public bool IsKeyDown(Keys key) => _currentState.IsKeyDown(key);

    /// <summary>
    /// Checks if a specific key was just pressed this frame.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key was pressed this frame but not the previous frame.</returns>
    public bool IsKeyPressed(Keys key) =>
        _currentState.IsKeyDown(key) && !_previousState.IsKeyDown(key);

    /// <summary>
    /// Checks if a specific key was just released this frame.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key was released this frame.</returns>
    public bool IsKeyReleased(Keys key) =>
        !_currentState.IsKeyDown(key) && _previousState.IsKeyDown(key);
}
