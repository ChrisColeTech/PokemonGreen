#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Assets;
using PokemonGreen.Core.Pokemon;
using PokemonGreen.Core.UI;
using PokemonGreen.Core.UI.Fonts;

namespace PokemonGreen.Core.Battle;

/// <summary>
/// Encapsulates the entire battle state machine: entering/exiting battle,
/// turn management, input handling, 3D scene rendering, and 2D battle UI overlay.
/// Ported directly from the 2D game's Game1.cs battle code.
/// </summary>
public class BattleScreen3D
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly KermFontRenderer? _kermFontRenderer;
    private readonly KermFont? _kermFont;
    private readonly SpriteFont? _battleFont;
    private readonly GraphicsDevice _graphicsDevice;

    // Battle Pokemon
    private BattlePokemon? _allyPokemon;
    private BattlePokemon? _foePokemon;
    private BattleTurnManager? _battleTurnManager;

    // Battle UI
    private readonly Core.UI.MessageBox _battleMessageBox = new();
    private readonly MenuBox _battleMainMenu = new() { Columns = 2 };
    private readonly MenuBox _battleMoveMenu = new() { Columns = 2 };
    private MenuBox _activeBattleMenu = null!;
    private bool _inFightMenu;

    // Unified fight grid navigation (cols 0-1 = moves, cols 2-3 = action panel)
    private int _fightGridCol;
    private int _fightGridRow;

    // Battle intro sequence state
    private bool _battleZoomStarted;
    private bool _battleIntroComplete; // foe revealed — show foe info bar
    private bool _allySentOut;         // ally sent out — show ally info bar

    // Callbacks wired by Game1 for overlay integration
    private Action? _onBagSelected;
    private Action? _onPokemonSelected;

    // Exit callback — called when battle ends (victory, defeat, run)
    private Action? _onExitBattle;

    // ── 3D battle scene models ──────────────────────────────────────
    private readonly Dictionary<BattleBackground, (BattleModelData bg, BattleModelData ally, BattleModelData foe)>
        _battleScenes = new();
    private BattleModelData? _activeBattleBG;
    private BattleModelData? _activePlatformAlly;
    private BattleModelData? _activePlatformFoe;
    private AlphaTestEffect? _battleEffect;

    // Pokemon 3D models on battle field
    private SkeletalModelData? _allyModel;
    private SkeletalModelData? _foeModel;

    // Battle camera animation
    private static readonly Vector3 BattleCamFoe = new(6.9f, 7f, 4.6f);     // zoomed on foe
    private static readonly Vector3 BattleCamDefault = new(7f, 7f, 15f);     // full battle view
    private Vector3 _battleCamPos;
    private Vector3 _battleCamFrom;
    private Vector3 _battleCamTo;
    private float _battleCamLerp = 1f; // 1 = arrived
    private const float BattleCamSpeed = 0.4f; // seconds for full transition

    // Battle background type for current encounter
    private BattleBackground _currentBattleBackground = BattleBackground.Grass;

    /// <summary>True while the battle screen is active.</summary>
    public bool InBattle { get; private set; }

    public BattleScreen3D(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch,
        Texture2D pixel, KermFontRenderer? kermFontRenderer, KermFont? kermFont,
        SpriteFont? battleFont = null)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _kermFontRenderer = kermFontRenderer;
        _kermFont = kermFont;
        _battleFont = battleFont;

        _activeBattleMenu = _battleMainMenu;
        _battleMoveMenu.OnCancel = CloseFightMenu;
    }

    // ── Configuration ───────────────────────────────────────────────

    /// <summary>Configure callbacks for overlay integration (Bag, Pokemon screens).</summary>
    public void SetOverlayCallbacks(Action? onBag, Action? onPokemon)
    {
        _onBagSelected = onBag;
        _onPokemonSelected = onPokemon;
    }

    /// <summary>Configure the exit battle callback.</summary>
    public void SetExitCallback(Action? onExit)
    {
        _onExitBattle = onExit;
    }

    /// <summary>Set the battle background type (Grass, Cave, etc.) before entering battle.</summary>
    public void SetBattleBackground(BattleBackground bg)
    {
        _currentBattleBackground = bg;
    }

    // ── 3D Scene Loading (ported from 2D Game1.LoadBattleModels) ────

    /// <summary>
    /// Load all battle scene 3D models from the BattleBG directory.
    /// Call once during LoadContent.
    /// </summary>
    public void LoadBattleModels(string basePath)
    {
        // Ensure Pokemon3D dev paths are configured for model loading
        ModelLoader.InitializeDevPaths();

        try
        {
            var modelCache = new Dictionary<string, BattleModelData>();

            BattleModelData LoadModel(string relativePath)
            {
                if (modelCache.TryGetValue(relativePath, out var cached))
                    return cached;
                var model = BattleModelLoader.Load(Path.Combine(basePath, relativePath), _graphicsDevice);
                modelCache[relativePath] = model;
                return model;
            }

            // Grass background + Grass platforms
            _battleScenes[BattleBackground.Grass] = (
                LoadModel("Grass/Grass.dae"),
                LoadModel("PlatformGrassAlly/GrassAlly.dae"),
                LoadModel("PlatformGrassFoe/GrassFoe.dae"));

            // TallGrass shares the Grass background, different platforms
            _battleScenes[BattleBackground.TallGrass] = (
                LoadModel("Grass/Grass.dae"),
                LoadModel("PlatformTallGrassAlly/TallGrassAlly.dae"),
                LoadModel("PlatformTallGrassFoe/TallGrassFoe.dae"));

            // Cave background + Cave platforms
            _battleScenes[BattleBackground.Cave] = (
                LoadModel("Cave/Cave.dae"),
                LoadModel("PlatformCaveAlly/CaveAlly.dae"),
                LoadModel("PlatformCaveFoe/CaveFoe.dae"));

            // Dark background + Dark platform
            _battleScenes[BattleBackground.Dark] = (
                LoadModel("Dark/Dark.dae"),
                LoadModel("PlatformDark/Dark.dae"),
                LoadModel("PlatformDark/Dark.dae"));

            // Default active set to Grass
            if (_battleScenes.TryGetValue(BattleBackground.Grass, out var grass))
            {
                _activeBattleBG = grass.bg;
                _activePlatformAlly = grass.ally;
                _activePlatformFoe = grass.foe;
            }

            // AlphaTestEffect discards transparent pixels before depth-write
            _battleEffect = new AlphaTestEffect(_graphicsDevice)
            {
                VertexColorEnabled = false,
                Alpha = 1f,
                ReferenceAlpha = 128,
                AlphaFunction = CompareFunction.GreaterEqual,
            };
        }
        catch (Exception ex)
        {
            string errorMsg = $"[Battle3D] FAILED: {ex.Message}\n{ex.StackTrace}";
            Console.WriteLine(errorMsg);
            File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "battle3d_log.txt"), errorMsg + "\n");
        }
    }

    // ── Enter / Exit Battle ─────────────────────────────────────────

    /// <summary>Start a new battle encounter with real party/encounter data.</summary>
    public void EnterBattle(BattlePokemon ally, BattlePokemon foe)
    {
        InBattle = true;
        _allyPokemon = ally;
        _foePokemon = foe;

        // Load 3D models for the battling Pokemon
        _allyModel = LoadPokemonModel(_allyPokemon.SpeciesId);
        _foeModel = LoadPokemonModel(_foePokemon.SpeciesId);

        SetupBattleScene();
        SetupBattleTurnManager();
        SetupInitialState();
    }

    /// <summary>Start a new battle encounter with test data (for debugging).</summary>
    public void EnterBattle()
    {
        InBattle = true;
        _allyPokemon = BattlePokemon.CreateTestAlly();
        _foePokemon = BattlePokemon.CreateTestFoe();

        // Load 3D models for the battling Pokemon
        _allyModel = LoadPokemonModel(_allyPokemon.SpeciesId);
        _foeModel = LoadPokemonModel(_foePokemon.SpeciesId);

        SetupBattleScene();
        SetupBattleTurnManager();
        SetupInitialState();
    }

    private void SetupBattleScene()
    {
        // Select the battle background set based on encounter type
        if (_battleScenes.TryGetValue(_currentBattleBackground, out var scene))
        {
            _activeBattleBG = scene.bg;
            _activePlatformAlly = scene.ally;
            _activePlatformFoe = scene.foe;
        }

        // Start camera zoomed on foe
        _battleCamPos = BattleCamFoe;
        _battleCamLerp = 1f;
        _battleZoomStarted = false;
    }

    private void SetupBattleTurnManager()
    {
        _battleTurnManager = new BattleTurnManager(
            _allyPokemon!, _foePokemon!,
            showMessage: (msg, onDone) =>
            {
                _battleMessageBox.Clear();
                _battleMessageBox.Show(msg);
                _battleMessageBox.OnFinished = onDone;
            },
            hideMenu: () => _activeBattleMenu.IsActive = false,
            returnToMainMenu: () =>
            {
                BuildMoveMenu();
                _inFightMenu = false;
                ResetMainMenuItems();
                _activeBattleMenu = _battleMainMenu;
                _activeBattleMenu.SelectedIndex = 0;
                _activeBattleMenu.IsActive = true;
                _battleMessageBox.Clear();
                _battleMessageBox.Show("What will you do?");
            },
            exitBattle: () =>
            {
                _allyModel = null;
                _foeModel = null;
                ExitBattle();
                _onExitBattle?.Invoke();
            });

        // Wire animation clip switching for battle events
        _battleTurnManager.OnAllyAttack = () => _allyModel?.PlayIndex(1);
        _battleTurnManager.OnFoeAttack = () => _foeModel?.PlayIndex(1);
        _battleTurnManager.OnAllyFaint = () => _allyModel?.PlayIndex(2);
        _battleTurnManager.OnFoeFaint = () => _foeModel?.PlayIndex(2);
        _battleTurnManager.OnReturnToIdle = () =>
        {
            _allyModel?.PlayIndex(0);
            _foeModel?.PlayIndex(0);
        };
    }

    private void SetupInitialState()
    {
        _inFightMenu = false;
        _fightGridCol = 0;
        _fightGridRow = 0;
        _battleIntroComplete = false;
        _allySentOut = false;

        ResetMainMenuItems();
        _activeBattleMenu = _battleMainMenu;
        _activeBattleMenu.IsActive = false;
        _activeBattleMenu.SelectedIndex = 0;

        // Intro: "Wild X appeared!" → camera zoom-out → "Go! Y!" → show menu
        _battleMessageBox.Clear();
        _battleMessageBox.Show($"Wild {_foePokemon!.Nickname.ToUpper()} appeared!");
        _battleMessageBox.OnFinished = () =>
        {
            // Message dismissed → start camera zoom-out
            _battleZoomStarted = true;
            _battleCamFrom = _battleCamPos;
            _battleCamTo = BattleCamDefault;
            _battleCamLerp = 0f;
        };
    }

    private void ResetMainMenuItems()
    {
        _battleMainMenu.SetItems(
            new MenuItem("Fight", OpenFightMenu),
            new MenuItem("Bag", () => _onBagSelected?.Invoke()),
            new MenuItem("Pokemon", () => _onPokemonSelected?.Invoke()),
            new MenuItem("Run", TryRun));
        _battleMainMenu.Columns = 2;
    }

    /// <summary>End the current battle and return to overworld.</summary>
    public void ExitBattle()
    {
        InBattle = false;
        _allyPokemon = null;
        _foePokemon = null;
        _battleTurnManager = null;
        _allyModel = null;
        _foeModel = null;
        _activeBattleMenu.IsActive = false;
        _battleMessageBox.Clear();
        _battleIntroComplete = false;
        _allySentOut = false;
    }

    /// <summary>Re-activate menu after an overlay (Bag/Pokemon) closes.</summary>
    public void ReactivateMenu()
    {
        _activeBattleMenu.IsActive = true;
        _battleMessageBox.Show("What will you do?");
    }

    // ── Update ──────────────────────────────────────────────────────

    /// <summary>Process battle input and state each frame.</summary>
    public void Update(float dt, InputState uiInput)
    {
        // Animate camera zoom
        if (_battleCamLerp < 1f)
        {
            _battleCamLerp += dt / BattleCamSpeed;
            if (_battleCamLerp >= 1f)
            {
                _battleCamLerp = 1f;
                _battleCamPos = _battleCamTo;
                // Zoom-out finished — show "Go! <ally>!" before revealing the menu
                if (_battleZoomStarted)
                {
                    _battleZoomStarted = false;
                    _battleIntroComplete = true;
                    _battleMessageBox.Show($"Go! {_allyPokemon!.Nickname.ToUpper()}!");
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

        // Animate HP bar drain
        _allyPokemon?.UpdateDisplayHP(dt);
        _foePokemon?.UpdateDisplayHP(dt);

        // Animate Pokemon models
        double totalSec = dt; // simple delta for model animation
        _allyModel?.Update(totalSec);
        _foeModel?.Update(totalSec);

        // Input handling
        bool confirm = uiInput.Confirm;

        if (_activeBattleMenu.IsActive)
        {
            _battleMessageBox.Update(dt, false);

            if (_inFightMenu)
            {
                // Unified grid: cols 0-1 = move menu, cols 2-3 = action panel
                int newCol = _fightGridCol;
                int newRow = _fightGridRow;
                if (uiInput.Right) newCol++;
                if (uiInput.Left) newCol--;
                if (uiInput.Down) newRow++;
                if (uiInput.Up) newRow--;
                newCol = Math.Clamp(newCol, 0, 3);
                newRow = Math.Clamp(newRow, 0, 1);

                // Only move there if the cell has an item
                bool valid;
                if (newCol < 2)
                {
                    int idx = newRow * 2 + newCol;
                    valid = idx < _battleMoveMenu.Items.Count;
                }
                else
                {
                    int idx = newRow * 2 + (newCol - 2);
                    valid = idx < _battleMainMenu.Items.Count;
                }
                if (valid)
                {
                    _fightGridCol = newCol;
                    _fightGridRow = newRow;
                }

                // Map grid position to the right menu
                if (_fightGridCol < 2)
                {
                    _activeBattleMenu = _battleMoveMenu;
                    _battleMoveMenu.SelectedIndex = _fightGridRow * 2 + _fightGridCol;
                    _battleMainMenu.SelectedIndex = -1;
                }
                else
                {
                    _activeBattleMenu = _battleMainMenu;
                    _battleMainMenu.SelectedIndex = _fightGridRow * 2 + (_fightGridCol - 2);
                    _battleMoveMenu.SelectedIndex = -1;
                }

                // Confirm/cancel only — grid handles navigation, no mouse
                _activeBattleMenu.Update(
                    left: false, right: false, up: false, down: false,
                    confirm: uiInput.Confirm, cancel: uiInput.Cancel,
                    mousePosition: Point.Zero,
                    mouseClicked: false);
            }
            else
            {
                _activeBattleMenu.Update(
                    left: uiInput.Left, right: uiInput.Right,
                    up: uiInput.Up, down: uiInput.Down,
                    confirm: confirm,
                    cancel: uiInput.Cancel,
                    mousePosition: Point.Zero,
                    mouseClicked: false);
            }
        }
        else if (_battleMessageBox.IsActive)
        {
            _battleMessageBox.Update(dt, confirm);
        }
    }

    // ── Draw ────────────────────────────────────────────────────────

    /// <summary>
    /// Draw the 3D battle scene (backgrounds, platforms, Pokemon models).
    /// Call BEFORE starting the 2D SpriteBatch pass.
    /// </summary>
    public void Draw3DScene()
    {
        if (_battleEffect != null && _activeBattleBG != null)
        {
            DrawBattle3D();
        }
        else
        {
            // Fallback: dark background if models didn't load
            _spriteBatch.Begin();
            _spriteBatch.Draw(_pixel,
                new Rectangle(0, 0, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height),
                new Color(24, 24, 40));
            _spriteBatch.End();
        }
    }

    /// <summary>
    /// Draw the 2D battle UI overlay in virtual coordinate space.
    /// Call inside an active SpriteBatch with transform matrix set.
    /// </summary>
    public void DrawUI(int fontScale = 3, int virtualW = 800, int virtualH = 600)
    {
        int w = virtualW;
        int h = virtualH;

        // Scale layout proportionally to virtual resolution
        int margin = w / 40;         // 20 at 800, 32 at 1280
        int panelH = h / 5;          // 120 at 600, 192 at 960
        int panelY = h - panelH - margin;
        int infoBarW = w * 2 / 5;    // 320 at 800, 512 at 1280
        int infoBarH = h / 8;        // 75 at 600, 120 at 960
        int allyInfoH = infoBarH + h / 20;
        int menuW = w * 7 / 20;      // 280 at 800, 448 at 1280
        int gap = margin;

        // Info bars — foe appears after zoom-out, ally appears after "Go! <name>!"
        if (_battleIntroComplete && _foePokemon != null)
            BattleInfoBar.DrawFoeBar(_spriteBatch, _pixel, _kermFontRenderer, _battleFont!,
                new Rectangle(margin, margin, infoBarW, infoBarH), _foePokemon, fontScale);

        if (_allySentOut && _allyPokemon != null)
            BattleInfoBar.DrawAllyBar(_spriteBatch, _pixel, _kermFontRenderer, _battleFont!,
                new Rectangle(w - infoBarW - margin, panelY - allyInfoH - margin / 2, infoBarW, allyInfoH),
                _allyPokemon, _allyPokemon.EXPPercent, fontScale);

        int menuX = w - menuW - margin;

        if (_activeBattleMenu.IsActive)
        {
            if (_inFightMenu)
            {
                // Fight mode: move grid left, action panel (Back/Mega/Power) right
                int moveGridW = w - menuW - margin * 3;

                if (_kermFontRenderer != null && _kermFont != null)
                {
                    _battleMoveMenu.Draw(_spriteBatch, _kermFontRenderer, _kermFont, _pixel,
                        new Rectangle(margin, panelY, moveGridW, panelH), fontScale);
                    _battleMainMenu.Draw(_spriteBatch, _kermFontRenderer, _kermFont, _pixel,
                        new Rectangle(menuX, panelY, menuW, panelH), fontScale);
                }
                else if (_battleFont != null)
                {
                    _battleMoveMenu.Draw(_spriteBatch, _battleFont, _pixel,
                        new Rectangle(margin, panelY, moveGridW, panelH));
                    _battleMainMenu.Draw(_spriteBatch, _battleFont, _pixel,
                        new Rectangle(menuX, panelY, menuW, panelH));
                }

                // Move detail panel (TYPE/PP) below the move grid
                int moveGridW2 = w - menuW - margin * 3;
                DrawMoveDetailPanel(margin, panelY + panelH + margin / 3, moveGridW2, panelH * 2 / 3, fontScale);
            }
            else
            {
                // Main menu (Fight/Bag/Pokemon/Run): message box left + menu right
                int textBoxW = w - menuW - margin * 3;

                if (_kermFontRenderer != null && _kermFont != null)
                {
                    _activeBattleMenu.Draw(_spriteBatch, _kermFontRenderer, _kermFont, _pixel,
                        new Rectangle(menuX, panelY, menuW, panelH), fontScale);
                    _battleMessageBox.Draw(_spriteBatch, _kermFontRenderer, _pixel,
                        new Rectangle(margin, panelY, textBoxW, panelH), fontScale);
                }
                else if (_battleFont != null)
                {
                    _activeBattleMenu.Draw(_spriteBatch, _battleFont, _pixel,
                        new Rectangle(menuX, panelY, menuW, panelH));
                    _battleMessageBox.Draw(_spriteBatch, _battleFont, _pixel,
                        new Rectangle(margin, panelY, textBoxW, panelH));
                }
            }
        }
        else if (_battleMessageBox.IsActive)
        {
            // Full-width message box (intro / battle messages)
            int boxW = w - margin * 2;
            if (_kermFontRenderer != null)
                _battleMessageBox.Draw(_spriteBatch, _kermFontRenderer, _pixel,
                    new Rectangle(margin, panelY, boxW, panelH), fontScale);
            else if (_battleFont != null)
                _battleMessageBox.Draw(_spriteBatch, _battleFont, _pixel,
                    new Rectangle(margin, panelY, boxW, panelH));
        }
    }

    // ── 3D Rendering (ported from 2D Game1.DrawBattle3D) ────────────

    private void DrawBattle3D()
    {
        var device = _graphicsDevice;
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
        _battleEffect.VertexColorEnabled = false;
        _battleEffect.Alpha = 1f;
        _battleEffect.DiffuseColor = Vector3.One;

        // Background at origin
        _battleEffect.World = Matrix.Identity;
        _activeBattleBG!.Draw(device, _battleEffect);

        // Foe platform at (0, -0.20, -15)
        if (_activePlatformFoe != null)
        {
            _battleEffect.World = Matrix.CreateTranslation(0f, -0.20f, -15f);
            _activePlatformFoe.Draw(device, _battleEffect);
        }

        // Ally platform at (0, -0.20, 3)
        if (_activePlatformAlly != null)
        {
            _battleEffect.World = Matrix.CreateTranslation(0f, -0.20f, 3f);
            _activePlatformAlly.Draw(device, _battleEffect);
        }

        // Foe Pokemon model — faces toward ally/camera (+Z direction, no rotation needed)
        if (_foeModel != null)
        {
            float scale = FitModelScale(_foeModel, 3.0f);
            _battleEffect.World = Matrix.CreateScale(scale) *
                Matrix.CreateTranslation(0f, -0.20f - _foeModel.BoundsMin.Y * scale, -15f);
            _foeModel.Draw(device, _battleEffect);
        }

        // Ally Pokemon model — faces away from camera toward foe (rotate 180°)
        if (_allyModel != null)
        {
            float scale = FitModelScale(_allyModel, 3.5f);
            _battleEffect.World = Matrix.CreateScale(scale) *
                Matrix.CreateRotationY(MathF.PI) *
                Matrix.CreateTranslation(0f, -0.20f - _allyModel.BoundsMin.Y * scale, 3f);
            _allyModel.Draw(device, _battleEffect);
        }

        // Reset state for 2D rendering
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.BlendState = BlendState.AlphaBlend;
    }

    // ── Move detail panel ───────────────────────────────────────────

    private void DrawMoveDetailPanel(int x, int y, int width, int height, int fontScale)
    {
        UIStyle.DrawBattlePanel(_spriteBatch, _pixel, new Rectangle(x, y, width, height));

        if (_allyPokemon == null || _battleMoveMenu.SelectedIndex < 0
            || _battleMoveMenu.SelectedIndex >= _allyPokemon.Moves.Length)
            return;

        var battleMove = _allyPokemon.Moves[_battleMoveMenu.SelectedIndex];
        var moveData = MoveRegistry.GetMove(battleMove.MoveId);

        int padding = 16;
        int textX = x + padding;

        string typeName = moveData?.Type.ToString().ToUpper() ?? "???";
        string typeLabel = $"TYPE/{typeName}";
        string ppLabel = $"PP  {battleMove.CurrentPP,2}/{battleMove.MaxPP,2}";

        if (_kermFontRenderer != null && _kermFont != null)
        {
            int fontH = _kermFont.FontHeight * fontScale;
            int lineSpacing = 8;
            int totalTextH = fontH * 2 + lineSpacing;
            int startY = y + (height - totalTextH) / 2;

            _kermFontRenderer.DrawString(_spriteBatch, typeLabel,
                new Vector2(textX, startY), fontScale, Color.White);
            _kermFontRenderer.DrawString(_spriteBatch, ppLabel,
                new Vector2(textX, startY + fontH + lineSpacing), fontScale, Color.White);
        }
        else if (_battleFont != null)
        {
            var typeSize = _battleFont.MeasureString(typeLabel);
            int lineSpacing = 6;
            int totalTextH = (int)(typeSize.Y * 2) + lineSpacing;
            int startY = y + (height - totalTextH) / 2;

            UIStyle.DrawShadowedText(_spriteBatch, _battleFont, typeLabel,
                new Vector2(textX, startY),
                UIStyle.TextNormal, UIStyle.TextNormalOutline);
            UIStyle.DrawShadowedText(_spriteBatch, _battleFont, ppLabel,
                new Vector2(textX, startY + (int)typeSize.Y + lineSpacing),
                UIStyle.TextNormal, UIStyle.TextNormalOutline);
        }
    }

    // ── Private helpers ─────────────────────────────────────────────

    private void OpenFightMenu()
    {
        BuildMoveMenu();

        // Swap the main menu to Back/Mega/Power (displayed alongside move grid)
        _battleMainMenu.SetItems(
            new MenuItem("Back", CloseFightMenu),
            new MenuItem("Mega", () => { /* toggle later */ }),
            new MenuItem("Power", () => { /* toggle later */ }));
        _battleMainMenu.Columns = 2;
        _battleMainMenu.SelectedIndex = -1;

        // Switch keyboard focus to move grid
        _activeBattleMenu = _battleMoveMenu;
        _activeBattleMenu.SelectedIndex = 0;
        _activeBattleMenu.IsActive = true;
        _battleMessageBox.Clear();
        _inFightMenu = true;
        _fightGridCol = 0;
        _fightGridRow = 0;
    }

    private void CloseFightMenu()
    {
        // Restore main menu items
        ResetMainMenuItems();
        _inFightMenu = false;
        _activeBattleMenu = _battleMainMenu;
        _activeBattleMenu.SelectedIndex = 0;
        _activeBattleMenu.IsActive = true;
        _battleMessageBox.Clear();
        _battleMessageBox.Show("What will you do?");
    }

    private void TryRun()
    {
        _activeBattleMenu.IsActive = false;
        _battleMessageBox.Clear();
        _battleMessageBox.Show("You got away safely!");
        _battleMessageBox.OnFinished = () =>
        {
            _allyModel = null;
            _foeModel = null;
            ExitBattle();
            _onExitBattle?.Invoke();
        };
    }

    private void BuildMoveMenu()
    {
        if (_allyPokemon == null) return;
        var moves = _allyPokemon.Moves;
        var items = new MenuItem[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            var bm = moves[i];
            var data = MoveRegistry.GetMove(bm.MoveId);
            string name = data?.Name ?? $"Move#{bm.MoveId}";
            bool enabled = bm.CurrentPP > 0;
            int moveIndex = i;
            items[i] = new MenuItem(name, () => SelectMove(moveIndex), enabled);
        }
        _battleMoveMenu.SetItems(items);
    }

    private void SelectMove(int moveIndex)
    {
        if (_allyPokemon == null || moveIndex >= _allyPokemon.Moves.Length) return;
        var bm = _allyPokemon.Moves[moveIndex];
        if (bm.CurrentPP <= 0)
        {
            _battleMessageBox.Clear();
            _battleMessageBox.Show("No PP left for this move!");
            return;
        }
        bm.CurrentPP--;
        _battleTurnManager?.StartTurn(moveIndex);
    }

    private SkeletalModelData? LoadPokemonModel(int speciesId)
    {
        var species = SpeciesRegistry.GetSpecies(speciesId);
        if (species?.ModelFolder == null) return null;
        var model = ModelLoader.Load(species.ModelFolder, _graphicsDevice);
        if (model != null)
        {
            var min = model.BoundsMin;
            var max = model.BoundsMax;
            System.Diagnostics.Debug.WriteLine(
                $"[Battle3D] Pokemon #{speciesId} ({species.Name}): {model.Meshes.Count} meshes, " +
                $"bounds: ({min.X:F1},{min.Y:F1},{min.Z:F1}) to ({max.X:F1},{max.Y:F1},{max.Z:F1})");
        }
        return model;
    }

    private static float FitModelScale(SkeletalModelData model, float targetHeight)
    {
        float modelHeight = model.BoundsMax.Y - model.BoundsMin.Y;
        if (modelHeight <= 0.001f) return 1f;
        return targetHeight / modelHeight;
    }
}
