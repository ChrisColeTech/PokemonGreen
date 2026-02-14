using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core;

namespace PokemonGreen;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private GameWorld _world;
    private int _frameCount;
    private int _lastViewportWidth;
    private int _lastViewportHeight;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        _graphics.PreferredBackBufferWidth = 800;
        _graphics.PreferredBackBufferHeight = 600;
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += OnClientSizeChanged;
        Console.WriteLine("[CTOR] Window.AllowUserResizing = true");
    }

    private void OnClientSizeChanged(object sender, EventArgs e)
    {
        Console.WriteLine($"[RESIZE EVENT] Fired!");
        Console.WriteLine($"[RESIZE EVENT] Window.ClientBounds: {Window.ClientBounds.Width}x{Window.ClientBounds.Height}");
        
        if (GraphicsDevice == null)
        {
            Console.WriteLine("[RESIZE EVENT] GraphicsDevice is null, skipping");
            return;
        }
        
        Console.WriteLine($"[RESIZE EVENT] GraphicsDevice.Viewport: {GraphicsDevice.Viewport.Width}x{GraphicsDevice.Viewport.Height}");
        Console.WriteLine($"[RESIZE EVENT] Current PreferredBackBuffer: {_graphics.PreferredBackBufferWidth}x{_graphics.PreferredBackBufferHeight}");
        
        _graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
        _graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
        _graphics.ApplyChanges();
        
        Console.WriteLine($"[RESIZE EVENT] New PreferredBackBuffer: {_graphics.PreferredBackBufferWidth}x{_graphics.PreferredBackBufferHeight}");
        Console.WriteLine($"[RESIZE EVENT] After ApplyChanges Viewport: {GraphicsDevice.Viewport.Width}x{GraphicsDevice.Viewport.Height}");
    }

    protected override void Initialize()
    {
        _world = new GameWorld("test_legacy_map");
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
        _frameCount++;
        
        var vp = GraphicsDevice.Viewport;
        if (vp.Width != _lastViewportWidth || vp.Height != _lastViewportHeight)
        {
            Console.WriteLine($"[DRAW Frame {_frameCount}] Viewport changed: {vp.Width}x{vp.Height} (was {_lastViewportWidth}x{_lastViewportHeight})");
            _lastViewportWidth = vp.Width;
            _lastViewportHeight = vp.Height;
        }
        
        GraphicsDevice.Clear(new Color(156, 205, 255));

        _world!.Draw(_spriteBatch!, vp);

        base.Draw(gameTime);
    }
}
