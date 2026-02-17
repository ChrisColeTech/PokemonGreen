#nullable enable
using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokemonGreen.Core.Battle;
using PokemonGreen.Core.Items;
using PokemonGreen.Core.Maps;
using PokemonGreen.Core.Pokemon;
using PokemonGreen.Core.Rendering;
using PokemonGreen.Core.Save;
using PokemonGreen.Core.Rendering.Skeletal;
using PokemonGreen.Core.Systems;
using PokemonGreen.Core.UI;
using PokemonGreen.Core.UI.Fonts;
using PokemonGreen.Core.UI.Screens;

namespace PokemonGreen._3D;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private BasicEffect _effect;
    private BasicEffect _gridEffect;
    private SkinnedDaeModel _model;
    private AnimationController? _animController;
    private bool _isJumping;
    private VertexPositionColor[] _gridVertices;

    // Tile map
    private TileMapMesh3D? _tileMapMesh;

    // UI overlay system
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private KermFont? _kermFont;
    private KermFontRenderer? _kermFontRenderer;
    private CharacterSelectScreen? _overlay;
    private KeyboardState _prevKeyboard;

    // Message box (reused from 2D game)
    private readonly Core.UI.MessageBox _messageBox = new();

    // Pause menu (same MenuBox pattern as 2D game)
    private readonly MenuBox _pauseMenuBox = new() { Columns = 1, UseStandardStyle = true };
    private bool _isPaused;

    // Subsystems
    private BattleScreen3D _battleScreen = null!;
    private CubeCollectibleSystem _cubeSystem = null!;
    private PersistenceManager3D _persistence = null!;

    // Party and inventory for battle overlays
    private Party _party = null!;
    private PlayerInventory _inventory = null!;

    // Encounter system
    private readonly Random _encounterRng = new();
    private float _encounterStepTimer;
    private const float EncounterStepInterval = 0.4f; // check every 0.4s of walking
    private const float EncounterChance = 0.15f; // 15% per check

    // Battle transition (white flash → fade to black → battle → fade from black)
    private enum TransitionPhase
    {
        None,
        FlashWhite,
        FadeToBattle,
        FadeFromBattle,
        FadeToBlackFromBattle,
        FadeFromBlackToOverworld
    }
    private TransitionPhase _transition;
    private float _transitionTimer;
    private float _transitionAlpha;
    private BattleBackground _pendingBattleBG;
    private const float FlashDuration = 0.15f;
    private const float FadeDuration = 0.3f;

    // Character data
    private string _assetsRoot;
    private string _currentCharacterFolder = "tr0001_00";
    private (string folder, string name)[] _characters = Array.Empty<(string, string)>();

    private readonly Camera3D _camera = new(nearPlane: 0.1f, farPlane: 1000f);
    private readonly ThirdPersonInputMapper _input = new();
    private Vector3 _playerPosition = Vector3.Zero;
    private float _playerYaw;
    private float _playerTargetYaw;
    private float _cameraYaw = MathF.PI;
    private bool _wasTurning;
    private float _cameraFollowDelayTimer;
    private float _verticalVelocity;

    // Set to true to launch directly into the battle screen for debugging.
    private const bool DebugStartInBattle = true;

    // Virtual resolution for 2D UI — higher than the 2D game (800x600) so the
    // transform matrix scales DOWN at 1080p instead of up, keeping text crisp.
    private const int VirtualWidth = 1280;
    private const int VirtualHeight = 960;
    private const int UIFontScale = 5;

    private const float PlayerTargetHeight = 2f;
    private const float PlayerModelHeightOffset = 0.0f;
    private const float PlayerModelScale = 0.015f;
    private const int GridHalfSize = 30;
    private const float GridY = 0f;
    private const float PlayerTurnSpeed = 8f;
    private const float CameraYawFollowSpeed = 3.5f;
    private const float CameraFollowDelay = 0.12f;
    private const float JumpVelocity = 13f;
    private const float Gravity = -35f;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        // Initialize registries (needed for Pokemon model loading in battles)
        PokemonGreen.Core.Pokemon.SpeciesRegistry.Initialize();

        _effect = new BasicEffect(GraphicsDevice);
        _effect.LightingEnabled = true;
        _effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-1, -2, -1));
        _effect.DirectionalLight0.DiffuseColor = new Vector3(0.5f);
        _effect.AmbientLightColor = new Vector3(0.6f);

        _gridEffect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            LightingEnabled = false
        };

        _gridVertices = CreateGridVertices();

        // Load all maps in the world and build 3D mesh
        const string worldId = "small_world";
        const float tileSize = 2f;
        _tileMapMesh = new TileMapMesh3D
        {
            TileWorldSize = tileSize,
            BlockHeight = 1.2f,
            GroundY = GridY,
        };
        _tileMapMesh.BuildWorld(GraphicsDevice, worldId);

        // Spawn player at world center
        _playerPosition = TileMapMesh3D.GetWorldCenter(worldId, tileSize, GridY);

        _camera.Pitch = -0.3f;
        _camera.Distance = 8f;
        _camera.MinDistance = 4f;
        _camera.MaxDistance = 20f;

        // Load persisted state
        _persistence = new PersistenceManager3D();
        _persistence.Load();

        if (!string.IsNullOrEmpty(_persistence.RestoredCharacterFolder))
        {
            _currentCharacterFolder = _persistence.RestoredCharacterFolder;
            Console.WriteLine($"[Save] Restored character: {_currentCharacterFolder}");
        }

        // Pause menu items
        _pauseMenuBox.SetItems(
            new MenuItem("Resume", ClosePauseMenu),
            new MenuItem("Reset Cubes", ResetCubes),
            new MenuItem("Close", ClosePauseMenu));
        _pauseMenuBox.OnCancel = ClosePauseMenu;

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Resolve assets root
        string assemblyDir = Path.GetDirectoryName(typeof(Game1).Assembly.Location) ?? "";
        _assetsRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", "PokemonGreen.Assets", "Pokemon3D"));

        // Auto-scan for character folders with manifest.json
        string overworldDir = Path.Combine(_assetsRoot, "characters", "overworld");
        if (Directory.Exists(overworldDir))
        {
            _characters = Directory.GetDirectories(overworldDir)
                .Where(d => File.Exists(Path.Combine(d, "manifest.json")))
                .Select(d => Path.GetFileName(d))
                .OrderBy(f => f)
                .Select(f => (folder: f, name: f))
                .ToArray();
        }

        // Load KermFont for UI overlays
        string kermFontPath = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", "PokemonGreen.Assets", "Content", "Fonts", "Kerm", "Battle.kermfont"));
        if (File.Exists(kermFontPath))
        {
            var palette = new[]
            {
                Color.Transparent,
                new Color(245, 245, 245, 255),  // main text — bright white
                new Color(40, 40, 50, 50),       // shadow — subtle, low alpha
                new Color(20, 20, 30, 30),       // outer — barely visible
            };
            _kermFont = new KermFont(GraphicsDevice, kermFontPath, palette: palette);
            _kermFontRenderer = new KermFontRenderer(_kermFont);
        }

        // Initialize subsystems that need GPU resources
        _battleScreen = new BattleScreen3D();
        _battleScreen.Initialize(_spriteBatch, _pixel, _kermFontRenderer, _kermFont);

        // Test party and inventory for battle overlays
        _party = Party.CreateTestParty();
        _inventory = PlayerInventory.CreateTestInventory();
        _battleScreen.SetPartyAndInventory(_party, _inventory);

        // Load 3D battle scene models
        string battleBGPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BattleBG");
        if (!Directory.Exists(battleBGPath))
        {
            // Dev path fallback: Assets project
            battleBGPath = Path.GetFullPath(Path.Combine(_assetsRoot, "..", "BattleBG"));
        }
        if (Directory.Exists(battleBGPath))
            _battleScreen.LoadBattleModels(GraphicsDevice, battleBGPath);

        _cubeSystem = new CubeCollectibleSystem(GraphicsDevice, _gridEffect, _spriteBatch,
            _pixel, _kermFontRenderer, _kermFont);
        _cubeSystem.LoadFromFlags(_persistence.StoryFlags);

        Console.WriteLine($"[Save] Loaded {_cubeSystem.CubeCount} collected cubes");

        if (DebugStartInBattle)
        {
            _battleScreen.EnterBattle();
            _battleScreen.OnBattleExit = () =>
            {
                _transition = TransitionPhase.FadeToBlackFromBattle;
                _transitionTimer = 0f;
                _transitionAlpha = 0f;
            };
        }

        // Load default character
        LoadCharacterModel(_currentCharacterFolder);
    }

    private void LoadCharacterModel(string folderName)
    {
        string dir = Path.Combine(_assetsRoot, "characters", "overworld", folderName);
        if (!Directory.Exists(dir)) return;

        var animSet = SplitModelAnimationSetLoader.Load(dir);
        _animController = new AnimationController(animSet);

        _model = new SkinnedDaeModel();
        _model.Load(GraphicsDevice, animSet.ModelPath, animSet.Skeleton);

        // Start with idle animation
        _animController.Play("Idle", loop: true, resetTime: true);
        _model.UpdatePose(GraphicsDevice, _animController.SkinPose);

        _currentCharacterFolder = folderName;
    }

    protected override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var keyboard = Keyboard.GetState();
        bool confirmPressed = keyboard.GetPressedKeyCount() > 0 && _prevKeyboard.GetPressedKeyCount() == 0;

        // Animate cubes regardless of state
        _cubeSystem.UpdateAnimation(dt);

        // Update battle transition (flash/fade)
        UpdateTransition(dt);

        // During transition, block all input
        if (_transition != TransitionPhase.None
            && _transition != TransitionPhase.FadeFromBattle
            && _transition != TransitionPhase.FadeFromBlackToOverworld)
        {
            _prevKeyboard = keyboard;
            base.Update(gameTime);
            return;
        }

        // Handle battle state (blocks all overworld input)
        if (_battleScreen.InBattle)
        {
            var uiInput = BuildInputState(keyboard);
            _battleScreen.Update(dt, uiInput, gameTime.TotalGameTime.TotalSeconds);
            _prevKeyboard = keyboard;
            base.Update(gameTime);
            return;
        }

        // Handle message box (blocks all other input) — any key dismisses
        if (_messageBox.IsActive)
        {
            bool anyKey = keyboard.GetPressedKeyCount() > 0 && _prevKeyboard.GetPressedKeyCount() == 0;
            _messageBox.Update(dt, anyKey);
            _prevKeyboard = keyboard;
            base.Update(gameTime);
            return;
        }

        // Handle pause menu (blocks all other input)
        if (_isPaused)
        {
            var uiInput = BuildInputState(keyboard);
            _pauseMenuBox.Update(
                left: false, right: false,
                up: uiInput.Up, down: uiInput.Down,
                confirm: uiInput.Confirm,
                cancel: uiInput.Cancel,
                mousePosition: Point.Zero,
                mouseClicked: false);

            _prevKeyboard = keyboard;
            base.Update(gameTime);
            return;
        }

        // Handle overlay
        if (_overlay != null)
        {
            var uiInput = BuildInputState(keyboard);
            _overlay.Update(dt, uiInput);

            if (_overlay.IsFinished)
            {
                if (_overlay.SelectedFolder != null)
                {
                    LoadCharacterModel(_overlay.SelectedFolder);
                    _persistence.Save(_playerPosition, _currentCharacterFolder, _cubeSystem.CubeCount);
                }
                _overlay = null;
            }

            _prevKeyboard = keyboard;
            base.Update(gameTime);
            return;
        }

        // Enter opens pause menu
        if (keyboard.IsKeyDown(Keys.Enter) && !_prevKeyboard.IsKeyDown(Keys.Enter))
        {
            OpenPauseMenu();
            _prevKeyboard = keyboard;
            base.Update(gameTime);
            return;
        }

        // Tab or Escape opens character select
        if ((keyboard.IsKeyDown(Keys.Tab) && !_prevKeyboard.IsKeyDown(Keys.Tab))
            || (keyboard.IsKeyDown(Keys.Escape) && !_prevKeyboard.IsKeyDown(Keys.Escape)))
        {
            _overlay = new CharacterSelectScreen(
                _characters.Select(c => c.folder).ToArray(),
                _characters.Select(c => c.name).ToArray());
            _prevKeyboard = keyboard;
            base.Update(gameTime);
            return;
        }

        _input.Update();
        var input = _input.State;

        _playerTargetYaw += input.Turn * 2f * dt;
        _camera.Rotate(0f, input.Pitch * dt);
        _camera.Zoom(-input.Zoom * 5f * dt);

        var cameraForward = new Vector3(-MathF.Sin(_cameraYaw), 0, -MathF.Cos(_cameraYaw));
        var cameraRight = Vector3.Normalize(Vector3.Cross(cameraForward, Vector3.Up));
        var speed = (input.IsRunning ? 8f : 4f) * dt;

        var moveInputX = input.MoveX;
        var moveInputZ = input.MoveZ;

        var move = cameraForward * moveInputZ + cameraRight * moveInputX;
        bool isMoving = move.LengthSquared() > 0.0001f;

        if (isMoving)
        {
            var moveDir = Vector3.Normalize(move);
            var desiredPos = _playerPosition + moveDir * speed;

            // Collision check with wall sliding
            if (_tileMapMesh != null)
            {
                bool fullOk = _tileMapMesh.CanOccupy(desiredPos.X, desiredPos.Z, _playerPosition.Y);
                if (fullOk)
                {
                    _playerPosition = desiredPos;
                }
                else
                {
                    // Try sliding along X axis only
                    var slideX = new Vector3(desiredPos.X, _playerPosition.Y, _playerPosition.Z);
                    if (_tileMapMesh.CanOccupy(slideX.X, slideX.Z, _playerPosition.Y))
                        _playerPosition = slideX;

                    // Try sliding along Z axis only
                    var slideZ = new Vector3(_playerPosition.X, _playerPosition.Y, desiredPos.Z);
                    if (_tileMapMesh.CanOccupy(slideZ.X, slideZ.Z, _playerPosition.Y))
                        _playerPosition = slideZ;
                }
            }
            else
            {
                _playerPosition = desiredPos;
            }

            _playerTargetYaw = MathF.Atan2(moveDir.X, moveDir.Z);
        }

        float supportHeight = _tileMapMesh?.GetSupportHeight(_playerPosition.X, _playerPosition.Z) ?? 0f;
        var isGrounded = _playerPosition.Y <= supportHeight + 0.001f;
        var jumpPressed = input.JumpPressed;
        if (jumpPressed && isGrounded)
        {
            _verticalVelocity = JumpVelocity;
            _isJumping = true;
            isGrounded = false;
        }

        if (!isGrounded)
            _verticalVelocity += Gravity * dt;

        _playerPosition.Y += _verticalVelocity * dt;
        supportHeight = _tileMapMesh?.GetSupportHeight(_playerPosition.X, _playerPosition.Z) ?? 0f;
        if (_playerPosition.Y < supportHeight)
        {
            _playerPosition.Y = supportHeight;
            _verticalVelocity = 0f;
            _isJumping = false;
            isGrounded = true;
        }

        // Check cube collection
        if (_cubeSystem.CheckCollection(_playerPosition, _persistence.StoryFlags))
        {
            _messageBox.Show("You found another cube!");
            _messageBox.OnFinished = null; // just dismiss
            _persistence.Save(_playerPosition, _currentCharacterFolder, _cubeSystem.CubeCount);
        }

        // Check encounter tiles (only while moving on the ground)
        if (isMoving && isGrounded)
            CheckEncounterTile();

        if (_animController is not null)
        {
            bool hasJumpClip = _isJumping && _animController.HasClip("Jump");
            string tag = hasJumpClip ? "Jump"
                : isMoving ? (input.IsRunning ? "Run" : "Walk")
                : "Idle";

            // Only reset time when switching to a new animation, not every frame
            bool isNewTag = !string.Equals(_animController.ActiveTag, tag, StringComparison.OrdinalIgnoreCase);
            _animController.Play(tag, loop: !hasJumpClip, resetTime: isNewTag);
            _animController.Update(dt);
            _model?.UpdatePose(GraphicsDevice, _animController.SkinPose);
        }

        _playerYaw = MoveTowardsAngle(_playerYaw, _playerTargetYaw, PlayerTurnSpeed * dt);

        var desiredCameraYaw = _playerYaw + MathF.PI;
        var isTurning = MathF.Abs(MathHelper.WrapAngle(_playerTargetYaw - _playerYaw)) > 0.02f;
        if (isTurning && !_wasTurning)
            _cameraFollowDelayTimer = CameraFollowDelay;

        _wasTurning = isTurning;

        if (_cameraFollowDelayTimer > 0f)
        {
            _cameraFollowDelayTimer -= dt;
        }
        else
        {
            _cameraYaw = MoveTowardsAngle(_cameraYaw, desiredCameraYaw, CameraYawFollowSpeed * dt);
        }

        _camera.Yaw = _cameraYaw;
        _camera.Follow(_playerPosition, PlayerTargetHeight);
        _camera.Update(GraphicsDevice.Viewport.AspectRatio);

        _effect.View = _camera.ViewMatrix;
        _effect.Projection = _camera.ProjectionMatrix;

        _prevKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_battleScreen.InBattle)
        {
            // 3D battle scene (backgrounds, platforms, Pokemon models)
            _battleScreen.Draw3DScene();

            // 2D UI overlay — fills full window width
            var battleTransform = GetBattleUITransform(out int battleW, out int battleH);
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied,
                SamplerState.PointClamp, transformMatrix: battleTransform);
            _battleScreen.DrawUI(UIFontScale, battleW, battleH);
            _spriteBatch.End();

            // Draw transition overlay on top of battle
            if (_transition != TransitionPhase.None)
            {
                int sw = GraphicsDevice.Viewport.Width;
                int sh = GraphicsDevice.Viewport.Height;
                _spriteBatch.Begin();
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, sw, sh),
                    Color.Black * _transitionAlpha);
                _spriteBatch.End();
            }

            base.Draw(gameTime);
            return;
        }

        GraphicsDevice.Clear(_transition == TransitionPhase.FadeToBlackFromBattle
            ? Color.Black
            : Color.CornflowerBlue);

        if (_gridVertices != null && _gridVertices.Length > 0)
        {
            _gridEffect.World = Matrix.Identity;
            _gridEffect.View = _effect.View;
            _gridEffect.Projection = _effect.Projection;

            foreach (var pass in _gridEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(
                    PrimitiveType.LineList,
                    _gridVertices,
                    0,
                    _gridVertices.Length / 2);
            }
        }

        // Draw tile map
        if (_tileMapMesh != null)
        {
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            _gridEffect.View = _effect.View;
            _gridEffect.Projection = _effect.Projection;
            _tileMapMesh.Draw(GraphicsDevice, _gridEffect);
        }

        // Draw collectible cubes
        _cubeSystem.DrawCubes(_effect.View, _effect.Projection);

        if (_model?.VertexBuffer != null)
        {
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            _effect.World =
                Matrix.CreateScale(PlayerModelScale)
                * Matrix.CreateRotationY(_playerYaw)
                * Matrix.CreateTranslation(_playerPosition + Vector3.Up * PlayerModelHeightOffset);

            _model.Draw(GraphicsDevice, _effect);
        }

        // 2D UI overlay pass — virtual 800x600, scaled to fill viewport (matches 2D game)
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied,
            SamplerState.PointClamp, transformMatrix: GetUITransform());

        // Cube counter (upper-left)
        _cubeSystem.DrawCounter(UIFontScale);

        // Character select overlay
        if (_overlay != null)
        {
            _overlay.Draw(_spriteBatch, _pixel, _kermFontRenderer, _kermFont,
                null!, VirtualWidth, VirtualHeight, UIFontScale);
        }

        // Pause menu (top-right)
        if (_isPaused)
        {
            int menuW = 256;
            int menuH = 224;
            int menuX = VirtualWidth - menuW - 24;
            int menuY = 24;

            if (_kermFontRenderer != null && _kermFont != null)
                _pauseMenuBox.Draw(_spriteBatch, _kermFontRenderer, _kermFont, _pixel,
                    new Rectangle(menuX, menuY, menuW, menuH), UIFontScale);
        }

        // Message box (bottom of screen)
        if (_messageBox.IsActive)
        {
            int boxH = 128;
            int margin = 32;
            var bounds = new Rectangle(margin, VirtualHeight - boxH - margin, VirtualWidth - margin * 2, boxH);

            if (_kermFontRenderer != null)
                _messageBox.Draw(_spriteBatch, _kermFontRenderer, _pixel, bounds, fontScale: UIFontScale);
        }

        _spriteBatch.End();

        // ── Transition overlays ──
        if (_transition != TransitionPhase.None)
        {
            int sw = GraphicsDevice.Viewport.Width;
            int sh = GraphicsDevice.Viewport.Height;
            _spriteBatch.Begin();

            if (_transition == TransitionPhase.FlashWhite)
            {
                // White flash overlay
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, sw, sh),
                    Color.White * _transitionAlpha);
            }
            else
            {
                // Black fade overlay
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, sw, sh),
                    Color.Black * _transitionAlpha);
            }

            _spriteBatch.End();
        }

        base.Draw(gameTime);
    }

    // ── Input ─────────────────────────────────────────────────────────

    private InputState BuildInputState(KeyboardState keyboard)
    {
        var mouse = Mouse.GetState();
        // Any key freshly pressed this frame (for message dismissal)
        bool anyKey = keyboard.GetPressedKeyCount() > 0 && _prevKeyboard.GetPressedKeyCount() == 0;
        return new InputState
        {
            Left = keyboard.IsKeyDown(Keys.Left) && !_prevKeyboard.IsKeyDown(Keys.Left),
            Right = keyboard.IsKeyDown(Keys.Right) && !_prevKeyboard.IsKeyDown(Keys.Right),
            Up = keyboard.IsKeyDown(Keys.Up) && !_prevKeyboard.IsKeyDown(Keys.Up),
            Down = keyboard.IsKeyDown(Keys.Down) && !_prevKeyboard.IsKeyDown(Keys.Down),
            Confirm = (keyboard.IsKeyDown(Keys.Enter) && !_prevKeyboard.IsKeyDown(Keys.Enter))
                   || (keyboard.IsKeyDown(Keys.Z) && !_prevKeyboard.IsKeyDown(Keys.Z)),
            Cancel = (keyboard.IsKeyDown(Keys.Escape) && !_prevKeyboard.IsKeyDown(Keys.Escape))
                  || (keyboard.IsKeyDown(Keys.X) && !_prevKeyboard.IsKeyDown(Keys.X)),
            AnyKey = anyKey,
            PageLeft = (keyboard.IsKeyDown(Keys.Q) && !_prevKeyboard.IsKeyDown(Keys.Q))
                    || (keyboard.IsKeyDown(Keys.PageUp) && !_prevKeyboard.IsKeyDown(Keys.PageUp)),
            PageRight = (keyboard.IsKeyDown(Keys.E) && !_prevKeyboard.IsKeyDown(Keys.E))
                     || (keyboard.IsKeyDown(Keys.PageDown) && !_prevKeyboard.IsKeyDown(Keys.PageDown)),
            MousePosition = mouse.Position,
            MouseClicked = mouse.LeftButton == ButtonState.Pressed,
        };
    }

    // ── Grid ──────────────────────────────────────────────────────────

    private static VertexPositionColor[] CreateGridVertices()
    {
        var lineCount = (GridHalfSize * 2 + 1) * 2;
        var vertices = new VertexPositionColor[lineCount * 2];
        var index = 0;

        for (var i = -GridHalfSize; i <= GridHalfSize; i++)
        {
            var isMajorLine = i % 5 == 0;
            var xLineColor = i == 0
                ? new Color(220, 80, 80)
                : (isMajorLine ? new Color(95, 95, 95) : new Color(55, 55, 55));
            var zLineColor = i == 0
                ? new Color(80, 160, 220)
                : (isMajorLine ? new Color(95, 95, 95) : new Color(55, 55, 55));

            vertices[index++] = new VertexPositionColor(new Vector3(i, GridY, -GridHalfSize), xLineColor);
            vertices[index++] = new VertexPositionColor(new Vector3(i, GridY, GridHalfSize), xLineColor);

            vertices[index++] = new VertexPositionColor(new Vector3(-GridHalfSize, GridY, i), zLineColor);
            vertices[index++] = new VertexPositionColor(new Vector3(GridHalfSize, GridY, i), zLineColor);
        }

        return vertices;
    }

    // ── Pause Menu ───────────────────────────────────────────────────

    private void OpenPauseMenu()
    {
        _isPaused = true;
        _pauseMenuBox.IsActive = true;
        _pauseMenuBox.SelectedIndex = 0;
    }

    private void ClosePauseMenu()
    {
        _isPaused = false;
        _pauseMenuBox.IsActive = false;
    }

    private void ResetCubes()
    {
        _cubeSystem.ResetAll(_persistence.StoryFlags);
        _persistence.Save(_playerPosition, _currentCharacterFolder, _cubeSystem.CubeCount);
        ClosePauseMenu();
        _messageBox.Show("All cubes have been reset!");
    }

    // ── Encounter ──────────────────────────────────────────────────────

    private void CheckEncounterTile()
    {
        if (_tileMapMesh == null || _messageBox.IsActive || _transition != TransitionPhase.None) return;
        if (_battleScreen.InBattle) return;

        string? behavior = _tileMapMesh.GetOverlayBehavior(_playerPosition.X, _playerPosition.Z);
        if (behavior == null || !behavior.Contains("encounter")) return;

        _encounterStepTimer += (float)TargetElapsedTime.TotalSeconds;
        if (_encounterStepTimer < EncounterStepInterval) return;
        _encounterStepTimer = 0f;

        if (_encounterRng.NextDouble() < EncounterChance)
        {
            _pendingBattleBG = BattleBackgroundResolver.FromOverlayBehavior(behavior);
            BeginBattleTransition();
        }
    }

    private void BeginBattleTransition()
    {
        _transition = TransitionPhase.FlashWhite;
        _transitionTimer = 0f;
        _transitionAlpha = 0f;
    }

    private void UpdateTransition(float dt)
    {
        if (_transition == TransitionPhase.None) return;

        _transitionTimer += dt;

        switch (_transition)
        {
            case TransitionPhase.FlashWhite:
                // Quick white flash
                _transitionAlpha = 1f - (_transitionTimer / FlashDuration);
                if (_transitionTimer >= FlashDuration)
                {
                    _transition = TransitionPhase.FadeToBattle;
                    _transitionTimer = 0f;
                    _transitionAlpha = 0f;
                }
                break;

            case TransitionPhase.FadeToBattle:
                // Fade to black
                _transitionAlpha = _transitionTimer / FadeDuration;
                if (_transitionTimer >= FadeDuration)
                {
                    _transitionAlpha = 1f;
                    // Enter battle at peak black
                    _battleScreen.EnterBattle(_pendingBattleBG);
                    _battleScreen.OnBattleExit = () =>
                    {
                        // When battle ends, fade out
                        _transition = TransitionPhase.FadeToBlackFromBattle;
                        _transitionTimer = 0f;
                        _transitionAlpha = 0f;
                    };
                    _transition = TransitionPhase.FadeFromBattle;
                    _transitionTimer = 0f;
                }
                break;

            case TransitionPhase.FadeFromBattle:
                // Fade from black (revealing battle screen)
                _transitionAlpha = 1f - (_transitionTimer / FadeDuration);
                if (_transitionTimer >= FadeDuration)
                {
                    _transitionAlpha = 0f;
                    _transition = TransitionPhase.None;
                }
                break;

            case TransitionPhase.FadeToBlackFromBattle:
                _transitionAlpha = MathF.Min(1f, _transitionTimer / FadeDuration);
                if (_transitionTimer >= FadeDuration)
                {
                    _transitionAlpha = 1f;
                    if (_battleScreen.InBattle)
                        _battleScreen.CleanupBattle();

                    _transition = TransitionPhase.FadeFromBlackToOverworld;
                    _transitionTimer = 0f;
                }
                break;

            case TransitionPhase.FadeFromBlackToOverworld:
                _transitionAlpha = 1f - MathF.Min(1f, _transitionTimer / FadeDuration);
                if (_transitionTimer >= FadeDuration)
                {
                    _transitionAlpha = 0f;
                    _transition = TransitionPhase.None;
                }
                break;
        }
    }

    // ── UI Scaling ─────────────────────────────────────────────────────

    /// <summary>
    /// Maps virtual 800x600 UI coordinates to the actual viewport,
    /// maintaining aspect ratio (letterboxed). Matches the 2D game exactly.
    /// </summary>
    private Matrix GetUITransform()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        float sx = (float)w / VirtualWidth;
        float sy = (float)h / VirtualHeight;
        float scale = Math.Min(sx, sy);
        float ox = (w - VirtualWidth * scale) / 2f;
        float oy = (h - VirtualHeight * scale) / 2f;
        return Matrix.CreateScale(scale, scale, 1f) * Matrix.CreateTranslation(ox, oy, 0f);
    }

    /// <summary>
    /// Maps battle UI coordinates to fill the full window width.
    /// Scales uniformly by height so text stays the right size,
    /// but the virtual width expands to fill the actual aspect ratio.
    /// </summary>
    private Matrix GetBattleUITransform(out int virtualW, out int virtualH)
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        float scale = (float)h / VirtualHeight;
        virtualW = (int)(w / scale);
        virtualH = VirtualHeight;
        return Matrix.CreateScale(scale, scale, 1f);
    }

    // ── Utility ───────────────────────────────────────────────────────

    private static float MoveTowardsAngle(float current, float target, float maxDelta)
    {
        var delta = MathHelper.WrapAngle(target - current);
        if (MathF.Abs(delta) <= maxDelta)
            return target;

        return current + MathF.Sign(delta) * maxDelta;
    }

}
