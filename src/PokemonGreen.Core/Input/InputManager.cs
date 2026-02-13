using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokemonGreen.Core;

namespace PokemonGreen.Core.Input;

public class InputManager
{
    private KeyboardState _previousKeyboardState;
    private Vector2 _movement;

    public Vector2 Movement { get { return _movement; } }
    public bool RunToggled { get; private set; }
    public bool PauseToggled { get; private set; }
    public bool ZoomIn { get; private set; }
    public bool ZoomOut { get; private set; }
    public bool Quit { get; private set; }
    public PlayerState? TriggeredAction { get; private set; }

    public void Update()
    {
        var currentKeyboardState = Keyboard.GetState();

        _movement = Vector2.Zero;
        RunToggled = false;
        PauseToggled = false;
        ZoomIn = false;
        ZoomOut = false;
        Quit = false;
        TriggeredAction = null;

        if (currentKeyboardState.IsKeyDown(Keys.W) || currentKeyboardState.IsKeyDown(Keys.Up))
            _movement.Y -= 1;
        if (currentKeyboardState.IsKeyDown(Keys.S) || currentKeyboardState.IsKeyDown(Keys.Down))
            _movement.Y += 1;
        if (currentKeyboardState.IsKeyDown(Keys.A) || currentKeyboardState.IsKeyDown(Keys.Left))
            _movement.X -= 1;
        if (currentKeyboardState.IsKeyDown(Keys.D) || currentKeyboardState.IsKeyDown(Keys.Right))
            _movement.X += 1;

        if (currentKeyboardState.IsKeyDown(Keys.LeftShift) && _previousKeyboardState.IsKeyUp(Keys.LeftShift))
            RunToggled = true;

        if (currentKeyboardState.IsKeyDown(Keys.P) && _previousKeyboardState.IsKeyUp(Keys.P))
            PauseToggled = true;

        if (currentKeyboardState.IsKeyDown(Keys.OemPlus) || currentKeyboardState.IsKeyDown(Keys.Add))
            ZoomIn = true;
        if (currentKeyboardState.IsKeyDown(Keys.OemMinus) || currentKeyboardState.IsKeyDown(Keys.Subtract))
            ZoomOut = true;

        if (currentKeyboardState.IsKeyDown(Keys.Escape))
            Quit = true;

        if (currentKeyboardState.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space))
            TriggeredAction = PlayerState.Jump;
        else if (currentKeyboardState.IsKeyDown(Keys.Q) && _previousKeyboardState.IsKeyUp(Keys.Q))
            TriggeredAction = PlayerState.Combat;
        else if (currentKeyboardState.IsKeyDown(Keys.E) && _previousKeyboardState.IsKeyUp(Keys.E))
            TriggeredAction = PlayerState.Spellcast;
        else if (currentKeyboardState.IsKeyDown(Keys.R) && _previousKeyboardState.IsKeyUp(Keys.R))
            TriggeredAction = PlayerState.Climb;

        _previousKeyboardState = currentKeyboardState;
    }
}