using System;
using System.Collections.Generic;
using System.Linq;

namespace PokemonGreen.Core.Maps;

public abstract class MapDefinition
{
#if DEBUG
    private static readonly TileRenderCatalog DebugTileRenderCatalog = new();
#endif

    private readonly int[] _baseTileData;
    private readonly int?[] _overlayTileData;
    private readonly int[] _walkableTileIds;

    public string MapId { get; }
    public string DisplayName { get; }
    public int Width { get; }
    public int Height { get; }
    public int TileSize { get; }

    protected MapDefinition(
        string mapId,
        string displayName,
        int width,
        int height,
        int tileSize,
        int[] baseTileData,
        int?[] overlayTileData,
        int[] walkableTileIds)
    {
        if (string.IsNullOrWhiteSpace(mapId))
        {
            throw new ArgumentException("Map id is required.", nameof(mapId));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        }

        if (tileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize), "Tile size must be positive.");
        }

        if (baseTileData.Length != width * height)
        {
            throw new ArgumentException("Base tile count must match width * height.", nameof(baseTileData));
        }

        if (overlayTileData.Length != width * height)
        {
            throw new ArgumentException("Overlay tile count must match width * height.", nameof(overlayTileData));
        }

        MapId = mapId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? mapId : displayName;
        Width = width;
        Height = height;
        TileSize = tileSize;
        _baseTileData = baseTileData;
        _overlayTileData = overlayTileData;
        _walkableTileIds = walkableTileIds.Distinct().ToArray();

#if DEBUG
        ValidateRenderableTileCoverage(mapId, _baseTileData, _overlayTileData);
#endif
    }

    public int GetBaseTileId(int x, int y)
    {
        if (x < 0 || x >= Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "x is out of bounds.");
        }

        if (y < 0 || y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y), "y is out of bounds.");
        }

        return _baseTileData[(y * Width) + x];
    }

    public int? GetOverlayTileId(int x, int y)
    {
        if (x < 0 || x >= Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "x is out of bounds.");
        }

        if (y < 0 || y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y), "y is out of bounds.");
        }

        return _overlayTileData[(y * Width) + x];
    }

    public IReadOnlyCollection<int> GetWalkableTileIds() => _walkableTileIds;

    public (int[,] BaseTiles, int?[,] OverlayTiles) CreateTileGrids()
    {
        var baseTiles = new int[Height, Width];
        var overlayTiles = new int?[Height, Width];
        
        for (var y = 0; y < Height; y += 1)
        {
            for (var x = 0; x < Width; x += 1)
            {
                var index = (y * Width) + x;
                baseTiles[y, x] = _baseTileData[index];
                overlayTiles[y, x] = _overlayTileData[index];
            }
        }

        return (baseTiles, overlayTiles);
    }

    public TileMap CreateTileMap()
    {
        var (baseTiles, overlayTiles) = CreateTileGrids();
        return new TileMap(baseTiles, overlayTiles, TileSize, _walkableTileIds);
    }

#if DEBUG
    private static void ValidateRenderableTileCoverage(string mapId, IReadOnlyCollection<int> baseTileData, IReadOnlyCollection<int?> overlayTileData)
    {
        var unmappedBaseTileIds = baseTileData
            .Distinct()
            .Where(tileId => !DebugTileRenderCatalog.TryGetRule(tileId, out _))
            .OrderBy(tileId => tileId)
            .ToArray();

        if (unmappedBaseTileIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Map '{mapId}' contains base tile ids with no runtime render mapping: {string.Join(", ", unmappedBaseTileIds)}.");
        }

        var unmappedOverlayTileIds = overlayTileData
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .Distinct()
            .Where(tileId => !DebugTileRenderCatalog.TryGetRule(tileId, out _))
            .OrderBy(tileId => tileId)
            .ToArray();

        if (unmappedOverlayTileIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Map '{mapId}' contains overlay tile ids with no runtime render mapping: {string.Join(", ", unmappedOverlayTileIds)}.");
        }
    }
#endif
}
