#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.Maps;
using PokemonGreen.Core.UI;
using PokemonGreen.Core.UI.Fonts;

namespace PokemonGreen.Core.Systems;

/// <summary>
/// Manages collectible coin spawning, animation, collection detection, and rendering.
/// </summary>
public class CubeCollectibleSystem
{
    private static readonly Vector3[] LegacySpawnPositions =
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
    private Vector3[] _spawnPositions;
    private string[] _spawnFlagKeys;

    private bool[] _cubeCollected;
    private int _cubeCount;
    private readonly VertexPositionColor[] _cubeVertices;
    private readonly short[] _cubeIndices;
    private float _cubeRotation;
    private float _cubeBobTimer;

    /// <summary>Number of cubes collected so far.</summary>
    public int CubeCount => _cubeCount;

    /// <summary>Total number of cubes in the world.</summary>
    public int TotalCubes => _spawnPositions.Length;

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

        (_cubeVertices, _cubeIndices) = CreateCoinMesh(radius: CubeSize * 0.55f, thickness: CubeSize * 0.20f,
            faceColor: new Color(255, 210, 70));
        _spawnPositions = (Vector3[])LegacySpawnPositions.Clone();
        _spawnFlagKeys = Enumerable.Range(0, _spawnPositions.Length)
            .Select(i => $"coin_legacy_{i}")
            .ToArray();
        _cubeCollected = new bool[_spawnPositions.Length];
        _cubeCount = 0;
    }

    /// <summary>
    /// Generates coin spawns per map from walkable tile centers.
    /// This keeps all spawns inside loaded map bounds and deterministic.
    /// </summary>
    public void GenerateSpawnsForWorld(string worldId, float tileWorldSize, Func<float, float, bool> canPlace)
    {
        var generated = new List<Vector3>();
        var generatedFlags = new List<string>();
        const int targetTotalCoins = 84;

        var maps = MapCatalog.GetAllMaps()
            .Where(m => string.Equals(m.WorldId, worldId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.WorldY)
            .ThenBy(m => m.WorldX)
            .ThenBy(m => m.Id, StringComparer.Ordinal)
            .ToList();

        var mapCandidates = new List<(MapDefinition map, List<(Vector3 pos, string flag)> candidates)>();
        int totalCandidates = 0;

        foreach (var map in maps)
        {
            float offsetX = map.WorldX * map.Width * tileWorldSize;
            float offsetZ = map.WorldY * map.Height * tileWorldSize;

            var candidates = new List<(Vector3 pos, string flag)>(map.Width * map.Height);
            for (int ty = 0; ty < map.Height; ty++)
            {
                for (int tx = 0; tx < map.Width; tx++)
                {
                    float x = offsetX + (tx + 0.5f) * tileWorldSize;
                    float z = offsetZ + (ty + 0.5f) * tileWorldSize;
                    if (canPlace(x, z))
                        candidates.Add((new Vector3(x, 0f, z), $"coin_{worldId}_{map.Id}_{tx}_{ty}"));
                }
            }

            if (candidates.Count == 0)
                continue;

            mapCandidates.Add((map, candidates));
            totalCandidates += candidates.Count;
        }

        if (mapCandidates.Count > 0 && totalCandidates > 0)
        {
            int worldTarget = Math.Min(targetTotalCoins, totalCandidates);
            var allocated = new int[mapCandidates.Count];
            var remainders = new float[mapCandidates.Count];

            int allocatedSum = 0;
            for (int i = 0; i < mapCandidates.Count; i++)
            {
                float exact = worldTarget * (mapCandidates[i].candidates.Count / (float)totalCandidates);
                int baseCount = Math.Min((int)MathF.Floor(exact), mapCandidates[i].candidates.Count);
                allocated[i] = baseCount;
                remainders[i] = exact - baseCount;
                allocatedSum += baseCount;
            }

            while (allocatedSum < worldTarget)
            {
                int bestIndex = -1;
                float bestRemainder = float.MinValue;
                for (int i = 0; i < mapCandidates.Count; i++)
                {
                    if (allocated[i] >= mapCandidates[i].candidates.Count)
                        continue;

                    if (remainders[i] > bestRemainder)
                    {
                        bestRemainder = remainders[i];
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0)
                    break;

                allocated[bestIndex]++;
                remainders[bestIndex] = 0f;
                allocatedSum++;
            }

            for (int i = 0; i < mapCandidates.Count; i++)
            {
                int coinsForMap = allocated[i];
                if (coinsForMap <= 0)
                    continue;

                var map = mapCandidates[i].map;
                var candidates = mapCandidates[i].candidates;
                int seed = StableHash32($"{worldId}:{map.Id}:{map.Width}:{map.Height}");
                foreach (int idx in PickUniqueIndices(candidates.Count, coinsForMap, seed))
                {
                    generated.Add(candidates[idx].pos);
                    generatedFlags.Add(candidates[idx].flag);
                }
            }
        }

        if (generated.Count == 0)
        {
            generated.AddRange(LegacySpawnPositions);
            generatedFlags.AddRange(Enumerable.Range(0, LegacySpawnPositions.Length)
                .Select(i => $"coin_legacy_{i}"));
        }

        _spawnPositions = generated.ToArray();
        _spawnFlagKeys = generatedFlags.ToArray();
        _cubeCollected = new bool[_spawnPositions.Length];
        _cubeCount = 0;
        Console.WriteLine($"[Coins] Generated {_spawnPositions.Length} coin spawns in world '{worldId}'.");
    }

    /// <summary>
    /// Initialize the collected state from persisted story flags.
    /// Call after loading save data.
    /// </summary>
    public void LoadFromFlags(System.Collections.Generic.HashSet<string> storyFlags)
    {
        _cubeCollected = new bool[_spawnPositions.Length];
        _cubeCount = 0;

        for (int i = 0; i < _spawnPositions.Length; i++)
        {
            bool collected = storyFlags.Contains(_spawnFlagKeys[i]);
            if (!collected && storyFlags.Contains($"cube_{i}"))
            {
                collected = true;
                storyFlags.Add(_spawnFlagKeys[i]);
            }

            if (collected)
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

        for (int i = 0; i < _spawnPositions.Length; i++)
        {
            if (_cubeCollected[i]) continue;

            var cubeXZ = new Vector2(_spawnPositions[i].X, _spawnPositions[i].Z);
            float dist = Vector2.Distance(playerXZ, cubeXZ);

            if (dist < CubeCollectRadius)
            {
                _cubeCollected[i] = true;
                _cubeCount++;
                storyFlags.Add(_spawnFlagKeys[i]);
                return true;
            }
        }
        return false;
    }

    /// <summary>Reset all cubes to uncollected and remove their flags.</summary>
    public void ResetAll(System.Collections.Generic.HashSet<string> storyFlags)
    {
        var keysToRemove = storyFlags
            .Where(k => k.StartsWith("coin_", StringComparison.Ordinal) || k.StartsWith("cube_", StringComparison.Ordinal))
            .ToList();
        foreach (var key in keysToRemove)
            storyFlags.Remove(key);

        for (int i = 0; i < _spawnPositions.Length; i++)
        {
            _cubeCollected[i] = false;
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

        for (int i = 0; i < _spawnPositions.Length; i++)
        {
            if (_cubeCollected[i]) continue;

            var pos = _spawnPositions[i];
            _effect.World =
                Matrix.CreateRotationX(0.24f)
                * Matrix.CreateRotationY(_cubeRotation * 2.3f)
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
        string text = $"Coins: {_cubeCount}";

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

    private static (VertexPositionColor[] verts, short[] indices) CreateCoinMesh(float radius, float thickness, Color faceColor)
    {
        const int segments = 16;
        float half = thickness * 0.5f;

        var edgeColor = new Color(
            (int)(faceColor.R * 0.78f),
            (int)(faceColor.G * 0.68f),
            (int)(faceColor.B * 0.35f));
        var backColor = new Color(
            (int)(faceColor.R * 0.82f),
            (int)(faceColor.G * 0.82f),
            (int)(faceColor.B * 0.82f));

        var verts = new System.Collections.Generic.List<VertexPositionColor>(segments * 4 + 2);
        var indices = new System.Collections.Generic.List<short>(segments * 12);

        short frontCenter = (short)verts.Count;
        verts.Add(new VertexPositionColor(new Vector3(0f, 0f, half), faceColor));

        short backCenter = (short)verts.Count;
        verts.Add(new VertexPositionColor(new Vector3(0f, 0f, -half), backColor));

        for (int i = 0; i < segments; i++)
        {
            float t = MathHelper.TwoPi * i / segments;
            float x = MathF.Cos(t) * radius;
            float y = MathF.Sin(t) * radius;

            verts.Add(new VertexPositionColor(new Vector3(x, y, half), faceColor));
            verts.Add(new VertexPositionColor(new Vector3(x, y, -half), backColor));
        }

        for (int i = 0; i < segments; i++)
        {
            short i0f = (short)(2 + i * 2);
            short i0b = (short)(i0f + 1);
            short i1f = (short)(2 + ((i + 1) % segments) * 2);
            short i1b = (short)(i1f + 1);

            indices.Add(frontCenter);
            indices.Add(i0f);
            indices.Add(i1f);

            indices.Add(backCenter);
            indices.Add(i1b);
            indices.Add(i0b);

            short sideBase = (short)verts.Count;
            verts.Add(new VertexPositionColor(new Vector3(verts[i0f].Position.X, verts[i0f].Position.Y, half), edgeColor));
            verts.Add(new VertexPositionColor(new Vector3(verts[i1f].Position.X, verts[i1f].Position.Y, half), edgeColor));
            verts.Add(new VertexPositionColor(new Vector3(verts[i1b].Position.X, verts[i1b].Position.Y, -half), edgeColor));
            verts.Add(new VertexPositionColor(new Vector3(verts[i0b].Position.X, verts[i0b].Position.Y, -half), edgeColor));

            indices.Add(sideBase);
            indices.Add((short)(sideBase + 1));
            indices.Add((short)(sideBase + 2));
            indices.Add(sideBase);
            indices.Add((short)(sideBase + 2));
            indices.Add((short)(sideBase + 3));
        }

        return (verts.ToArray(), indices.ToArray());
    }

    private static IEnumerable<int> PickUniqueIndices(int total, int pickCount, int seed)
    {
        var indices = Enumerable.Range(0, total).ToArray();
        var rng = new Random(seed);

        for (int i = indices.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        for (int i = 0; i < pickCount; i++)
            yield return indices[i];
    }

    private static int StableHash32(string text)
    {
        unchecked
        {
            int hash = (int)2166136261;
            for (int i = 0; i < text.Length; i++)
                hash = (hash ^ text[i]) * 16777619;
            return hash;
        }
    }
}
