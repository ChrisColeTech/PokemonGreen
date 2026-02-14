using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.Maps;

namespace PokemonGreen.Core.Rendering;

public static class TileRenderer
{
    private static readonly Dictionary<int, Texture2D[]> _animatedTiles = new();
    private static float _animTimer;
    private static readonly float _animFrameDuration = 0.3f;
    private static int _animFrame;

    private static readonly Dictionary<int, float> _visualScale = new()
    {
        [16] = 1.8f,
        [20] = 1.4f,
        [22] = 1.3f,
        [18] = 0.55f,
        [19] = 1.5f,
    };

    public static void Update(float deltaTime)
    {
        _animTimer += deltaTime;
        if (_animTimer >= _animFrameDuration)
        {
            _animTimer -= _animFrameDuration;
            _animFrame++;
        }
    }

    public static void DrawMap(
        SpriteBatch spriteBatch,
        TileMap map,
        Systems.Camera camera,
        int tileSize = 32)
    {
        var bounds = camera.Bounds;
        int startX = Math.Max(0, bounds.Left / tileSize - 4);
        int startY = Math.Max(0, bounds.Top / tileSize - 4);
        int endX = Math.Min(map.Width, (bounds.Right / tileSize) + 5);
        int endY = Math.Min(map.Height, (bounds.Bottom / tileSize) + 5);

        for (int x = startX; x < endX; x++)
            for (int y = startY; y < endY; y++)
                DrawTile(spriteBatch, map.GetBaseTile(x, y), x, y, tileSize);

        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                int overlayId = map.GetOverlayTile(x, y);
                if (overlayId >= 0)
                    DrawTile(spriteBatch, overlayId, x, y, tileSize);
            }
        }
    }

    private static void DrawTile(
        SpriteBatch spriteBatch,
        int tileId,
        int tileX,
        int tileY,
        int tileSize)
    {
        var tile = TileRegistry.GetTile(tileId);
        if (tile == null)
            return;

        var texture = GetAnimatedFrame(tileId, tile.Name)
                      ?? TextureStore.GetTileTexture(tileId, tile.Name);

        if (texture == null)
        {
            var fallbackColor = TextureStore.ParseHexColor(tile.Color);
            texture = TextureStore.CreateColorTexture(fallbackColor);
        }

        int worldX = tileX * tileSize;
        int worldY = tileY * tileSize;

        float scale = 1.0f;
        if (_visualScale.TryGetValue(tileId, out float visualScale))
        {
            scale = visualScale;
        }

        float textureScale = (float)tileSize / texture.Width * scale;
        int drawWidth = (int)(texture.Width * textureScale);
        int drawHeight = (int)(texture.Height * textureScale);
        int offsetX = (tileSize - drawWidth) / 2;
        int offsetY = tileSize - drawHeight;

        spriteBatch.Draw(
            texture,
            new Rectangle(worldX + offsetX, worldY + offsetY, drawWidth, drawHeight),
            null,
            Color.White);
    }

    private static Texture2D? GetAnimatedFrame(int tileId, string tileName)
    {
        if (_animatedTiles.TryGetValue(tileId, out var frames))
            return frames[_animFrame % frames.Length];

        var baseName = TileNameToSpriteBase(tileName);
        var frameList = new List<Texture2D>();

        for (int i = 0; ; i++)
        {
            var frame = TextureStore.GetAnimatedTileFrame(tileId, baseName, i);
            if (frame == null)
                break;
            frameList.Add(frame);
        }

        if (frameList.Count > 1)
        {
            var arr = frameList.ToArray();
            _animatedTiles[tileId] = arr;
            return arr[_animFrame % arr.Length];
        }

        return null;
    }

    private static string TileNameToSpriteBase(string name)
    {
        var result = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (char.IsUpper(c) && result.Length > 0)
                result.Append('_');
            result.Append(char.ToLower(c));
        }
        return $"tile_{result}";
    }

    public static void Clear()
    {
        _animatedTiles.Clear();
    }
}
