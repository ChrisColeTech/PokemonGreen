using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.Audio;
using PokemonGreen.Core.Graphics;
using PokemonGreen.Core.Input;
using PokemonGreen.Core.Maps;
using PokemonGreen.Core.Rendering;

namespace PokemonGreen.Core;

public class GameWorld
{
    private TextureStore? _textures;
    private TileRenderer? _tileRenderer;
    private PlayerRenderer? _playerRenderer;
    private SoundManager _soundManager = null!;
    private bool _isPaused;
    private float _animationTimer;
    private int _waterFrameIndex;

    public TileMap TileMap { get; private set; }
    public Player Player { get; }
    public Camera Camera { get; } = new();
    public InputManager Input { get; } = new();
    public TileRenderCatalog TileRenderCatalog { get; } = new();
    public bool IsPaused => _isPaused;
    public bool ShouldQuit { get; private set; }

    public GameWorld(string mapId)
    {
        var activeMap = MapCatalog.TryGetById(mapId, out var generatedMap)
            ? generatedMap!
            : TestTwoLayerMap.Instance;

        TileMap = activeMap.CreateTileMap();
        var spawnPosition = FindSpawnPosition();
        Player = new Player(spawnPosition.X, spawnPosition.Y);
    }

    private Vector2 FindSpawnPosition()
    {
        var startX = TileMap.Width / 2;
        var startY = TileMap.Height / 2;

        for (var radius = 0; radius <= Math.Max(TileMap.Width, TileMap.Height); radius += 1)
        {
            var minX = Math.Max(0, startX - radius);
            var maxX = Math.Min(TileMap.Width - 1, startX + radius);
            var minY = Math.Max(0, startY - radius);
            var maxY = Math.Min(TileMap.Height - 1, startY + radius);

            for (var y = minY; y <= maxY; y += 1)
            {
                for (var x = minX; x <= maxX; x += 1)
                {
                    if (!TileMap.IsWalkable(x, y))
                    {
                        continue;
                    }

                    return new Vector2(
                        (x * TileMap.TileSize) + (TileMap.TileSize / 2f),
                        (y * TileMap.TileSize) + (TileMap.TileSize / 2f));
                }
            }
        }

        return new Vector2(TileMap.TileSize / 2f, TileMap.TileSize / 2f);
    }

    public void LoadTextures(ContentManager content, GraphicsDevice graphicsDevice)
    {
        _textures = new TextureStore();
        _textures.Load(content, graphicsDevice);
        _tileRenderer = new TileRenderer(TileMap, TileRenderCatalog, _textures);
        _playerRenderer = new PlayerRenderer(Player, _textures);
        
        _soundManager = SoundManager.Instance;
        _soundManager.Load(content);
    }

    public bool Update(GameTime gameTime)
    {
        ShouldQuit = false;
        Input.Update();

        if (Input.Quit)
        {
            ShouldQuit = true;
            return true;
        }

        if (Input.PauseToggled)
        {
            _isPaused = !_isPaused;
        }

        if (_isPaused)
        {
            return false;
        }

        if (Input.RunToggled)
        {
            Player.ToggleRun();
        }

        if (Input.TriggeredAction.HasValue)
        {
            Player.TriggerAction(Input.TriggeredAction.Value);
        }

        Player.Update(Input.Movement.X, Input.Movement.Y, TileMap, (float)gameTime.ElapsedGameTime.TotalSeconds);

        if (Input.ZoomIn)
        {
            Camera.ZoomIn();
        }

        if (Input.ZoomOut)
        {
            Camera.ZoomOut();
        }

        _animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_animationTimer >= 0.25f)
        {
            _animationTimer = 0f;
            _waterFrameIndex = (_waterFrameIndex + 1) % 4;
        }

        return false;
    }

    public void Draw(SpriteBatch spriteBatch, Viewport viewport)
    {
        Camera.Update(new Vector2(Player.X, Player.Y), TileMap.Width, TileMap.Height, TileMap.TileSize, viewport);

        spriteBatch.Begin(transformMatrix: Camera.Transform, samplerState: SamplerState.PointClamp);

        _tileRenderer!.DrawBaseTiles(spriteBatch);
        _tileRenderer.DrawOverlayTiles(spriteBatch, _waterFrameIndex);

        _playerRenderer!.Draw(spriteBatch);

        spriteBatch.End();
    }
}