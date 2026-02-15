using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.Maps;

namespace PokemonGreen.Core.Rendering;

public static class TileRenderer
{
    private static readonly Dictionary<int, AnimatedSprite> _tileSprites = new();
    private static float _animTimer;
    private static readonly float _animFrameDuration = 0.3f;
    private static int _animFrame;
    private static AnimatedSprite? _singleGrassSprite;
    private static AnimatedSprite? _singleFlamesSprite;

    private static readonly Dictionary<int, float> _visualScale = new()
    {
        [16] = 1.8f,
        [17] = 1.2f,
        [20] = 1.4f,
        [22] = 1.3f,
        [18] = 0.55f,
        [19] = 1.5f,
        [123] = 1.2f,  // FireRock - same as Rock
        [124] = 1.3f,  // FireBoulder - same as Boulder
    };

    private static readonly Dictionary<int, (int x, int y)> _visualOffset = new()
    {
        [16] = (8, 4),
        [17] = (8, 6),
        [22] = (5, 8),
        [20] = (5, 6),
        [19] = (5, 8),
        [123] = (8, 6),  // FireRock - same as Rock
        [124] = (5, 8),  // FireBoulder - same as Boulder
    };

    private static readonly HashSet<int> _scaledDecoration = new() { 16, 17, 19, 20, 22, 123, 124 };

    public static void Update(float deltaTime)
    {
        _animTimer += deltaTime;
        if (_animTimer >= _animFrameDuration)
        {
            _animTimer -= _animFrameDuration;
            _animFrame++;
        }
        AnimatedSpriteCache.UpdateAll(deltaTime);
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
                
                // Tall grass or flames at player's feet: draw full behind player
                if ((overlayId == 72 || overlayId == 121) && x == playerTileX && y == feetTileY)
                {
                    DrawTile(spriteBatch, overlayId, x, y, tileSize, useSingleGrass: false);
                    continue;
                }
                
                // Other overlays: Y-sort (draw if tile is above player's feet)
                if (overlayId != 72 && overlayId != 121 && y >= feetTileY)
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
        
        // Draw single grass/flames at player's feet tile
        if (feetTileY < map.Height)
        {
            int feetOverlayId = map.GetOverlayTile(playerTileX, feetTileY);
            if (feetOverlayId == 72 || feetOverlayId == 121)
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
                if (overlayId < 0 || overlayId == 72 || overlayId == 121)
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
        else if (useSingleGrass && tileId == 121)
        {
            texture = GetSingleFlamesFrame();           
        }
        else
        {
            texture = GetAnimatedFrame(tileId, tile)
                      ?? TextureStore.GetTileTexture(tileId, tile.Name);
        }

        if (texture == null)
        {
            var fallbackColor = TextureStore.ParseHexColor(tile.Color);
            texture = TextureStore.CreateColorTexture(fallbackColor);
        }
 
   
        int worldX = tileX * tileSize;
        int worldY = tileY * tileSize;
        int objSize = tileSize - tileSize/2;
    
        // Tall grass or flames: single layer at player's feet, double layer elsewhere
        if (tileId == 72 || tileId == 121)
        {
            if (useSingleGrass)
            {
                int halfTile = tileSize / 2;

                // Single layer in front of player
                spriteBatch.Draw(
                    texture,
                    new Rectangle(worldX + halfTile, worldY, objSize, objSize),
                    null,
                    Color.White);
            }
            else
            {
                // Double layer: draw twice with horizontal offset for density
                int halfTile = tileSize / 2;
                
                spriteBatch.Draw(
                    texture,
                    new Rectangle(worldX, worldY, objSize, objSize),
                    null,
                    Color.White);
                
                spriteBatch.Draw(
                    texture,
                    new Rectangle(worldX + halfTile, worldY - halfTile / 2, objSize, objSize),
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
        int drawWidth = (int)MathF.Round(texture.Width * textureScale);
        int drawHeight = (int)MathF.Round(texture.Height * textureScale);

        int offsetX, offsetY;
        if (_scaledDecoration.Contains(tileId))
        {
            offsetX = (tileSize - drawWidth + 1) / 2;
            offsetY = (tileSize - drawHeight + 1) / 2;
        }
        else
        {
            offsetX = (tileSize - drawWidth) / 2;
            offsetY = tileSize - drawHeight;
        }

        if (_visualOffset.TryGetValue(tileId, out var offset))
        {
            offsetX += offset.x;
            offsetY += offset.y;
        }

        spriteBatch.Draw(
            texture,
            new Rectangle(worldX + offsetX, worldY + offsetY, drawWidth, drawHeight),
            null,
            Color.White);
    }

    private static Texture2D? GetAnimatedFrame(int tileId, TileDefinition tile)
    {
        var spriteName = tile.GetSpriteName();
        
        if (_tileSprites.TryGetValue(tileId, out var cached))
            return cached.GetCurrentFrame();
        
        var sprite = AnimatedSpriteCache.GetOrLoad(spriteName);
        if (sprite != null)
        {
            _tileSprites[tileId] = sprite;
            return sprite.GetCurrentFrame();
        }
        
        return TextureStore.GetTileTexture(tileId, tile.Name);
    }

    private static Texture2D? GetSingleGrassFrame()
    {
        if (_singleGrassSprite != null)
            return _singleGrassSprite.GetCurrentFrame();

        _singleGrassSprite = AnimatedSpriteCache.GetOrLoad("tile_tall_grass_single");
        return _singleGrassSprite?.GetCurrentFrame() ?? TextureStore.GetTexture("tile_tall_grass_single");
    }

    private static Texture2D? GetSingleFlamesFrame()
    {
        if (_singleFlamesSprite != null)
            return _singleFlamesSprite.GetCurrentFrame();

        _singleFlamesSprite = AnimatedSpriteCache.GetOrLoad("tile_flames_single");
        return _singleFlamesSprite?.GetCurrentFrame() ?? TextureStore.GetTexture("tile_flames_single");
    }

    public static void Clear()
    {
        _tileSprites.Clear();
        _singleGrassSprite = null;
        _singleFlamesSprite = null;
        AnimatedSpriteCache.Clear();
    }
}
