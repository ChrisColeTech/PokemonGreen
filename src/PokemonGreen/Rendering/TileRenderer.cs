using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.Maps;
using PokemonGreen.Core.Systems;

namespace PokemonGreen.Rendering;

/// <summary>
/// Static class for rendering tile maps.
/// </summary>
public static class TileRenderer
{
    private static readonly Dictionary<int, Texture2D> _tileTextures = new();

    /// <summary>
    /// Draws the tile map using the camera's viewport.
    /// </summary>
    /// <param name="spriteBatch">The sprite batch to draw with.</param>
    /// <param name="map">The tile map to draw.</param>
    /// <param name="camera">The camera for viewport calculations.</param>
    /// <param name="graphicsDevice">The graphics device for creating textures.</param>
    /// <param name="tileSize">The size of each tile in pixels.</param>
    public static void DrawMap(
        SpriteBatch spriteBatch,
        TileMap map,
        Camera camera,
        GraphicsDevice graphicsDevice,
        int tileSize = 32)
    {
        // Calculate visible tile range
        var bounds = camera.Bounds;
        int startX = Math.Max(0, bounds.Left / tileSize);
        int startY = Math.Max(0, bounds.Top / tileSize);
        int endX = Math.Min(map.Width, (bounds.Right / tileSize) + 2);
        int endY = Math.Min(map.Height, (bounds.Bottom / tileSize) + 2);

        // Draw base layer
        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                int tileId = map.GetBaseTile(x, y);
                DrawTile(spriteBatch, tileId, x, y, camera, graphicsDevice, tileSize);
            }
        }

        // Draw overlay layer if present
        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                int overlayId = map.GetOverlayTile(x, y);
                if (overlayId >= 0)
                {
                    DrawTile(spriteBatch, overlayId, x, y, camera, graphicsDevice, tileSize);
                }
            }
        }
    }

    /// <summary>
    /// Draws a single tile at the specified position.
    /// </summary>
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

        // Get or create texture for this tile
        var texture = GetTileTexture(tileId, tile.Color, graphicsDevice);

        // Convert world position to screen position
        float worldX = tileX * tileSize;
        float worldY = tileY * tileSize;
        var (screenX, screenY) = camera.WorldToScreen(worldX, worldY);

        // Calculate scaled tile size based on camera zoom
        int scaledSize = (int)(tileSize * camera.Zoom);

        // Draw the tile
        spriteBatch.Draw(
            texture,
            new Rectangle(screenX, screenY, scaledSize, scaledSize),
            Color.White);
    }

    /// <summary>
    /// Gets or creates a texture for the specified tile.
    /// </summary>
    private static Texture2D GetTileTexture(int tileId, string hexColor, GraphicsDevice graphicsDevice)
    {
        if (_tileTextures.TryGetValue(tileId, out var cached))
            return cached;

        var color = TextureStore.ParseHexColor(hexColor);
        var texture = TextureStore.CreateColorTexture(graphicsDevice, color);
        _tileTextures[tileId] = texture;
        return texture;
    }

    /// <summary>
    /// Clears the tile texture cache.
    /// </summary>
    public static void Clear()
    {
        _tileTextures.Clear();
    }
}
