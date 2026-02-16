using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokemonGreen.Core.Rendering;

namespace PokemonGreen._3D;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private BasicEffect _effect;
    private DaeModel _model;
    private Texture2D _texture;

    private Vector3 _position = Vector3.Zero;
    private float _yaw;
    private float _pitch = -0.3f;
    private float _distance = 8f;

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
        if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();

        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var k = Keyboard.GetState();

        if (k.IsKeyDown(Keys.Q)) _yaw -= 2f * dt;
        if (k.IsKeyDown(Keys.E)) _yaw += 2f * dt;
        if (k.IsKeyDown(Keys.R)) _pitch = MathHelper.Clamp(_pitch - 1f * dt, -1.2f, -0.1f);
        if (k.IsKeyDown(Keys.F)) _pitch = MathHelper.Clamp(_pitch + 1f * dt, -1.2f, -0.1f);
        if (k.IsKeyDown(Keys.PageUp)) _distance = MathHelper.Clamp(_distance - 5f * dt, 4f, 20f);
        if (k.IsKeyDown(Keys.PageDown)) _distance = MathHelper.Clamp(_distance + 5f * dt, 4f, 20f);

        var forward = new Vector3(MathF.Sin(_yaw), 0, MathF.Cos(_yaw));
        var right = new Vector3(MathF.Cos(_yaw), 0, -MathF.Sin(_yaw));
        var speed = (k.IsKeyDown(Keys.LeftShift) ? 8f : 4f) * dt;

        var move = Vector3.Zero;
        if (k.IsKeyDown(Keys.W) || k.IsKeyDown(Keys.Up)) move += forward;
        if (k.IsKeyDown(Keys.S) || k.IsKeyDown(Keys.Down)) move -= forward;
        if (k.IsKeyDown(Keys.A) || k.IsKeyDown(Keys.Left)) move -= right;
        if (k.IsKeyDown(Keys.D) || k.IsKeyDown(Keys.Right)) move += right;

        if (move.LengthSquared() > 0)
        {
            _position += Vector3.Normalize(move) * speed;
        }

        var camX = _position.X + MathF.Sin(_yaw) * MathF.Cos(_pitch) * _distance;
        var camY = _position.Y + 2f - MathF.Sin(_pitch) * _distance;
        var camZ = _position.Z + MathF.Cos(_yaw) * MathF.Cos(_pitch) * _distance;

        _effect.View = Matrix.CreateLookAt(new Vector3(camX, camY, camZ), _position + Vector3.Up * 2f, Vector3.Up);
        _effect.Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, GraphicsDevice.Viewport.AspectRatio, 0.1f, 1000f);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        if (_model?.VertexBuffer != null)
        {
            _effect.World = Matrix.CreateScale(0.015f) * Matrix.CreateTranslation(_position + Vector3.Up * 1.5f);
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
}
