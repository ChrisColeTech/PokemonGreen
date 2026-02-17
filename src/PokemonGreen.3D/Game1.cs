#nullable enable
using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokemonGreen.Core.Rendering;
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
    private SplitModelAnimationSet? _animationSet;
    private SkeletalAnimator? _animator;
    private string _activeClip = string.Empty;
    private VertexPositionColor[] _gridVertices;

    // UI overlay system
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private KermFont? _kermFont;
    private KermFontRenderer? _kermFontRenderer;
    private CharacterSelectScreen? _overlay;
    private KeyboardState _prevKeyboard;

    // Character data
    private string _assetsRoot;
    private string _currentCharacterFolder = "tr0001_00_fi";
    private static readonly (string folder, string name)[] Characters =
    {
        ("tr0001_00_fi", "Character 1"),
        ("tr0002_00_fi", "Character 2"),
        ("tr0003_00_fi", "Character 3"),
        ("tr0004_00_fi", "Character 4"),
        ("tr0005_00_fi", "Character 5"),
        ("tr0006_00_fi", "Character 6"),
    };

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
    private const float PlayerModelHeightOffset = 1.5f;
    private const float PlayerModelScale = 0.015f;
    private const int GridHalfSize = 30;
    private const float GridY = 0f;
    private const float PlayerTurnSpeed = 8f;
    private const float CameraYawFollowSpeed = 3.5f;
    private const float CameraFollowDelay = 0.12f;
    private const float JumpVelocity = 6.5f;
    private const float Gravity = -20f;

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

        _camera.Pitch = -0.3f;
        _camera.Distance = 8f;
        _camera.MinDistance = 4f;
        _camera.MaxDistance = 20f;

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

        _animationSet = SplitModelAnimationSetLoader.Load(dir);
        _animator = new SkeletalAnimator(_animationSet.Skeleton);

        _model = new SkinnedDaeModel();
        _model.Load(GraphicsDevice, _animationSet.ModelPath, _animationSet.Skeleton);

        // Start with idle animation
        string idleClip = ResolveMovementClip(_animationSet, isMoving: false, isRunning: false);
        if (!string.IsNullOrEmpty(idleClip) && _animationSet.Clips.TryGetValue(idleClip, out var clip))
        {
            _activeClip = idleClip;
            _animator.Play(clip, loop: true, resetTime: true);
            _model.UpdatePose(GraphicsDevice, _animator.SkinPose);
        }

        _currentCharacterFolder = folderName;
    }

    protected override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var keyboard = Keyboard.GetState();

        // Handle overlay
        if (_overlay != null)
        {
            var uiInput = BuildInputState(keyboard);
            _overlay.Update(dt, uiInput);

            if (_overlay.IsFinished)
            {
                if (_overlay.SelectedFolder != null)
                    LoadCharacterModel(_overlay.SelectedFolder);
                _overlay = null;
            }

            _prevKeyboard = keyboard;
            base.Update(gameTime);
            return;
        }

        // Tab or Escape opens character select
        if ((keyboard.IsKeyDown(Keys.Tab) && !_prevKeyboard.IsKeyDown(Keys.Tab))
            || (keyboard.IsKeyDown(Keys.Escape) && !_prevKeyboard.IsKeyDown(Keys.Escape)))
        {
            _overlay = new CharacterSelectScreen(
                Characters.Select(c => c.folder).ToArray(),
                Characters.Select(c => c.name).ToArray());
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
            _playerPosition += moveDir * speed;
            _playerTargetYaw = MathF.Atan2(moveDir.X, moveDir.Z);
        }

        if (_animationSet is not null && _animator is not null)
        {
            string targetClip = ResolveMovementClip(_animationSet, isMoving, input.IsRunning);
            if (!string.Equals(targetClip, _activeClip, StringComparison.Ordinal) && _animationSet.Clips.TryGetValue(targetClip, out SkeletalAnimationClip? clip))
            {
                _activeClip = targetClip;
                _animator.Play(clip, loop: true, resetTime: false);
            }

            _animator.Update(dt);
            _model?.UpdatePose(GraphicsDevice, _animator.SkinPose);
        }

        var isGrounded = _playerPosition.Y <= 0.001f;
        var jumpPressed = input.JumpPressed;
        if (jumpPressed && isGrounded)
        {
            _verticalVelocity = JumpVelocity;
            isGrounded = false;
        }

        if (!isGrounded)
            _verticalVelocity += Gravity * dt;

        _playerPosition.Y += _verticalVelocity * dt;
        if (_playerPosition.Y < 0f)
        {
            _playerPosition.Y = 0f;
            _verticalVelocity = 0f;
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

        // Draw UI overlay on top of 3D scene
        if (_overlay != null)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            _overlay.Draw(_spriteBatch, _pixel, _kermFontRenderer, _kermFont,
                null!, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            _spriteBatch.End();
        }

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

    private static float MoveTowardsAngle(float current, float target, float maxDelta)
    {
        var delta = MathHelper.WrapAngle(target - current);
        if (MathF.Abs(delta) <= maxDelta)
            return target;

        return current + MathF.Sign(delta) * maxDelta;
    }

    private static string ResolveMovementClip(SplitModelAnimationSet set, bool isMoving, bool isRunning)
    {
        // Spica format: Motion_0 = idle, Motion_1 = walk, Motion_2 = run
        // OhanaCli format: anim_0 = idle, anim_1 = walk, anim_2 = run
        if (!isMoving) return FindClip(set, "Motion_0", "anim_0");
        if (isRunning) return FindClip(set, "Motion_2", "anim_2");
        return FindClip(set, "Motion_1", "anim_1");
    }

    private static string FindClip(SplitModelAnimationSet set, string primary, string fallback)
    {
        if (set.Clips.ContainsKey(primary)) return primary;
        if (set.Clips.ContainsKey(fallback)) return fallback;
        return set.Clips.Keys.OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault() ?? string.Empty;
    }
}
