using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core.Graphics;
using PokemonGreen.Core.Maps;

namespace PokemonGreen.Core.Rendering;

public class TileRenderer
{
    private readonly TileMap _tileMap;
    private readonly TileRenderCatalog _tileRenderCatalog;
    private readonly TextureStore _textures;

    public TileRenderer(TileMap tileMap, TileRenderCatalog tileRenderCatalog, TextureStore textures)
    {
        _tileMap = tileMap;
        _tileRenderCatalog = tileRenderCatalog;
        _textures = textures;
    }

    public void DrawBaseTiles(SpriteBatch spriteBatch)
    {
        for (var y = 0; y < _tileMap.Height; y++)
        {
            for (var x = 0; x < _tileMap.Width; x++)
            {
                var tileId = _tileMap.BaseTiles[y, x];
                if (!_tileRenderCatalog.TryGetRule(tileId, out var renderRule))
                {
                    continue;
                }

                var destination = CreateTileRectangle(x, y);
                var texture = GetTextureForVisualKind(renderRule.VisualKind);
                spriteBatch.Draw(texture, destination, new Color(renderRule.Red, renderRule.Green, renderRule.Blue));

                if (renderRule.IsTemporaryVisual)
                {
                    DrawTemporaryMarker(spriteBatch, destination, tileId);
                }
            }
        }
    }

    public void DrawOverlayTiles(SpriteBatch spriteBatch, int waterFrameIndex)
    {
        for (var y = 0; y < _tileMap.Height; y++)
        {
            for (var x = 0; x < _tileMap.Width; x++)
            {
                var tileId = _tileMap.OverlayTiles[y, x];
                if (!tileId.HasValue)
                {
                    continue;
                }

                if (!_tileRenderCatalog.TryGetRule(tileId.Value, out var renderRule))
                {
                    DrawUnknownTileFallback(spriteBatch, CreateTileRectangle(x, y));
                    continue;
                }

                DrawOverlayTile(spriteBatch, CreateTileRectangle(x, y), tileId.Value, renderRule, waterFrameIndex);
            }
        }
    }

    private void DrawOverlayTile(SpriteBatch spriteBatch, Rectangle destination, int tileId, TileRenderRule renderRule, int waterFrameIndex)
    {
        if (renderRule.VisualKind == TileVisualKind.Item)
        {
            DrawItem(spriteBatch, destination, tileId, renderRule);
            return;
        }

        var texture = GetTextureForVisualKind(renderRule.VisualKind, waterFrameIndex);

        if (renderRule.VisualKind == TileVisualKind.Tree)
        {
            var largeSize = _tileMap.TileSize * 2;
            var offset = _tileMap.TileSize / 2;
            var largeDest = new Rectangle(
                destination.X - offset,
                destination.Y - offset,
                largeSize,
                largeSize);
            spriteBatch.Draw(texture, largeDest, new Color(renderRule.Red, renderRule.Green, renderRule.Blue));
        }
        else if (renderRule.VisualKind == TileVisualKind.Flower)
        {
            var flowerSize = (int)(_tileMap.TileSize * 1.1f);
            var offset = (_tileMap.TileSize - flowerSize) / 2;
            var flowerDest = new Rectangle(
                destination.X + offset,
                destination.Y + offset,
                flowerSize,
                flowerSize);
            spriteBatch.Draw(texture, flowerDest, new Color(renderRule.Red, renderRule.Green, renderRule.Blue));
        }
        else
        {
            spriteBatch.Draw(texture, destination, new Color(renderRule.Red, renderRule.Green, renderRule.Blue));
        }

        if (renderRule.IsTemporaryVisual)
        {
            DrawTemporaryMarker(spriteBatch, destination, tileId);
        }
    }

    private void DrawItem(SpriteBatch spriteBatch, Rectangle destination, int tileId, TileRenderRule renderRule)
    {
        if (!_textures.Items.TryGetValue(tileId, out var texture))
        {
            texture = _textures.Entity;
        }

        var itemSize = _tileMap.TileSize;
        var itemDest = new Rectangle(
            destination.X + (destination.Width - itemSize) / 2,
            destination.Y + (destination.Height - itemSize) / 2,
            itemSize,
            itemSize);

        spriteBatch.Draw(texture, itemDest, Color.White);
    }

    public void DrawOverlayMarkers(SpriteBatch spriteBatch)
    {
        for (var y = 0; y < _tileMap.Height; y++)
        {
            for (var x = 0; x < _tileMap.Width; x++)
            {
                var overlayTileId = _tileMap.OverlayTiles[y, x];
                if (!overlayTileId.HasValue)
                {
                    continue;
                }

                if (!_tileRenderCatalog.TryGetRule(overlayTileId.Value, out var renderRule))
                {
                    continue;
                }

                if (renderRule.OverlayKind == TileOverlayKind.None)
                {
                    continue;
                }

                DrawOverlayPattern(spriteBatch, CreateTileRectangle(x, y), renderRule.OverlayKind, new Color(renderRule.Red, renderRule.Green, renderRule.Blue));
            }
        }
    }

    private Texture2D GetTextureForVisualKind(TileVisualKind visualKind, int waterFrameIndex = 0)
    {
        return visualKind switch
        {
            TileVisualKind.AnimatedWater => _textures.WaterFrames[waterFrameIndex],
            TileVisualKind.Grass => _textures.Grass,
            TileVisualKind.Path => _textures.Path,
            TileVisualKind.Tree => _textures.Tree,
            TileVisualKind.Rock => _textures.Rock,
            TileVisualKind.Flower => _textures.Flower,
            TileVisualKind.InteractiveObject => _textures.Interactive,
            TileVisualKind.EntityMarker => _textures.Entity,
            TileVisualKind.TrainerMarker => _textures.Trainer,
            TileVisualKind.Statue => _textures.Statue,
            TileVisualKind.Solid => _textures.Pixel,
            _ => _textures.Pixel,
        };
    }

    private Rectangle CreateTileRectangle(int tileX, int tileY)
    {
        return new Rectangle(
            tileX * _tileMap.TileSize,
            tileY * _tileMap.TileSize,
            _tileMap.TileSize,
            _tileMap.TileSize);
    }

    private void DrawOverlayPattern(SpriteBatch spriteBatch, Rectangle destination, TileOverlayKind overlayKind, Color color)
    {
        if (!OverlayPatterns.Patterns.TryGetValue(overlayKind, out var patternRows))
        {
            return;
        }

        var cellSize = Math.Max(1, destination.Width / 8);
        var contentWidth = 5 * cellSize;
        var contentHeight = 5 * cellSize;
        var startX = destination.X + (destination.Width - contentWidth) / 2;
        var startY = destination.Y + (destination.Height - contentHeight) / 2;

        var background = Color.Black * 0.45f;
        spriteBatch.Draw(
            _textures.Pixel,
            new Rectangle(startX - 1, startY - 1, contentWidth + 2, contentHeight + 2),
            background);

        for (var row = 0; row < patternRows.Length; row++)
        {
            var rowPattern = patternRows[row];
            for (var column = 0; column < rowPattern.Length; column++)
            {
                if (rowPattern[column] != '1')
                {
                    continue;
                }

                spriteBatch.Draw(
                    _textures.Pixel,
                    new Rectangle(startX + (column * cellSize), startY + (row * cellSize), cellSize, cellSize),
                    color);
            }
        }
    }

    private void DrawTemporaryMarker(SpriteBatch spriteBatch, Rectangle destination, int tileId)
    {
        var markerSize = Math.Max(4, destination.Width / 5);
        var markerColor = CreateMarkerColor(tileId);

        spriteBatch.Draw(
            _textures.Pixel,
            new Rectangle(destination.Right - markerSize, destination.Top, markerSize, markerSize),
            markerColor);
    }

    private void DrawUnknownTileFallback(SpriteBatch spriteBatch, Rectangle destination)
    {
        var halfWidth = Math.Max(1, destination.Width / 2);
        var halfHeight = Math.Max(1, destination.Height / 2);
        var magenta = new Color(255, 0, 255);
        var yellow = new Color(255, 255, 0);

        spriteBatch.Draw(_textures.Pixel, new Rectangle(destination.Left, destination.Top, halfWidth, halfHeight), magenta);
        spriteBatch.Draw(_textures.Pixel, new Rectangle(destination.Left + halfWidth, destination.Top, destination.Width - halfWidth, halfHeight), yellow);
        spriteBatch.Draw(_textures.Pixel, new Rectangle(destination.Left, destination.Top + halfHeight, halfWidth, destination.Height - halfHeight), yellow);
        spriteBatch.Draw(_textures.Pixel, new Rectangle(destination.Left + halfWidth, destination.Top + halfHeight, destination.Width - halfWidth, destination.Height - halfHeight), magenta);

        spriteBatch.Draw(_textures.Pixel, new Rectangle(destination.Left, destination.Top, destination.Width, 1), Color.Black);
        spriteBatch.Draw(_textures.Pixel, new Rectangle(destination.Left, destination.Bottom - 1, destination.Width, 1), Color.Black);
        spriteBatch.Draw(_textures.Pixel, new Rectangle(destination.Left, destination.Top, 1, destination.Height), Color.Black);
        spriteBatch.Draw(_textures.Pixel, new Rectangle(destination.Right - 1, destination.Top, 1, destination.Height), Color.Black);
    }

    private static Color CreateMarkerColor(int tileId)
    {
        var red = (byte)((tileId * 73 + 41) % 256);
        var green = (byte)((tileId * 151 + 19) % 256);
        var blue = (byte)((tileId * 199 + 97) % 256);
        return new Color(red, green, blue);
    }
}