#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokemonGreen.Core.Maps;
using PokemonGreen.Core.Rendering;
using PokemonGreen.Core.Rendering.Skeletal;
using PokemonGreen.Core.Save;
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

    // Persistence
    private readonly SaveManager _saveManager = new();
    private const int SaveSlot = 99; // dedicated slot for 3D POC
    private HashSet<string> _storyFlags = new();

    // Collectible cubes
    private static readonly Vector3[] CubeSpawnPositions =
    {
        // Center map area
        new( 3, 0,  5), new(-4, 0,  8), new( 7, 0, -3),
        new(-6, 0, -7), new(10, 0,  2), new(-2, 0, 12),
        new( 8, 0, -9), new(-9, 0,  4), new( 5, 0, -12),
        new(12, 0,  9), new(-11, 0, -2), new( 1, 0, 15),
        // North map
        new( 4, 0, -20), new(14, 0, -25), new(24, 0, -18),
        new(10, 0, -30), new(20, 0, -22), new( 8, 0, -15),
        // South map
        new( 6, 0,  38), new(18, 0,  42), new(26, 0,  35),
        new(12, 0,  50), new(22, 0,  45), new( 2, 0,  55),
        // West map
        new(-18, 0,  6), new(-24, 0, 14), new(-12, 0, 22),
        new(-28, 0, 10), new(-20, 0, 26), new(-15, 0, 18),
        // East map
        new( 38, 0,  4), new( 44, 0, 12), new( 50, 0,  8),
        new( 36, 0, 20), new( 42, 0, 26), new( 55, 0, 16),
        // Scattered extras
        new( 16, 0, 16), new( 30, 0, 30), new(-5, 0, 30),
        new( 28, 0, -8), new(-22, 0, -4), new( 48, 0, 22),
    };
    private bool[] _cubeCollected;
    private int _cubeCount;
    private VertexPositionColor[] _cubeVertices;
    private short[] _cubeIndices;
    private float _cubeRotation;
    private float _cubeBobTimer;
    private const float CubeSize = 0.4f;
    private const float CubeHoverHeight = 0.8f;
    private const float CubeCollectRadius = 1.5f;

    // Encounter system
    private readonly Random _encounterRng = new();
    private float _encounterStepTimer;
    private const float EncounterStepInterval = 0.4f; // check every 0.4s of walking
    private const float EncounterChance = 0.15f; // 15% per check

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
        (_cubeVertices, _cubeIndices) = CreateCubeMesh(CubeSize, new Color(255, 200, 50));

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
        LoadSaveData();

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
                new Color(239, 239, 239, 255),
                new Color(80, 80, 80, 90),
                new Color(40, 40, 40, 60),
            };
            _kermFont = new KermFont(GraphicsDevice, kermFontPath, palette: palette);
            _kermFontRenderer = new KermFontRenderer(_kermFont);
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
        _cubeRotation += dt * 1.5f;
        _cubeBobTimer += dt;

        // Handle message box (blocks all other input)
        if (_messageBox.IsActive)
        {
            _messageBox.Update(dt, confirmPressed);
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
                    PersistCharacterSelection(_overlay.SelectedFolder);
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
                bool fullOk = _tileMapMesh.IsWalkable(desiredPos.X, desiredPos.Z);
                if (fullOk)
                {
                    _playerPosition = desiredPos;
                }
                else
                {
                    // Try sliding along X axis only
                    var slideX = new Vector3(desiredPos.X, _playerPosition.Y, _playerPosition.Z);
                    if (_tileMapMesh.IsWalkable(slideX.X, slideX.Z))
                        _playerPosition = slideX;

                    // Try sliding along Z axis only
                    var slideZ = new Vector3(_playerPosition.X, _playerPosition.Y, desiredPos.Z);
                    if (_tileMapMesh.IsWalkable(slideZ.X, slideZ.Z))
                        _playerPosition = slideZ;
                }
            }
            else
            {
                _playerPosition = desiredPos;
            }

            _playerTargetYaw = MathF.Atan2(moveDir.X, moveDir.Z);
        }

        var isGrounded = _playerPosition.Y <= 0.001f;
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
        if (_playerPosition.Y < 0f)
        {
            _playerPosition.Y = 0f;
            _verticalVelocity = 0f;
            _isJumping = false;
        }

        // Check cube collection
        CheckCubeCollection();

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
        GraphicsDevice.Clear(Color.CornflowerBlue);

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
        DrawCubes();

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

        // 2D UI overlay pass
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

        // Cube counter (upper-left)
        DrawCubeCounter();

        // Character select overlay
        if (_overlay != null)
        {
            _overlay.Draw(_spriteBatch, _pixel, _kermFontRenderer, _kermFont,
                null!, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }

        // Pause menu (top-right, same as 2D game)
        if (_isPaused)
        {
            int vw = GraphicsDevice.Viewport.Width;
            int menuW = 160;
            int menuH = 140;
            int menuX = vw - menuW - 16;
            int menuY = 16;

            if (_kermFontRenderer != null && _kermFont != null)
                _pauseMenuBox.Draw(_spriteBatch, _kermFontRenderer, _kermFont, _pixel,
                    new Rectangle(menuX, menuY, menuW, menuH), 3);
        }

        // Message box (bottom of screen)
        if (_messageBox.IsActive)
        {
            int vw = GraphicsDevice.Viewport.Width;
            int vh = GraphicsDevice.Viewport.Height;
            int boxH = 80;
            int margin = 20;
            var bounds = new Rectangle(margin, vh - boxH - margin, vw - margin * 2, boxH);

            if (_kermFontRenderer != null)
                _messageBox.Draw(_spriteBatch, _kermFontRenderer, _pixel, bounds, fontScale: 1);
        }

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private InputState BuildInputState(KeyboardState keyboard)
    {
        var mouse = Mouse.GetState();
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
            PageLeft = (keyboard.IsKeyDown(Keys.Q) && !_prevKeyboard.IsKeyDown(Keys.Q))
                    || (keyboard.IsKeyDown(Keys.PageUp) && !_prevKeyboard.IsKeyDown(Keys.PageUp)),
            PageRight = (keyboard.IsKeyDown(Keys.E) && !_prevKeyboard.IsKeyDown(Keys.E))
                     || (keyboard.IsKeyDown(Keys.PageDown) && !_prevKeyboard.IsKeyDown(Keys.PageDown)),
            MousePosition = mouse.Position,
            MouseClicked = mouse.LeftButton == ButtonState.Pressed,
        };
    }

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

    // ── Persistence ──────────────────────────────────────────────────

    private void LoadSaveData()
    {
        _cubeCollected = new bool[CubeSpawnPositions.Length];
        _cubeCount = 0;

        var saveData = _saveManager.Load(SaveSlot);
        if (saveData != null)
        {
            _storyFlags = saveData.StoryFlags;

            // Restore collected cubes from story flags
            for (int i = 0; i < CubeSpawnPositions.Length; i++)
            {
                if (_storyFlags.Contains($"cube_{i}"))
                {
                    _cubeCollected[i] = true;
                    _cubeCount++;
                }
            }

            // Restore character selection
            if (!string.IsNullOrEmpty(saveData.SelectedCharacter))
            {
                _currentCharacterFolder = saveData.SelectedCharacter;
                Console.WriteLine($"[Save] Restored character: {_currentCharacterFolder}");
            }

            Console.WriteLine($"[Save] Loaded {_cubeCount} collected cubes from slot {SaveSlot}");
        }
        else
        {
            _storyFlags = new HashSet<string>();
            Console.WriteLine("[Save] No save found, starting fresh");
        }
    }

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
        for (int i = 0; i < CubeSpawnPositions.Length; i++)
        {
            _cubeCollected[i] = false;
            _storyFlags.Remove($"cube_{i}");
        }
        _cubeCount = 0;
        PerformSave();
        ClosePauseMenu();
        _messageBox.Show("All cubes have been reset!");
    }

    private void PersistCharacterSelection(string folderName)
    {
        PerformSave();
    }

    private void PerformSave()
    {
        var data = new GameSaveData
        {
            PlayerName = "Red",
            MapId = "3d_overworld",
            PlayerX = _playerPosition.X,
            PlayerY = _playerPosition.Z, // map Y = world Z
            SelectedCharacter = _currentCharacterFolder,
            StoryFlags = _storyFlags,
            SavedAt = DateTime.UtcNow,
        };
        _saveManager.Save(SaveSlot, data);
        Console.WriteLine($"[Save] Saved {_cubeCount} cubes to slot {SaveSlot}");
    }

    // ── Cube Collection ───────────────────────────────────────────────

    private void CheckCubeCollection()
    {
        var playerXZ = new Vector2(_playerPosition.X, _playerPosition.Z);

        for (int i = 0; i < CubeSpawnPositions.Length; i++)
        {
            if (_cubeCollected[i]) continue;

            var cubeXZ = new Vector2(CubeSpawnPositions[i].X, CubeSpawnPositions[i].Z);
            float dist = Vector2.Distance(playerXZ, cubeXZ);

            if (dist < CubeCollectRadius)
            {
                _cubeCollected[i] = true;
                _cubeCount++;
                _storyFlags.Add($"cube_{i}");

                _messageBox.Show("You found another cube!");
                _messageBox.OnFinished = null; // just dismiss

                PerformSave();
                break; // one per frame
            }
        }
    }

    // ── Encounter ──────────────────────────────────────────────────────

    private void CheckEncounterTile()
    {
        if (_tileMapMesh == null || _messageBox.IsActive) return;

        string? behavior = _tileMapMesh.GetOverlayBehavior(_playerPosition.X, _playerPosition.Z);
        if (behavior == null || !behavior.Contains("encounter")) return;

        _encounterStepTimer += (float)TargetElapsedTime.TotalSeconds;
        if (_encounterStepTimer < EncounterStepInterval) return;
        _encounterStepTimer = 0f;

        if (_encounterRng.NextDouble() < EncounterChance)
        {
            string encounterType = behavior switch
            {
                "wild_encounter" => "A wild Pokemon appeared!",
                "rare_encounter" => "A rare Pokemon appeared!",
                "double_encounter" => "Wild Pokemon appeared!",
                "cave_encounter" => "A wild cave Pokemon appeared!",
                "fire_encounter" => "A wild fire Pokemon appeared!",
                _ => "A wild Pokemon appeared!"
            };
            _messageBox.Show(encounterType);
        }
    }

    // ── Cube Rendering ────────────────────────────────────────────────

    private void DrawCubes()
    {
        if (_cubeVertices == null) return;

        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

        _gridEffect.View = _effect.View;
        _gridEffect.Projection = _effect.Projection;

        float bob = MathF.Sin(_cubeBobTimer * 2f) * 0.15f;

        for (int i = 0; i < CubeSpawnPositions.Length; i++)
        {
            if (_cubeCollected[i]) continue;

            var pos = CubeSpawnPositions[i];
            _gridEffect.World =
                Matrix.CreateRotationY(_cubeRotation)
                * Matrix.CreateTranslation(pos.X, CubeHoverHeight + bob, pos.Z);

            foreach (var pass in _gridEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _cubeVertices, 0, _cubeVertices.Length,
                    _cubeIndices, 0, _cubeIndices.Length / 3);
            }
        }
    }

    private void DrawCubeCounter()
    {
        int total = CubeSpawnPositions.Length;
        string text = $"Cubes: {_cubeCount} / {total}";

        // Background panel
        int px = 12, py = 12, padX = 12, padY = 8;
        int textW = text.Length * 8; // approximate
        int textH = 16;

        if (_kermFont != null)
        {
            var size = _kermFont.MeasureString(text);
            textW = size.X;
            textH = size.Y;
        }

        var panelRect = new Rectangle(px, py, textW + padX * 2, textH + padY * 2);
        UIStyle.DrawBattlePanel(_spriteBatch, _pixel, panelRect);

        if (_kermFontRenderer != null)
        {
            _kermFontRenderer.DrawString(_spriteBatch, text,
                new Vector2(px + padX, py + padY), 1, Color.White);
        }
    }

    // ── Cube Mesh ─────────────────────────────────────────────────────

    private static (VertexPositionColor[] verts, short[] indices) CreateCubeMesh(float size, Color color)
    {
        float s = size / 2f;
        var darkColor = new Color(
            (int)(color.R * 0.6f), (int)(color.G * 0.6f), (int)(color.B * 0.6f));
        var midColor = new Color(
            (int)(color.R * 0.8f), (int)(color.G * 0.8f), (int)(color.B * 0.8f));

        // 8 corners, colored by face for a bit of shading
        var verts = new VertexPositionColor[]
        {
            // Top face (bright)
            new(new Vector3(-s,  s, -s), color),     // 0
            new(new Vector3( s,  s, -s), color),     // 1
            new(new Vector3( s,  s,  s), color),     // 2
            new(new Vector3(-s,  s,  s), color),     // 3
            // Bottom face (dark)
            new(new Vector3(-s, -s, -s), darkColor),  // 4
            new(new Vector3( s, -s, -s), darkColor),  // 5
            new(new Vector3( s, -s,  s), darkColor),  // 6
            new(new Vector3(-s, -s,  s), darkColor),  // 7
            // Front face (mid)
            new(new Vector3(-s, -s,  s), midColor),   // 8
            new(new Vector3( s, -s,  s), midColor),   // 9
            new(new Vector3( s,  s,  s), color),      // 10
            new(new Vector3(-s,  s,  s), color),      // 11
            // Back face (mid)
            new(new Vector3( s, -s, -s), midColor),   // 12
            new(new Vector3(-s, -s, -s), midColor),   // 13
            new(new Vector3(-s,  s, -s), midColor),   // 14
            new(new Vector3( s,  s, -s), midColor),   // 15
            // Right face (mid-bright)
            new(new Vector3( s, -s,  s), midColor),   // 16
            new(new Vector3( s, -s, -s), midColor),   // 17
            new(new Vector3( s,  s, -s), color),      // 18
            new(new Vector3( s,  s,  s), color),      // 19
            // Left face (dark)
            new(new Vector3(-s, -s, -s), darkColor),  // 20
            new(new Vector3(-s, -s,  s), darkColor),  // 21
            new(new Vector3(-s,  s,  s), midColor),   // 22
            new(new Vector3(-s,  s, -s), midColor),   // 23
        };

        var indices = new short[]
        {
            0,1,2,  0,2,3,       // top
            4,6,5,  4,7,6,       // bottom
            8,9,10, 8,10,11,     // front
            12,13,14, 12,14,15,  // back
            16,17,18, 16,18,19,  // right
            20,21,22, 20,22,23,  // left
        };

        return (verts, indices);
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
