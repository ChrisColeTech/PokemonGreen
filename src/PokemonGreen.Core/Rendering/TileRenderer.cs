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

    public static void DrawBaseTiles(
        SpriteBatch spriteBatch,
        TileMap map,
        Systems.Camera camera,
        int tileSize = 32)
    {
        GetVisibleRange(map, camera, tileSize, out int startX, out int startY, out int endX, out int endY);

        for (int x = startX; x < endX; x++)
            for (int y = startY; y < endY; y++)
                DrawTile(spriteBatch, map.GetBaseTile(x, y), x, y, tileSize);
    }

    public static void DrawOverlaysBehindPlayer(
        SpriteBatch spriteBatch,
        TileMap map,
        Systems.Camera camera,
        int tileSize,
        int playerTileX,
        int playerTileY)
    {
        GetVisibleRange(map, camera, tileSize, out int startX, out int startY, out int endX, out int endY);
        int feetTileY = playerTileY + 1;

        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                int overlayId = map.GetOverlayTile(x, y);
                if (overlayId < 0)
                    continue;
                
                // Tall grass: skip player's feet tile (draws single layer in front)
                if (overlayId == 72 && x == playerTileX && y == feetTileY)
                    continue;
                
                // Other overlays: Y-sort (draw if tile is above player's feet)
                if (overlayId != 72 && y >= feetTileY)
                    continue;
                    
                DrawTile(spriteBatch, overlayId, x, y, tileSize, useSingleGrass: false);
            }
        }
    }

    public static void DrawOverlaysInFrontOfPlayer(
        SpriteBatch spriteBatch,
        TileMap map,
        Systems.Camera camera,
        int tileSize,
        int playerTileX,
        int playerTileY)
    {
        int feetTileY = playerTileY + 1;
        
        // Draw single grass at player's feet tile
        if (feetTileY < map.Height)
        {
            int feetOverlayId = map.GetOverlayTile(playerTileX, feetTileY);
            if (feetOverlayId == 72)
            {
                DrawTile(spriteBatch, feetOverlayId, playerTileX, feetTileY, tileSize, useSingleGrass: true);
            }
        }
        
        // Draw other overlays at/below player's feet (in front)
        GetVisibleRange(map, camera, tileSize, out int startX, out int startY, out int endX, out int endY);
        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                int overlayId = map.GetOverlayTile(x, y);
                if (overlayId < 0 || overlayId == 72)
                    continue;
                
                if (y >= feetTileY)
                    DrawTile(spriteBatch, overlayId, x, y, tileSize, useSingleGrass: false);
            }
        }
    }

    private static void GetVisibleRange(
        TileMap map,
        Systems.Camera camera,
        int tileSize,
        out int startX,
        out int startY,
        out int endX,
        out int endY)
    {
        var bounds = camera.Bounds;
        startX = Math.Max(0, bounds.Left / tileSize - 4);
        startY = Math.Max(0, bounds.Top / tileSize - 4);
        endX = Math.Min(map.Width, (bounds.Right / tileSize) + 5);
        endY = Math.Min(map.Height, (bounds.Bottom / tileSize) + 5);
    }

    private static void DrawTile(
        SpriteBatch spriteBatch,
        int tileId,
        int tileX,
        int tileY,
        int tileSize,
        bool useSingleGrass = false)
    {
        var tile = TileRegistry.GetTile(tileId);
        if (tile == null)
            return;

        Texture2D? texture;
        if (useSingleGrass && tileId == 72)
        {
            texture = GetSingleGrassFrame();
        }
        else
        {
            texture = GetAnimatedFrame(tileId, tile.Name)
                      ?? TextureStore.GetTileTexture(tileId, tile.Name);
        }

        if (texture == null)
        {
            var fallbackColor = TextureStore.ParseHexColor(tile.Color);
            texture = TextureStore.CreateColorTexture(fallbackColor);
        }

        int worldX = tileX * tileSize;
        int worldY = tileY * tileSize;

        // Tall grass: single layer at player's feet, double layer elsewhere
        if (tileId == 72)
        {
            if (useSingleGrass)
            {
                // Single layer at player's feet (draws in front of player)
                spriteBatch.Draw(
                    texture,
                    new Rectangle(worldX, worldY, tileSize, tileSize),
                    null,
                    Color.White);
            }
            else
            {
                // Double layer: draw twice with horizontal offset for density
                int halfTile = tileSize / 2;
                
                spriteBatch.Draw(
                    texture,
                    new Rectangle(worldX, worldY, tileSize, tileSize),
                    null,
                    Color.White);
                
                spriteBatch.Draw(
                    texture,
                    new Rectangle(worldX + halfTile, worldY - halfTile / 2, tileSize, tileSize),
                    null,
                    Color.White);
            }
            return;
        }

        // NPCs and Trainers (48-71)
        if (TileSpriteMapping.IsNPCTile(tileId))
        {
            var spriteName = TileSpriteMapping.GetSpriteName(tileId);
            if (spriteName != null)
            {
                var npcTexture = TextureStore.GetTexture(spriteName);
                if (npcTexture != null)
                {
                    spriteBatch.Draw(
                        npcTexture,
                        new Rectangle(worldX, worldY, tileSize, tileSize),
                        null,
                        Color.White);
                }
            }
            return;
        }

        // Items (96-111)
        if (TileSpriteMapping.IsItemTile(tileId))
        {
            var spriteName = TileSpriteMapping.GetSpriteName(tileId);
            if (spriteName != null)
            {
                var itemTexture = TextureStore.GetTexture(spriteName);
                if (itemTexture != null)
                {
                    int itemSize = tileSize / 2;
                    int itemOffset = itemSize / 2;
                    
                    spriteBatch.Draw(
                        itemTexture,
                        new Rectangle(worldX + itemOffset, worldY + itemOffset, itemSize, itemSize),
                        null,
                        Color.White);
                }
            }
            return;
        }

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

    private static Texture2D[]? _singleGrassFrames;
    
    private static Texture2D? GetSingleGrassFrame()
    {
        if (_singleGrassFrames != null)
            return _singleGrassFrames[_animFrame % _singleGrassFrames.Length];

        var frameList = new List<Texture2D>();
        for (int i = 0; ; i++)
        {
            var frame = TextureStore.GetTexture($"tile_tall_grass_single_{i}");
            if (frame == null)
                break;
            frameList.Add(frame);
        }

        if (frameList.Count > 0)
        {
            _singleGrassFrames = frameList.ToArray();
            return _singleGrassFrames[_animFrame % _singleGrassFrames.Length];
        }

        return TextureStore.GetTexture("tile_tall_grass_single");
    }

    public static void Clear()
    {
        _animatedTiles.Clear();
        _singleGrassFrames = null;
    }
}
