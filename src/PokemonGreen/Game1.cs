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
        // Set window size
        _graphics.PreferredBackBufferWidth = ViewportWidth;
        _graphics.PreferredBackBufferHeight = ViewportHeight;
        _graphics.ApplyChanges();

        // Create GameWorld with viewport size
        _gameWorld = new GameWorld(ViewportWidth, ViewportHeight);

        // Register all generated maps
        MapRegistry.Initialize();

        // Try loading from MapCatalog first, fall back to test map
        var allMaps = MapCatalog.GetAllMaps();
        if (allMaps.Count > 0)
        {
            var firstMap = allMaps.First();
            _gameWorld.LoadMap(firstMap);
        }
        else
        {
            var testMap = CreateTestMap();
            _gameWorld.LoadMap(testMap);
            _gameWorld.SetPlayerPosition(5, 5);
        }

        // Create player renderer
        _playerRenderer = new PlayerRenderer();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Create a 1x1 white pixel for drawing overlays
        _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        // Initialize TextureStore with the graphics device
        TextureStore.Initialize(GraphicsDevice);

        // Load player sprite sheets
        _playerRenderer.LoadContent(Content.RootDirectory);
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        // Get deltaTime from gameTime
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Update the game world
        _gameWorld.Update(deltaTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // Clear to CornflowerBlue
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // Begin SpriteBatch
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            null,
            null,
            null,
            null);

        // Draw tiles
        if (_gameWorld.CurrentMap != null)
        {
            TileRenderer.DrawMap(
                _spriteBatch,
                _gameWorld.CurrentMap,
                _gameWorld.Camera,
                GraphicsDevice,
                GameWorld.TileSize);
        }

        // Draw player
        _playerRenderer.Draw(
            _spriteBatch,
            _gameWorld.Player,
            _gameWorld.Camera,
            GraphicsDevice,
            GameWorld.TileSize);

        // Draw fade overlay
        if (_gameWorld.FadeAlpha > 0f)
        {
            _spriteBatch.Draw(
                _pixelTexture,
                new Rectangle(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height),
                Color.Black * _gameWorld.FadeAlpha);
        }

        // End SpriteBatch
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void OnClientSizeChanged(object? sender, System.EventArgs e)
    {
        int w = Window.ClientBounds.Width;
        int h = Window.ClientBounds.Height;
        if (w <= 0 || h <= 0) return;

        _gameWorld.OnViewportResized(w, h);
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
