using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core;

namespace PokemonGreen;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private GameWorld _world;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        _graphics.PreferredBackBufferWidth = 800;
        _graphics.PreferredBackBufferHeight = 600;
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        _world = new GameWorld("test_two_layer_map");
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _world!.LoadTextures(Content, GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        if (_world!.Update(gameTime))
        {
            Exit();
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        _world!.Draw(_spriteBatch!, GraphicsDevice.Viewport);

        base.Draw(gameTime);
    }
}