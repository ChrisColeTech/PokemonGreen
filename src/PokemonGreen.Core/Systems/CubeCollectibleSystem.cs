#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.UI;
using PokemonGreen.Core.UI.Fonts;

namespace PokemonGreen.Core.Systems;

/// <summary>
/// Manages collectible cube spawning, animation, collection detection, and rendering.
/// </summary>
public class CubeCollectibleSystem
{
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

    private const float CubeSize = 0.4f;
    private const float CubeHoverHeight = 0.8f;
    private const float CubeCollectRadius = 1.5f;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly BasicEffect _effect;
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly KermFontRenderer? _kermFontRenderer;
    private readonly KermFont? _kermFont;

    private bool[] _cubeCollected;
    private int _cubeCount;
    private readonly VertexPositionColor[] _cubeVertices;
    private readonly short[] _cubeIndices;
    private float _cubeRotation;
    private float _cubeBobTimer;

    /// <summary>Number of cubes collected so far.</summary>
    public int CubeCount => _cubeCount;

    /// <summary>Total number of cubes in the world.</summary>
    public int TotalCubes => CubeSpawnPositions.Length;

    public CubeCollectibleSystem(GraphicsDevice graphicsDevice, BasicEffect effect,
        SpriteBatch spriteBatch, Texture2D pixel,
        KermFontRenderer? kermFontRenderer, KermFont? kermFont)
    {
        _graphicsDevice = graphicsDevice;
        _effect = effect;
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _kermFontRenderer = kermFontRenderer;
        _kermFont = kermFont;

        (_cubeVertices, _cubeIndices) = CreateCubeMesh(CubeSize, new Color(255, 200, 50));
        _cubeCollected = new bool[CubeSpawnPositions.Length];
        _cubeCount = 0;
    }

    /// <summary>
    /// Initialize the collected state from persisted story flags.
    /// Call after loading save data.
    /// </summary>
    public void LoadFromFlags(System.Collections.Generic.HashSet<string> storyFlags)
    {
        _cubeCollected = new bool[CubeSpawnPositions.Length];
        _cubeCount = 0;

        for (int i = 0; i < CubeSpawnPositions.Length; i++)
        {
            if (storyFlags.Contains($"cube_{i}"))
            {
                _cubeCollected[i] = true;
                _cubeCount++;
            }
        }
    }

    /// <summary>Advance cube animation timers.</summary>
    public void UpdateAnimation(float dt)
    {
        _cubeRotation += dt * 1.5f;
        _cubeBobTimer += dt;
    }

    /// <summary>
    /// Check if the player is close enough to collect a cube.
    /// Returns true if a cube was collected (caller should show a message and save).
    /// Also adds the flag to the provided storyFlags set.
    /// </summary>
    public bool CheckCollection(Vector3 playerPosition, System.Collections.Generic.HashSet<string> storyFlags)
    {
        var playerXZ = new Vector2(playerPosition.X, playerPosition.Z);

        for (int i = 0; i < CubeSpawnPositions.Length; i++)
        {
            if (_cubeCollected[i]) continue;

            var cubeXZ = new Vector2(CubeSpawnPositions[i].X, CubeSpawnPositions[i].Z);
            float dist = Vector2.Distance(playerXZ, cubeXZ);

            if (dist < CubeCollectRadius)
            {
                _cubeCollected[i] = true;
                _cubeCount++;
                storyFlags.Add($"cube_{i}");
                return true;
            }
        }
        return false;
    }

    /// <summary>Reset all cubes to uncollected and remove their flags.</summary>
    public void ResetAll(System.Collections.Generic.HashSet<string> storyFlags)
    {
        for (int i = 0; i < CubeSpawnPositions.Length; i++)
        {
            _cubeCollected[i] = false;
            storyFlags.Remove($"cube_{i}");
        }
        _cubeCount = 0;
    }

    /// <summary>Draw the 3D rotating cubes in the world (call outside SpriteBatch).</summary>
    public void DrawCubes(Matrix view, Matrix projection)
    {
        if (_cubeVertices == null) return;

        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

        _effect.View = view;
        _effect.Projection = projection;

        float bob = MathF.Sin(_cubeBobTimer * 2f) * 0.15f;

        for (int i = 0; i < CubeSpawnPositions.Length; i++)
        {
            if (_cubeCollected[i]) continue;

            var pos = CubeSpawnPositions[i];
            _effect.World =
                Matrix.CreateRotationY(_cubeRotation)
                * Matrix.CreateTranslation(pos.X, CubeHoverHeight + bob, pos.Z);

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _cubeVertices, 0, _cubeVertices.Length,
                    _cubeIndices, 0, _cubeIndices.Length / 3);
            }
        }
    }

    /// <summary>Draw the cube counter HUD element (call within SpriteBatch.Begin/End).</summary>
    public void DrawCounter(int fontScale = 2)
    {
        int total = CubeSpawnPositions.Length;
        string text = $"Cubes: {_cubeCount} / {total}";

        // Background panel
        int px = 12, py = 12, padX = 12, padY = 8;
        int textW = text.Length * 6 * fontScale; // approximate
        int textH = 7 * fontScale;

        if (_kermFont != null)
        {
            var size = _kermFont.MeasureString(text);
            textW = size.X * fontScale;
            textH = size.Y * fontScale;
        }

        var panelRect = new Rectangle(px, py, textW + padX * 2, textH + padY * 2);
        UIStyle.DrawBattlePanel(_spriteBatch, _pixel, panelRect);

        if (_kermFontRenderer != null)
        {
            _kermFontRenderer.DrawString(_spriteBatch, text,
                new Vector2(px + padX, py + padY), fontScale, Color.White);
        }
    }

    // ── Mesh generation ──────────────────────────────────────────────

    private static (VertexPositionColor[] verts, short[] indices) CreateCubeMesh(float size, Color color)
    {
        float s = size / 2f;
        var darkColor = new Color(
            (int)(color.R * 0.6f), (int)(color.G * 0.6f), (int)(color.B * 0.6f));
        var midColor = new Color(
            (int)(color.R * 0.8f), (int)(color.G * 0.8f), (int)(color.B * 0.8f));

        // 24 vertices (4 per face for distinct face shading)
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
}
