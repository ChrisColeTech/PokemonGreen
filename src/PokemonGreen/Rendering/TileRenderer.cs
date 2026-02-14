using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.Maps;
using PokemonGreen.Core.Systems;

namespace PokemonGreen.Rendering;

/// <summary>
/// Static class for rendering tile maps using sprite PNGs with color fallback.
/// Supports animated tile sequences (e.g. tile_water_0, tile_water_1, ...).
/// </summary>
public static class TileRenderer
{
    private static readonly Dictionary<int, Texture2D> _tileTextures = new();
    private static readonly Dictionary<string, Texture2D> _spriteTextures = new();

    // Animated tiles: tile ID → array of frame textures
    private static readonly Dictionary<int, Texture2D[]> _animatedTiles = new();
    private static float _animTimer;
    private static float _animFrameDuration = 0.3f;
    private static int _animFrame;

    private static bool _spritesLoaded;

    /// <summary>
    /// Loads all tile sprite PNGs from the Content/sprites directory.
    /// Call once after TextureStore.Initialize().
    /// </summary>
    public static void LoadSprites(string contentRootPath)
    {
        // Resolve relative to executable directory
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var spritesDir = Path.Combine(baseDir, contentRootPath, "sprites");
        if (!Directory.Exists(spritesDir))
            return;

        foreach (var file in Directory.GetFiles(spritesDir, "*.png"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            _spriteTextures[name] = TextureStore.LoadFromFile(file);
        }

        _spritesLoaded = true;
    }

    /// <summary>
    /// Advances the animation timer. Call once per frame.
    /// </summary>
    public static void Update(float deltaTime)
    {
        _animTimer += deltaTime;
        if (_animTimer >= _animFrameDuration)
        {
            _animTimer -= _animFrameDuration;
            _animFrame++;
        }
    }

    /// <summary>
    /// Draws the tile map using the camera's viewport.
    /// </summary>
    public static void DrawMap(
        SpriteBatch spriteBatch,
        TileMap map,
        Camera camera,
        GraphicsDevice graphicsDevice,
        int tileSize = 32)
    {
        var bounds = camera.Bounds;
        int startX = Math.Max(0, bounds.Left / tileSize);
        int startY = Math.Max(0, bounds.Top / tileSize);
        int endX = Math.Min(map.Width, (bounds.Right / tileSize) + 2);
        int endY = Math.Min(map.Height, (bounds.Bottom / tileSize) + 2);

        // Draw base layer
        for (int x = startX; x < endX; x++)
            for (int y = startY; y < endY; y++)
                DrawTile(spriteBatch, map.GetBaseTile(x, y), x, y, camera, graphicsDevice, tileSize);

        // Draw overlay layer
        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                int overlayId = map.GetOverlayTile(x, y);
                if (overlayId >= 0)
                    DrawTile(spriteBatch, overlayId, x, y, camera, graphicsDevice, tileSize);
            }
        }
    }

    private static void DrawTile(
        SpriteBatch spriteBatch,
        int tileId,
        int tileX,
        int tileY,
        Camera camera,
        GraphicsDevice graphicsDevice,
        int tileSize)
    {
        var tile = TileRegistry.GetTile(tileId);
        if (tile == null)
            return;

        var texture = GetAnimatedFrame(tileId, tile, graphicsDevice)
                      ?? GetTileTexture(tileId, tile, graphicsDevice);

        int worldX = tileX * tileSize;
        int worldY = tileY * tileSize;

        spriteBatch.Draw(
            texture,
            new Rectangle(worldX, worldY, tileSize, tileSize),
            Color.White);
    }

    /// <summary>
    /// Returns the current animation frame for an animated tile, or null if not animated.
    /// </summary>
    private static Texture2D? GetAnimatedFrame(int tileId, TileDefinition tile, GraphicsDevice graphicsDevice)
    {
        if (!_spritesLoaded)
            return null;

        if (_animatedTiles.TryGetValue(tileId, out var frames))
            return frames[_animFrame % frames.Length];

        // Check if this tile has a numbered sequence (e.g. tile_water_0, tile_water_1, ...)
        var baseName = "tile_" + PascalToSnake(tile.Name);
        var sequenceFrames = new List<Texture2D>();
        for (int i = 0; ; i++)
        {
            if (_spriteTextures.TryGetValue($"{baseName}_{i}", out var frame))
                sequenceFrames.Add(frame);
            else
                break;
        }

        if (sequenceFrames.Count > 1)
        {
            var arr = sequenceFrames.ToArray();
            _animatedTiles[tileId] = arr;
            return arr[_animFrame % arr.Length];
        }

        return null;
    }

    private static Texture2D GetTileTexture(int tileId, TileDefinition tile, GraphicsDevice graphicsDevice)
    {
        if (_tileTextures.TryGetValue(tileId, out var cached))
            return cached;

        // Try to find a matching sprite PNG
        if (_spritesLoaded)
        {
            var spriteName = "tile_" + PascalToSnake(tile.Name);
            if (_spriteTextures.TryGetValue(spriteName, out var sprite))
            {
                _tileTextures[tileId] = sprite;
                return sprite;
            }
        }

        // Fall back to solid color
        var color = TextureStore.ParseHexColor(tile.Color);
        var texture = TextureStore.CreateColorTexture(graphicsDevice, color);
        _tileTextures[tileId] = texture;
        return texture;
    }

    /// <summary>
    /// Converts PascalCase to snake_case: "TallGrass" → "tall_grass"
    /// </summary>
    private static string PascalToSnake(string name)
    {
        return Regex.Replace(name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
    }

    public static void Clear()
    {
        _tileTextures.Clear();
        _animatedTiles.Clear();
    }
}
