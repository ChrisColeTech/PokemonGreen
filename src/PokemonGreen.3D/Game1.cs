using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.Rendering;
using PokemonGreen.Core.Systems;

namespace PokemonGreen._3D;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private BasicEffect _effect;
    private BasicEffect _gridEffect;
    private DaeModel _model;
    private Texture2D _texture;
    private VertexPositionColor[] _gridVertices;

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
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
    }

    protected override void Initialize()
    {
        _effect = new BasicEffect(GraphicsDevice);
        _effect.LightingEnabled = true;
        _effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-1, -2, -1));
        _effect.DirectionalLight0.DiffuseColor = new Vector3(0.7f);
        _effect.AmbientLightColor = new Vector3(0.3f);

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
        var dir = "D:/Projects/PokemonGreen/src/PokemonGreen.Tests/exports-field-models/0/0002_tr0001_00_fi";
        var dae = Path.Combine(dir, "tr0001_00_fi.dae");
        var tex = Path.Combine(dir, "tr0001_00_BodyA.tga.png");

        if (File.Exists(dae))
        {
            _model = new DaeModel();
            _model.Load(GraphicsDevice, dae);
        }

        if (File.Exists(tex))
        {
            using var stream = File.OpenRead(tex);
            _texture = Texture2D.FromStream(GraphicsDevice, stream);
        }
    }

    protected override void Update(GameTime gameTime)
    {
        _input.Update();
        var input = _input.State;
        if (input.ExitRequested) Exit();

        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        _playerTargetYaw += input.Turn * 2f * dt;
        _camera.Rotate(0f, input.Pitch * dt);
        _camera.Zoom(-input.Zoom * 5f * dt);

        var cameraForward = new Vector3(-MathF.Sin(_cameraYaw), 0, -MathF.Cos(_cameraYaw));
        var cameraRight = Vector3.Normalize(Vector3.Cross(cameraForward, Vector3.Up));
        var speed = (input.IsRunning ? 8f : 4f) * dt;

        var moveInputX = input.MoveX;
        var moveInputZ = input.MoveZ;

        var move = cameraForward * moveInputZ + cameraRight * moveInputX;

        if (move.LengthSquared() > 0)
        {
            var moveDir = Vector3.Normalize(move);
            _playerPosition += moveDir * speed;
            _playerTargetYaw = MathF.Atan2(moveDir.X, moveDir.Z);
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
            _effect.World =
                Matrix.CreateScale(PlayerModelScale)
                * Matrix.CreateRotationY(_playerYaw)
                * Matrix.CreateTranslation(_playerPosition + Vector3.Up * PlayerModelHeightOffset);
            _effect.Texture = _texture;
            _effect.TextureEnabled = _texture != null;

            GraphicsDevice.SetVertexBuffer(_model.VertexBuffer);
            GraphicsDevice.Indices = _model.IndexBuffer;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _model.PrimitiveCount);
            }
        }

        base.Draw(gameTime);
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
}
