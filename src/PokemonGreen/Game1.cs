#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokemonGreen.Assets;
using PokemonGreen.Core;
using PokemonGreen.Core.Battle;
using PokemonGreen.Core.Items;
using PokemonGreen.Core.Maps;
using PokemonGreen.Core.Pokemon;
using PokemonGreen.Core.Rendering;
using PokemonGreen.Core.Systems;
using PokemonGreen.Core.UI;
using PokemonGreen.Core.UI.Fonts;
using PokemonGreen.Core.UI.Screens;

namespace PokemonGreen;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private GameWorld _gameWorld = null!;
    private PlayerRenderer _playerRenderer = null!;
    private Texture2D _pixelTexture = null!;
    private SpriteFont _battleFont = null!;
    private KermFont? _kermFont;
    private KermFontRenderer? _kermFontRenderer;
    private MouseState _previousMouseState;
    private KeyboardState _previousKeyboardState;

    // Screen overlay stack
    private readonly Stack<IScreenOverlay> _overlayStack = new();

    // Player data
    private Party _playerParty = null!;
    private PlayerInventory _playerBag = null!;

    // Battle UI
    private readonly Core.UI.MessageBox _battleMessageBox = new();
    private readonly MenuBox _battleMainMenu = new() { Columns = 2 };
    private readonly MenuBox _battleMoveMenu = new() { Columns = 2 };
    private MenuBox _activeBattleMenu = null!;
    private bool _battleZoomStarted;
    private bool _battleIntroComplete; // foe revealed — show foe info bar
    private bool _allySentOut;         // ally sent out — show ally info bar

    // Battle Pokemon
    private BattlePokemon _allyPokemon = null!;
    private BattlePokemon _foePokemon = null!;
    private BattleTurnManager? _battleTurnManager;

    // Pause menu
    private readonly MenuBox _pauseMenuBox = new() { Columns = 1, UseStandardStyle = true };

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

    // Day/night cycle
    private readonly DayNightCycle _dayNightCycle = new();
    private RenderTarget2D _overworldRenderTarget = null!;

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

        // Create player data
        _playerParty = Party.CreateTestParty();
        _playerBag = PlayerInventory.CreateTestInventory();

        // Set up battle menus
        _battleMainMenu.SetItems(
            new MenuItem("Fight", OpenFightMenu),
            new MenuItem("Bag", () => PushOverlay(new BagScreen(_playerBag))),
            new MenuItem("Pokemon", () => PushOverlay(new PartyScreen(_playerParty, PartyScreenMode.BattleSwitchIn))),
            new MenuItem("Run", () => _gameWorld.ExitBattle()));

        _battleMoveMenu.OnCancel = () => SwitchBattleMenu(_battleMainMenu, "What will you do?");

        _activeBattleMenu = _battleMainMenu;

        // Set up pause menu items
        _pauseMenuBox.SetItems(
            new MenuItem("Pokemon", () => PushOverlay(new PartyScreen(_playerParty, PartyScreenMode.PauseMenu))),
            new MenuItem("Bag", () => PushOverlay(new BagScreen(_playerBag))),
            new MenuItem("Save"),
            new MenuItem("Close", ClosePauseMenu));
        _pauseMenuBox.OnCancel = ClosePauseMenu;

        if (DebugStartInBattle)
        {
            _gameWorld.DebugEnterBattle();
            EnterBattle();
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

        _overworldRenderTarget = new RenderTarget2D(GraphicsDevice,
            GraphicsDevice.PresentationParameters.BackBufferWidth,
            GraphicsDevice.PresentationParameters.BackBufferHeight);

        // Load KermFont (Battle.kermfont from old engine)
        string kermFontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "Content", "Fonts", "Kerm", "Battle.kermfont");
        if (File.Exists(kermFontPath))
        {
            // Subtle shadow palette — outline is semi-transparent so it doesn't look thick at 3x
            var palette = new[]
            {
                Color.Transparent,
                new Color(239, 239, 239, 255),  // main text
                new Color(80, 80, 80, 90),       // shadow — semi-transparent dark
                new Color(40, 40, 40, 60),
            };
            _kermFont = new KermFont(GraphicsDevice, kermFontPath, palette: palette);
            _kermFontRenderer = new KermFontRenderer(_kermFont);
        }

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
                _overworldRenderTarget?.Dispose();
                _overworldRenderTarget = new RenderTarget2D(GraphicsDevice, w, h);
            }
        }

        // Input state
        var mouseState = Mouse.GetState();
        var kbState = Keyboard.GetState();
        bool mouseClicked = mouseState.LeftButton == ButtonState.Released
            && _previousMouseState.LeftButton == ButtonState.Pressed;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Transform mouse from window-client coords to backbuffer coords (handles DPI scaling)
        int vw = GraphicsDevice.Viewport.Width;
        int vh = GraphicsDevice.Viewport.Height;
        int cw = Window.ClientBounds.Width;
        int ch = Window.ClientBounds.Height;
        Point mousePos = (cw > 0 && ch > 0 && (cw != vw || ch != vh))
            ? new Point(mouseState.Position.X * vw / cw, mouseState.Position.Y * vh / ch)
            : mouseState.Position;

        // Convert to virtual UI coordinates (800x600) for hit testing
        Point virtualMouse = ScreenToVirtual(mousePos);

        var inputState = new InputState
        {
            Left = KeyPressed(kbState, Keys.Left) || KeyPressed(kbState, Keys.A),
            Right = KeyPressed(kbState, Keys.Right) || KeyPressed(kbState, Keys.D),
            Up = KeyPressed(kbState, Keys.Up) || KeyPressed(kbState, Keys.W),
            Down = KeyPressed(kbState, Keys.Down) || KeyPressed(kbState, Keys.S),
            Confirm = KeyPressed(kbState, Keys.Enter) || KeyPressed(kbState, Keys.Space) || KeyPressed(kbState, Keys.E),
            Cancel = KeyPressed(kbState, Keys.Escape) || KeyPressed(kbState, Keys.Back) || KeyPressed(kbState, Keys.B),
            MousePosition = virtualMouse,
            MouseClicked = mouseClicked,
        };

        // Overlay stack takes priority over all other input
        if (_overlayStack.Count > 0)
        {
            var top = _overlayStack.Peek();
            top.Update(dt, inputState);

            if (top.IsFinished)
            {
                _overlayStack.Pop();
                // Re-activate the menu that launched the overlay
                if (_gameWorld.State == GameWorld.GameState.Battle)
                {
                    _activeBattleMenu.IsActive = true;
                    _battleMessageBox.Show("What will you do?");
                }
                else if (_gameWorld.State == GameWorld.GameState.PauseMenu)
                    _pauseMenuBox.IsActive = true;
            }
        }
        else
        {
            // ESC toggles pause menu in overworld
            if (KeyPressed(kbState, Keys.Escape))
            {
                if (_gameWorld.State == GameWorld.GameState.PauseMenu)
                    ClosePauseMenu();
                else if (_gameWorld.State == GameWorld.GameState.Overworld)
                    OpenPauseMenu();
            }

            // Handle pause menu input
            if (_gameWorld.State == GameWorld.GameState.PauseMenu)
            {
                _pauseMenuBox.Update(
                    left: false, right: false,
                    up: inputState.Up, down: inputState.Down,
                    confirm: inputState.Confirm,
                    cancel: KeyPressed(kbState, Keys.Back) || KeyPressed(kbState, Keys.B),
                    mousePosition: virtualMouse,
                    mouseClicked: mouseClicked);
            }

            // Handle battle UI input
            if (_gameWorld.State == GameWorld.GameState.Battle && _gameWorld.FadeAlpha <= 0f)
            {
                bool confirm = inputState.Confirm || mouseClicked;

                if (_activeBattleMenu.IsActive)
                {
                    _battleMessageBox.Update(dt, false);
                    _activeBattleMenu.Update(
                        left: inputState.Left, right: inputState.Right,
                        up: inputState.Up, down: inputState.Down,
                        confirm: inputState.Confirm,
                        cancel: KeyPressed(kbState, Keys.Back) || KeyPressed(kbState, Keys.B),
                        mousePosition: virtualMouse,
                        mouseClicked: mouseClicked);
                }
                else if (_battleMessageBox.IsActive)
                {
                    _battleMessageBox.Update(dt, confirm);
                }
            }
        }
        _previousMouseState = mouseState;
        _previousKeyboardState = kbState;

        float deltaTime = dt;

        // Animate battle camera
        if (_battleCamLerp < 1f)
        {
            _battleCamLerp += deltaTime / BattleCamSpeed;
            if (_battleCamLerp >= 1f)
            {
                _battleCamLerp = 1f;
                _battleCamPos = _battleCamTo;
                // Zoom-out finished — show the menu
                if (_battleZoomStarted)
                {
                    _battleZoomStarted = false;
                    _battleIntroComplete = true;
                    // Show "Go! <ally>!" before revealing the menu
                    _battleMessageBox.Show($"Go! {_allyPokemon.Nickname.ToUpper()}!");
                    _battleMessageBox.OnFinished = () =>
                    {
                        _allySentOut = true;
                        _activeBattleMenu.IsActive = true;
                        _battleMessageBox.Clear();
                        _battleMessageBox.Show("What will you do?");
                    };
                }
            }
            else
            {
                // Smooth ease-out interpolation
                float t = 1f - (1f - _battleCamLerp) * (1f - _battleCamLerp);
                _battleCamPos = Vector3.Lerp(_battleCamFrom, _battleCamTo, t);
            }
        }

        // Detect battle entry and reset camera/UI
        var currentState = _gameWorld.State;
        if (currentState == GameWorld.GameState.Battle && _prevGameState != GameWorld.GameState.Battle)
            EnterBattle();
        _prevGameState = currentState;

        // Animate HP bar drain
        if (_gameWorld.State == GameWorld.GameState.Battle)
        {
            _allyPokemon?.UpdateDisplayHP(deltaTime);
            _foePokemon?.UpdateDisplayHP(deltaTime);
        }

        _dayNightCycle.Update(deltaTime);
        TileRenderer.Update(deltaTime);
        _gameWorld.Update(deltaTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        // Use viewport (backbuffer) dimensions for all drawing — matches DPI-adjusted mouse coords
        int screenW = GraphicsDevice.Viewport.Width;
        int screenH = GraphicsDevice.Viewport.Height;

        if (_gameWorld.State == GameWorld.GameState.Battle)
        {
            DrawBattlePlaceholder();
        }
        else
        {
            // Render overworld to off-screen target, then draw with day/night tint
            GraphicsDevice.SetRenderTarget(_overworldRenderTarget);
            GraphicsDevice.Clear(Color.Black);
            DrawOverworld();
            GraphicsDevice.SetRenderTarget(null);

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            _spriteBatch.Draw(_overworldRenderTarget,
                new Rectangle(0, 0, screenW, screenH),
                _dayNightCycle.GetCurrentTint());
            _spriteBatch.End();

            // Pause menu overlay
            if (_gameWorld.State == GameWorld.GameState.PauseMenu)
                DrawPauseMenu();
        }

        // Full-screen overlays (Party, Bag, etc.) — drawn in virtual coordinate space
        if (_overlayStack.Count > 0)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp,
                transformMatrix: GetUITransform());
            foreach (var overlay in _overlayStack)
            {
                overlay.Draw(_spriteBatch, _pixelTexture, _kermFontRenderer, _kermFont,
                    _battleFont, ViewportWidth, ViewportHeight);
            }
            _spriteBatch.End();
        }

        // Black fade overlay (used by all transitions).
        if (_gameWorld.FadeAlpha > 0f)
        {
            _spriteBatch.Begin();
            _spriteBatch.Draw(
                _pixelTexture,
                new Rectangle(0, 0, screenW, screenH),
                Color.Black * _gameWorld.FadeAlpha);
            _spriteBatch.End();
        }

        // White flash overlay (encounter trigger).
        if (_gameWorld.FadeWhiteAlpha > 0f)
        {
            _spriteBatch.Begin();
            _spriteBatch.Draw(
                _pixelTexture,
                new Rectangle(0, 0, screenW, screenH),
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
        // ── 3D scene (actual viewport resolution) ──
        if (_battleEffect != null && _battleBG != null)
        {
            DrawBattle3D();
        }
        else
        {
            // Fallback: dark background if models didn't load
            _spriteBatch.Begin();
            _spriteBatch.Draw(_pixelTexture,
                new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height),
                new Color(24, 24, 40));
            _spriteBatch.End();
        }

        // ── 2D UI overlay (virtual 800x600 coordinate space, scaled to viewport) ──
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp,
            transformMatrix: GetUITransform());

        int w = ViewportWidth;
        int h = ViewportHeight;

        int panelH = 120;
        int panelY = h - panelH - 20;

        // Info bars — foe appears after zoom-out, ally appears after "Go! <name>!"
        int infoBarW = 220;
        if (_battleIntroComplete && _foePokemon != null)
            BattleInfoBar.DrawFoeBar(_spriteBatch, _pixelTexture, _kermFontRenderer, _battleFont,
                new Rectangle(20, 20, infoBarW, 50), _foePokemon);

        if (_allySentOut && _allyPokemon != null)
            BattleInfoBar.DrawAllyBar(_spriteBatch, _pixelTexture, _kermFontRenderer, _battleFont,
                new Rectangle(w - infoBarW - 20, panelY - 70, infoBarW, 66), _allyPokemon, 0.5f);

        if (_activeBattleMenu.IsActive)
        {
            // Menu (bottom-right) + message (bottom-left)
            int menuW = 280;
            int menuX = w - menuW - 20;
            int textBoxW = w - menuW - 60;

            if (_kermFontRenderer != null && _kermFont != null)
            {
                _activeBattleMenu.Draw(_spriteBatch, _kermFontRenderer, _kermFont, _pixelTexture,
                    new Rectangle(menuX, panelY, menuW, panelH), 3);
                _battleMessageBox.Draw(_spriteBatch, _kermFontRenderer, _pixelTexture,
                    new Rectangle(20, panelY, textBoxW, panelH), 3);
            }
            else
            {
                _activeBattleMenu.Draw(_spriteBatch, _battleFont, _pixelTexture,
                    new Rectangle(menuX, panelY, menuW, panelH));
                _battleMessageBox.Draw(_spriteBatch, _battleFont, _pixelTexture,
                    new Rectangle(20, panelY, textBoxW, panelH));
            }
        }
        else if (_battleMessageBox.IsActive)
        {
            // Full-width message box (intro / battle messages)
            int boxW = w - 40;
            if (_kermFontRenderer != null)
                _battleMessageBox.Draw(_spriteBatch, _kermFontRenderer, _pixelTexture,
                    new Rectangle(20, panelY, boxW, panelH), 3);
            else
                _battleMessageBox.Draw(_spriteBatch, _battleFont, _pixelTexture,
                    new Rectangle(20, panelY, boxW, panelH));
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

    private void PushOverlay(IScreenOverlay overlay)
    {
        // Deactivate the current menu so it doesn't receive input
        if (_gameWorld.State == GameWorld.GameState.Battle)
            _activeBattleMenu.IsActive = false;
        else if (_gameWorld.State == GameWorld.GameState.PauseMenu)
            _pauseMenuBox.IsActive = false;
        _overlayStack.Push(overlay);
    }

    private void SwitchBattleMenu(MenuBox target, string message)
    {
        _activeBattleMenu.IsActive = false;
        _activeBattleMenu = target;
        _activeBattleMenu.SelectedIndex = 0;
        _activeBattleMenu.IsActive = true;
        _battleMessageBox.Clear();
        _battleMessageBox.Show(message);
    }

    private void OpenFightMenu()
    {
        BuildMoveMenu();
        SwitchBattleMenu(_battleMoveMenu, "Choose a move!");
    }

    private void BuildMoveMenu()
    {
        var moves = _allyPokemon.Moves;
        var items = new MenuItem[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            var bm = moves[i];
            var data = MoveRegistry.GetMove(bm.MoveId);
            string name = data?.Name ?? $"Move#{bm.MoveId}";
            string label = $"{name,-12} {bm.CurrentPP}/{bm.MaxPP}";
            bool enabled = bm.CurrentPP > 0;
            int moveIndex = i;
            items[i] = new MenuItem(label, () => SelectMove(moveIndex), enabled);
        }
        _battleMoveMenu.SetItems(items);
    }

    private void SelectMove(int moveIndex)
    {
        var bm = _allyPokemon.Moves[moveIndex];
        if (bm.CurrentPP <= 0)
        {
            _battleMessageBox.Clear();
            _battleMessageBox.Show("No PP left for this move!");
            return;
        }
        // Deduct PP and start the turn
        bm.CurrentPP--;
        _battleTurnManager?.StartTurn(moveIndex);
    }

    private void EnterBattle()
    {
        _allyPokemon = BattlePokemon.CreateTestAlly();
        _foePokemon = BattlePokemon.CreateTestFoe();

        _battleTurnManager = new BattleTurnManager(
            _allyPokemon, _foePokemon,
            showMessage: (msg, onDone) =>
            {
                _battleMessageBox.Clear();
                _battleMessageBox.Show(msg);
                _battleMessageBox.OnFinished = onDone;
            },
            hideMenu: () => _activeBattleMenu.IsActive = false,
            returnToMainMenu: () =>
            {
                BuildMoveMenu(); // refresh PP counts
                _activeBattleMenu = _battleMainMenu;
                _activeBattleMenu.SelectedIndex = 0;
                _activeBattleMenu.IsActive = true;
                _battleMessageBox.Clear();
                _battleMessageBox.Show("What will you do?");
            },
            exitBattle: () => _gameWorld.ExitBattle());

        _battleCamPos = BattleCamFoe;
        _battleCamLerp = 1f;
        _battleZoomStarted = false;
        _battleIntroComplete = false;
        _allySentOut = false;
        _activeBattleMenu = _battleMainMenu;
        _activeBattleMenu.IsActive = false;
        _activeBattleMenu.SelectedIndex = 0;
        _battleMessageBox.Clear();
        _battleMessageBox.Show($"Wild {_foePokemon.Nickname.ToUpper()} appeared!");
        _battleMessageBox.OnFinished = () =>
        {
            // Message dismissed → start camera zoom-out
            _battleZoomStarted = true;
            _battleCamFrom = _battleCamPos;
            _battleCamTo = BattleCamDefault;
            _battleCamLerp = 0f;
        };
    }

    private bool KeyPressed(KeyboardState current, Keys key)
    {
        return current.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }

    private void OpenPauseMenu()
    {
        _gameWorld.EnterPauseMenu();
        _pauseMenuBox.IsActive = true;
        _pauseMenuBox.SelectedIndex = 0;
    }

    private void ClosePauseMenu()
    {
        _gameWorld.ExitPauseMenu();
        _pauseMenuBox.IsActive = false;
    }

    /// <summary>
    /// Compute a transform matrix that maps virtual 800x600 coordinates
    /// to the actual viewport, maintaining aspect ratio (letterboxed).
    /// </summary>
    private Matrix GetUITransform()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        float sx = (float)w / ViewportWidth;
        float sy = (float)h / ViewportHeight;
        float scale = Math.Min(sx, sy);
        float ox = (w - ViewportWidth * scale) / 2f;
        float oy = (h - ViewportHeight * scale) / 2f;
        return Matrix.CreateScale(scale, scale, 1f) * Matrix.CreateTranslation(ox, oy, 0f);
    }

    /// <summary>
    /// Convert a screen-space (backbuffer) point to virtual UI coordinates.
    /// </summary>
    private Point ScreenToVirtual(Point screenPos)
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        float sx = (float)w / ViewportWidth;
        float sy = (float)h / ViewportHeight;
        float scale = Math.Min(sx, sy);
        float ox = (w - ViewportWidth * scale) / 2f;
        float oy = (h - ViewportHeight * scale) / 2f;
        return new Point((int)((screenPos.X - ox) / scale), (int)((screenPos.Y - oy) / scale));
    }

    private void DrawPauseMenu()
    {
        int w = ViewportWidth;
        int menuW = 160;
        int menuH = 180;
        int menuX = w - menuW - 16;
        int menuY = 16;

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp,
            transformMatrix: GetUITransform());
        if (_kermFontRenderer != null && _kermFont != null)
            _pauseMenuBox.Draw(_spriteBatch, _kermFontRenderer, _kermFont, _pixelTexture,
                new Rectangle(menuX, menuY, menuW, menuH), 3);
        else
            _pauseMenuBox.Draw(_spriteBatch, _battleFont, _pixelTexture,
                new Rectangle(menuX, menuY, menuW, menuH));
        _spriteBatch.End();
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
