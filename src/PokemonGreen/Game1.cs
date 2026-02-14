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

    private const int ViewportWidth = 800;
    private const int ViewportHeight = 600;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        // Set window size
        _graphics.PreferredBackBufferWidth = ViewportWidth;
        _graphics.PreferredBackBufferHeight = ViewportHeight;
        _graphics.ApplyChanges();

        // Create GameWorld with viewport size
        _gameWorld = new GameWorld(ViewportWidth, ViewportHeight);

        // Create a simple test map (10x10 grass with walls around border)
        var testMap = CreateTestMap();
        _gameWorld.LoadMap(testMap);

        // Set player starting position (center of map)
        _gameWorld.SetPlayerPosition(5, 5);

        // Create player renderer
        _playerRenderer = new PlayerRenderer();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Initialize TextureStore with the graphics device
        TextureStore.Initialize(GraphicsDevice);
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

        // End SpriteBatch
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    /// <summary>
    /// Creates a test map: 10x10 with grass interior and walls around the border.
    /// </summary>
    private static TileMap CreateTestMap()
    {
        const int mapWidth = 10;
        const int mapHeight = 10;

        var map = new TileMap(mapWidth, mapHeight);

        // Tile IDs from TileRegistry:
        // 1 = Grass (walkable)
        // 80 = Wall (not walkable)

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                // Check if this is a border tile
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
