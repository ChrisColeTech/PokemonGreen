using System.Collections.Generic;

namespace PokemonGreen.Core;

public class TileMap
{
    private readonly HashSet<int>? _walkableTileIds;

    public int TileSize { get; }
    public int[,] BaseTiles { get; }
    public int?[,] OverlayTiles { get; }
    public int Width => BaseTiles.GetLength(1);
    public int Height => BaseTiles.GetLength(0);

    public TileMap(int[,] baseTiles, int?[,] overlayTiles, int tileSize = 32, IEnumerable<int>? walkableTileIds = null)
    {
        BaseTiles = baseTiles;
        OverlayTiles = overlayTiles;
        TileSize = tileSize;
        if (walkableTileIds is not null)
        {
            _walkableTileIds = new HashSet<int>(walkableTileIds);
        }
    }

    public bool IsWalkable(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return false;

        var overlayTile = OverlayTiles[y, x];
        if (overlayTile.HasValue && _walkableTileIds is not null)
        {
            return _walkableTileIds.Contains(overlayTile.Value);
        }
        
        if (_walkableTileIds is null)
        {
            return BaseTiles[y, x] != 0;
        }

        return _walkableTileIds.Contains(BaseTiles[y, x]);
    }
}
