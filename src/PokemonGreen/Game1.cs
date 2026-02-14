using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokemonGreen.Core;
using PokemonGreen.Core.Maps;
using PokemonGreen.Rendering;

namespace PokemonGreen;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private GameWorld _gameWorld = null!;
    private PlayerRenderer _playerRenderer = null!;
    private Texture2D _pixelTexture = null!;

    private const int ViewportWidth = 800;
    private const int ViewportHeight = 600;
    private bool _windowResized;
    private int _frameCount;
    private int _lastLoggedVpW;
    private int _lastLoggedVpH;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += OnClientSizeChanged;
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = ViewportWidth;
        _graphics.PreferredBackBufferHeight = ViewportHeight;
        _graphics.ApplyChanges();

        MapRegistry.Initialize();

        _gameWorld = new GameWorld(ViewportWidth, ViewportHeight);

        if (MapCatalog.TryGetMap("greenleaf_metro", out var startMap) && startMap != null)
        {
            _gameWorld.LoadMap(startMap);
        }
        else
        {
            var allMaps = MapCatalog.GetAllMaps();
            if (allMaps.Count > 0)
                _gameWorld.LoadMap(allMaps.First());
            else
            {
                _gameWorld.LoadMap(CreateTestMap());
                _gameWorld.SetPlayerPosition(5, 5);
            }
        }

        _playerRenderer = new PlayerRenderer();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        TextureStore.Initialize(GraphicsDevice);

        TileRenderer.LoadSprites(Content.RootDirectory);
        _playerRenderer.LoadContent(Content.RootDirectory);

        _gameWorld.Camera.ViewportWidth = GraphicsDevice.Viewport.Width;
        _gameWorld.Camera.ViewportHeight = GraphicsDevice.Viewport.Height;
        Console.WriteLine($"[INIT LoadContent] GD.Viewport={GraphicsDevice.Viewport.Width}x{GraphicsDevice.Viewport.Height} Camera.Viewport={_gameWorld.Camera.ViewportWidth}x{_gameWorld.Camera.ViewportHeight} Zoom={_gameWorld.Camera.Zoom:F2} BackBuffer={GraphicsDevice.PresentationParameters.BackBufferWidth}x{GraphicsDevice.PresentationParameters.BackBufferHeight}");
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        if (_windowResized)
        {
            _windowResized = false;
            int w = Window.ClientBounds.Width;
            int h = Window.ClientBounds.Height;
            Console.WriteLine($"[RESIZE UPDATE] Processing: ClientBounds={w}x{h}");
            if (w > 0 && h > 0)
            {
                _graphics.PreferredBackBufferWidth = w;
                _graphics.PreferredBackBufferHeight = h;
                _graphics.ApplyChanges();
                var vp = GraphicsDevice.Viewport;
                Console.WriteLine($"[RESIZE UPDATE] After ApplyChanges: Viewport={vp.Width}x{vp.Height} BackBuffer={GraphicsDevice.PresentationParameters.BackBufferWidth}x{GraphicsDevice.PresentationParameters.BackBufferHeight}");
                _gameWorld.OnViewportResized(w, h);
                Console.WriteLine($"[RESIZE UPDATE] Camera: Viewport={_gameWorld.Camera.ViewportWidth}x{_gameWorld.Camera.ViewportHeight} Zoom={_gameWorld.Camera.Zoom:F2} Pos=({_gameWorld.Camera.X:F1},{_gameWorld.Camera.Y:F1})");
            }
        }

        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        TileRenderer.Update(deltaTime);
        _gameWorld.Update(deltaTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _frameCount++;
        var gdVp = GraphicsDevice.Viewport;
        int camW = _gameWorld.Camera.ViewportWidth;
        int camH = _gameWorld.Camera.ViewportHeight;
        if (camW != _lastLoggedVpW || camH != _lastLoggedVpH || _frameCount % 300 == 0)
        {
            Console.WriteLine($"[DRAW #{_frameCount}] GD.Viewport={gdVp.Width}x{gdVp.Height} Camera.Viewport={camW}x{camH} Zoom={_gameWorld.Camera.Zoom:F2} Bounds={_gameWorld.Camera.Bounds}");
            _lastLoggedVpW = camW;
            _lastLoggedVpH = camH;
        }

        GraphicsDevice.Clear(Color.Black);

        var transformMatrix = _gameWorld.Camera.GetTransformMatrix();
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            null,
            null,
            null,
            transformMatrix);

        if (_gameWorld.CurrentMap != null)
        {
            TileRenderer.DrawMap(
                _spriteBatch,
                _gameWorld.CurrentMap,
                _gameWorld.Camera,
                GraphicsDevice,
                GameWorld.TileSize);
        }

        _playerRenderer.Draw(
            _spriteBatch,
            _gameWorld.Player,
            _gameWorld.Camera,
            GraphicsDevice,
            GameWorld.TileSize);

        _spriteBatch.End();

        if (_gameWorld.FadeAlpha > 0f)
        {
            _spriteBatch.Begin();
            _spriteBatch.Draw(
                _pixelTexture,
                new Rectangle(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height),
                Color.Black * _gameWorld.FadeAlpha);
            _spriteBatch.End();
        }

        base.Draw(gameTime);
    }

    private void OnClientSizeChanged(object? sender, System.EventArgs e)
    {
        Console.WriteLine($"[RESIZE EVENT] ClientBounds={Window.ClientBounds.Width}x{Window.ClientBounds.Height} BackBuffer={_graphics.PreferredBackBufferWidth}x{_graphics.PreferredBackBufferHeight}");
        _windowResized = true;
    }

    /// <summary>
    /// Creates a test map: 10x10 with grass interior and walls around the border.
    /// </summary>
    private static TileMap CreateTestMap()
    {
        const int mapWidth = 10;
        const int mapHeight = 10;

        var map = new TileMap(mapWidth, mapHeight);

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                bool isBorder = x == 0 || x == mapWidth - 1 || y == 0 || y == mapHeight - 1;

                if (isBorder)
                {
                    map.SetBaseTile(x, y, 80); // Wall
                }
                else
                {
                    map.SetBaseTile(x, y, 1); // Grass
                }
            }
        }

        return map;
    }
}
