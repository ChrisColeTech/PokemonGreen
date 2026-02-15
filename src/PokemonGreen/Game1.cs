#nullable enable
using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokemonGreen.Assets;
using PokemonGreen.Core;
using PokemonGreen.Core.Maps;
using PokemonGreen.Core.Rendering;

namespace PokemonGreen;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private GameWorld _gameWorld = null!;
    private PlayerRenderer _playerRenderer = null!;
    private Texture2D _pixelTexture = null!;
    private SpriteFont _battleFont = null!;
    private MouseState _previousMouseState;
    private KeyboardState _previousKeyboardState;
    private int _battleMenuIndex; // 0=Fight, 1=Bag, 2=Pokemon, 3=Run
    private enum BattlePhase { Intro, ZoomOut, Menu }
    private BattlePhase _battlePhase;

    // Battle 3D scene
    private BattleModelData? _battleBG;
    private BattleModelData? _battlePlatformAlly;
    private BattleModelData? _battlePlatformFoe;
    private BasicEffect? _battleEffect;

    // Battle camera animation
    private static readonly Vector3 BattleCamFoe = new(6.9f, 7f, 4.6f);   // zoomed on foe
    private static readonly Vector3 BattleCamDefault = new(7f, 7f, 15f);   // full battle view
    private Vector3 _battleCamPos;
    private Vector3 _battleCamFrom;
    private Vector3 _battleCamTo;
    private float _battleCamLerp = 1f; // 1 = arrived
    private const float BattleCamSpeed = 0.4f; // seconds for full transition
    private GameWorld.GameState _prevGameState;

    private const int ViewportWidth = 800;
    private const int ViewportHeight = 600;
    private bool _windowResized;

    // Set to true to launch directly into the battle screen for debugging.
    private const bool DebugStartInBattle = true;

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

        WorldRegistry.Initialize();
        MapRegistry.Initialize();
        _gameWorld = new GameWorld(ViewportWidth, ViewportHeight);

        var defaultWorld = WorldRegistry.GetWorld(WorldRegistry.DefaultWorldId);
        if (defaultWorld != null
            && MapCatalog.TryGetMap(defaultWorld.SpawnMapId, out var startMap)
            && startMap != null)
        {
            _gameWorld.LoadMap(startMap, defaultWorld.SpawnX, defaultWorld.SpawnY);
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

        if (DebugStartInBattle)
        {
            _gameWorld.DebugEnterBattle();
            _battleCamPos = BattleCamFoe;
            _battleCamLerp = 1f;
            _battlePhase = BattlePhase.Intro;
        }

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        TextureStore.Initialize(GraphicsDevice);
        _battleFont = Content.Load<SpriteFont>("Fonts/BattleFont");

        // Load 3D battle scene models
        LoadBattleModels();

        _gameWorld.Camera.ViewportWidth = GraphicsDevice.Viewport.Width;
        _gameWorld.Camera.ViewportHeight = GraphicsDevice.Viewport.Height;
    }

    private void LoadBattleModels()
    {
        string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BattleBG");

        try
        {
            string bgPath = Path.Combine(basePath, "Grass", "Grass.dae");
            _battleBG = BattleModelLoader.Load(bgPath, GraphicsDevice);
            LogModelBounds("Grass.dae", _battleBG);

            string allyPath = Path.Combine(basePath, "PlatformGrassAlly", "GrassAlly.dae");
            _battlePlatformAlly = BattleModelLoader.Load(allyPath, GraphicsDevice);
            LogModelBounds("GrassAlly.dae", _battlePlatformAlly);

            string foePath = Path.Combine(basePath, "PlatformGrassFoe", "GrassFoe.dae");
            _battlePlatformFoe = BattleModelLoader.Load(foePath, GraphicsDevice);
            LogModelBounds("GrassFoe.dae", _battlePlatformFoe);

            // Set up the 3D effect
            _battleEffect = new BasicEffect(GraphicsDevice)
            {
                TextureEnabled = true,
                LightingEnabled = true,
                AmbientLightColor = new Vector3(0.85f, 0.85f, 0.85f),
            };
            _battleEffect.EnableDefaultLighting();
        }
        catch (Exception ex)
        {
            string errorMsg = $"[Battle3D] FAILED: {ex.Message}\n{ex.StackTrace}";
            Console.WriteLine(errorMsg);
            File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "battle3d_log.txt"), errorMsg + "\n");
        }
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
            if (w > 0 && h > 0)
            {
                _graphics.PreferredBackBufferWidth = w;
                _graphics.PreferredBackBufferHeight = h;
                _graphics.ApplyChanges();
                _gameWorld.OnViewportResized(w, h);
            }
        }

        // Handle battle UI input.
        var mouseState = Mouse.GetState();
        var kbState = Keyboard.GetState();
        bool mouseClicked = mouseState.LeftButton == ButtonState.Pressed
            && _previousMouseState.LeftButton == ButtonState.Released;

        if (_gameWorld.State == GameWorld.GameState.Battle && _gameWorld.FadeAlpha <= 0f)
        {
            bool anyKeyPressed = kbState.GetPressedKeyCount() > 0 && _previousKeyboardState.GetPressedKeyCount() == 0;

            if (_battlePhase == BattlePhase.Intro)
            {
                // Any key or click advances to zoom-out
                if (anyKeyPressed || mouseClicked)
                {
                    _battlePhase = BattlePhase.ZoomOut;
                    _battleCamFrom = _battleCamPos;
                    _battleCamTo = BattleCamDefault;
                    _battleCamLerp = 0f;
                }
            }
            else if (_battlePhase == BattlePhase.Menu)
            {
                // Arrow key navigation (2x2 grid: row = index/2, col = index%2)
                if (KeyPressed(kbState, Keys.Left) || KeyPressed(kbState, Keys.A))
                    _battleMenuIndex = (_battleMenuIndex % 2 == 1) ? _battleMenuIndex - 1 : _battleMenuIndex;
                if (KeyPressed(kbState, Keys.Right) || KeyPressed(kbState, Keys.D))
                    _battleMenuIndex = (_battleMenuIndex % 2 == 0) ? _battleMenuIndex + 1 : _battleMenuIndex;
                if (KeyPressed(kbState, Keys.Up) || KeyPressed(kbState, Keys.W))
                    _battleMenuIndex = (_battleMenuIndex >= 2) ? _battleMenuIndex - 2 : _battleMenuIndex;
                if (KeyPressed(kbState, Keys.Down) || KeyPressed(kbState, Keys.S))
                    _battleMenuIndex = (_battleMenuIndex < 2) ? _battleMenuIndex + 2 : _battleMenuIndex;

                // Mouse click on any menu item
                if (mouseClicked)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (GetMenuItemRect(i).Contains(mouseState.Position))
                        {
                            _battleMenuIndex = i;
                            ConfirmBattleMenu();
                            break;
                        }
                    }
                }

                // Confirm with Enter/Space/E
                if (KeyPressed(kbState, Keys.Enter) || KeyPressed(kbState, Keys.Space) || KeyPressed(kbState, Keys.E))
                    ConfirmBattleMenu();
            }
            // ZoomOut phase: no input, just wait for animation to finish
        }
        _previousMouseState = mouseState;
        _previousKeyboardState = kbState;

        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Animate battle camera
        if (_battleCamLerp < 1f)
        {
            _battleCamLerp += deltaTime / BattleCamSpeed;
            if (_battleCamLerp >= 1f)
            {
                _battleCamLerp = 1f;
                _battleCamPos = _battleCamTo;
                // Zoom-out finished — show the menu
                if (_battlePhase == BattlePhase.ZoomOut)
                    _battlePhase = BattlePhase.Menu;
            }
            else
            {
                // Smooth ease-out interpolation
                float t = 1f - (1f - _battleCamLerp) * (1f - _battleCamLerp);
                _battleCamPos = Vector3.Lerp(_battleCamFrom, _battleCamTo, t);
            }
        }

        // Detect battle entry and reset camera to foe focus
        var currentState = _gameWorld.State;
        if (currentState == GameWorld.GameState.Battle && _prevGameState != GameWorld.GameState.Battle)
        {
            _battleCamPos = BattleCamFoe;
            _battleCamLerp = 1f;
            _battleMenuIndex = 0;
            _battlePhase = BattlePhase.Intro;
        }
        _prevGameState = currentState;

        TileRenderer.Update(deltaTime);
        _gameWorld.Update(deltaTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        if (_gameWorld.State == GameWorld.GameState.Battle)
        {
            DrawBattlePlaceholder();
        }
        else
        {
            DrawOverworld();
        }

        // Black fade overlay (used by all transitions).
        if (_gameWorld.FadeAlpha > 0f)
        {
            _spriteBatch.Begin();
            _spriteBatch.Draw(
                _pixelTexture,
                new Rectangle(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height),
                Color.Black * _gameWorld.FadeAlpha);
            _spriteBatch.End();
        }

        // White flash overlay (encounter trigger).
        if (_gameWorld.FadeWhiteAlpha > 0f)
        {
            _spriteBatch.Begin();
            _spriteBatch.Draw(
                _pixelTexture,
                new Rectangle(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height),
                Color.White * _gameWorld.FadeWhiteAlpha);
            _spriteBatch.End();
        }

        base.Draw(gameTime);
    }

    private void DrawOverworld()
    {
        var transformMatrix = _gameWorld.Camera.GetTransformMatrix();
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.NonPremultiplied,
            SamplerState.PointClamp,
            null,
            null,
            null,
            transformMatrix);

        if (_gameWorld.CurrentMap != null)
        {
            int playerTileX = _gameWorld.Player.TileX;
            int playerTileY = _gameWorld.Player.TileY;

            TileRenderer.DrawBaseTiles(
                _spriteBatch,
                _gameWorld.CurrentMap,
                _gameWorld.Camera,
                GameWorld.TileSize);

            TileRenderer.DrawOverlaysBehindPlayer(
                _spriteBatch,
                _gameWorld.CurrentMap,
                _gameWorld.Camera,
                GameWorld.TileSize,
                playerTileX,
                playerTileY);

            _playerRenderer.Draw(
                _spriteBatch,
                _gameWorld.Player,
                _gameWorld.Camera,
                GameWorld.TileSize);

            TileRenderer.DrawOverlaysInFrontOfPlayer(
                _spriteBatch,
                _gameWorld.CurrentMap,
                _gameWorld.Camera,
                GameWorld.TileSize,
                playerTileX,
                playerTileY);
        }
        else
        {
            _playerRenderer.Draw(
                _spriteBatch,
                _gameWorld.Player,
                _gameWorld.Camera,
                GameWorld.TileSize);
        }

        _spriteBatch.End();
    }

    private void DrawBattlePlaceholder()
    {
        int w = Window.ClientBounds.Width;
        int h = Window.ClientBounds.Height;

        // ── 3D scene ──
        if (_battleEffect != null && _battleBG != null)
        {
            DrawBattle3D();
        }
        else
        {
            // Fallback: dark background if models didn't load
            _spriteBatch.Begin();
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, w, h), new Color(24, 24, 40));
            _spriteBatch.End();
        }

        // ── 2D UI overlay ──
        _spriteBatch.Begin();

        int menuH = 120;
        int menuY = h - menuH - 20;

        if (_battlePhase == BattlePhase.Intro)
        {
            // Intro: full-width text box with encounter message
            int boxW = w - 40;
            DrawTextBox(20, menuY, boxW, menuH);
            _spriteBatch.DrawString(_battleFont, "Wild POKEMON appeared!",
                new Vector2(32, menuY + 12), Color.White);

            // Prompt to continue
            string prompt = "Press any key...";
            var promptSize = _battleFont.MeasureString(prompt);
            _spriteBatch.DrawString(_battleFont, prompt,
                new Vector2(boxW - promptSize.X, menuY + menuH - promptSize.Y - 12),
                Color.Gray);
        }
        else if (_battlePhase == BattlePhase.Menu)
        {
            // Menu box (bottom-right)
            int menuW = 200;
            int menuX = w - menuW - 20;
            DrawTextBox(menuX, menuY, menuW, menuH);

            // Menu options
            string[] menuItems = ["Fight", "Bag", "Pokemon", "Run"];
            int itemH = menuH / 2;
            int itemW = menuW / 2;
            var mouseState = Mouse.GetState();

            for (int i = 0; i < menuItems.Length; i++)
            {
                int col = i % 2;
                int row = i / 2;
                int ix = menuX + col * itemW;
                int iy = menuY + row * itemH;
                var itemRect = new Rectangle(ix, iy, itemW, itemH);
                bool selected = i == _battleMenuIndex;
                bool hovering = itemRect.Contains(mouseState.Position);

                if (selected)
                    _spriteBatch.Draw(_pixelTexture, itemRect, new Color(70, 70, 110));
                else if (hovering)
                    _spriteBatch.Draw(_pixelTexture, itemRect, new Color(55, 55, 80));

                string label = selected ? "> " + menuItems[i] : "  " + menuItems[i];
                var textSize = _battleFont.MeasureString(label);
                _spriteBatch.DrawString(_battleFont, label,
                    new Vector2(ix + 8, iy + (itemH - textSize.Y) / 2),
                    selected ? Color.Yellow : Color.White);
            }

            // Text box (bottom-left)
            int textBoxW = w - menuW - 60;
            DrawTextBox(20, menuY, textBoxW, menuH);
            _spriteBatch.DrawString(_battleFont, "What will you do?",
                new Vector2(32, menuY + 12), Color.White);
        }

        _spriteBatch.End();
    }

    private void DrawBattle3D()
    {
        var device = GraphicsDevice;
        float aspect = device.Viewport.AspectRatio;

        // Camera from old engine: yaw=-22°, pitch=13°, position animated
        var cameraPos = _battleCamPos;
        float yaw = -22f;
        float pitch = 13f;
        float degToRad = MathHelper.Pi / 180f;
        var quat = Quaternion.CreateFromYawPitchRoll(-yaw * degToRad, -pitch * degToRad, 0f);
        var view = Matrix.CreateTranslation(-cameraPos) *
                   Matrix.CreateFromQuaternion(Quaternion.Conjugate(quat));

        // FOV ~26° derived from old engine's custom projection matrix (DS BW2 style)
        var projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(26f), aspect, 1f, 512f);

        // Enable depth buffer, disable culling so we see all faces
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullNone;
        device.SamplerStates[0] = SamplerState.PointClamp;
        device.BlendState = BlendState.AlphaBlend;

        _battleEffect!.View = view;
        _battleEffect.Projection = projection;
        _battleEffect.TextureEnabled = true;
        _battleEffect.LightingEnabled = false;
        _battleEffect.VertexColorEnabled = false;
        _battleEffect.Alpha = 1f;

        // Background at origin
        _battleEffect.World = Matrix.Identity;
        _battleEffect.DiffuseColor = Vector3.One;
        _battleBG!.Draw(device, _battleEffect);

        // Foe platform at (0, -0.20, -15)
        if (_battlePlatformFoe != null)
        {
            _battleEffect.World = Matrix.CreateTranslation(0f, -0.20f, -15f);
            _battlePlatformFoe.Draw(device, _battleEffect);
        }

        // Ally platform at (0, -0.20, 3)
        if (_battlePlatformAlly != null)
        {
            _battleEffect.World = Matrix.CreateTranslation(0f, -0.20f, 3f);
            _battlePlatformAlly.Draw(device, _battleEffect);
        }

        // Reset state for 2D rendering
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.BlendState = BlendState.AlphaBlend;
    }

    private static void LogModelBounds(string name, BattleModelData model)
    {
        var min = model.BoundsMin;
        var max = model.BoundsMax;
        string msg = $"[Battle3D] {name}: {model.Meshes.Count} meshes, {model.TotalVertices} verts, " +
            $"{model.TexturedMeshCount} textured, bounds: ({min.X:F1},{min.Y:F1},{min.Z:F1}) to ({max.X:F1},{max.Y:F1},{max.Z:F1})";
        Console.WriteLine(msg);
        System.Diagnostics.Debug.WriteLine(msg);
        File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "battle3d_log.txt"), msg + "\n");
    }

    private void DrawTextBox(int x, int y, int w, int h)
    {
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, w, h), new Color(40, 40, 60));
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, w, 2), Color.White);
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + h - 2, w, 2), Color.White);
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, 2, h), Color.White);
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x + w - 2, y, 2, h), Color.White);
    }

    private bool KeyPressed(KeyboardState current, Keys key)
    {
        return current.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }

    private void ConfirmBattleMenu()
    {
        // Only "Run" (index 3) does anything for now
        if (_battleMenuIndex == 3)
            _gameWorld.ExitBattle();
    }

    private Rectangle GetMenuItemRect(int index)
    {
        int menuW = 200;
        int menuH = 120;
        int menuX = Window.ClientBounds.Width - menuW - 20;
        int menuY = Window.ClientBounds.Height - menuH - 20;
        int itemW = menuW / 2;
        int itemH = menuH / 2;
        int col = index % 2;
        int row = index / 2;
        return new Rectangle(menuX + col * itemW, menuY + row * itemH, itemW, itemH);
    }

    private void OnClientSizeChanged(object? sender, System.EventArgs e)
    {
        _windowResized = true;
    }

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
                    map.SetBaseTile(x, y, 80);
                }
                else
                {
                    map.SetBaseTile(x, y, 1);
                }
            }
        }

        return map;
    }
}
