namespace PokemonGreen.Core.Maps;

/// <summary>
/// Represents a game map with base terrain and optional overlay layers.
/// </summary>
public class TileMap
{
    /// <summary>
    /// Width of the map in tiles.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Height of the map in tiles.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Base terrain layer containing ground tiles (grass, water, paths, etc.).
    /// </summary>
    public int[,] BaseTiles { get; }

    /// <summary>
    /// Optional overlay layer for decorations, items on terrain, etc.
    /// Null values or -1 indicate no overlay at that position.
    /// </summary>
    public int[,]? OverlayTiles { get; private set; }

    /// <summary>
    /// Creates a new TileMap with the specified dimensions.
    /// </summary>
    /// <param name="width">Width of the map in tiles.</param>
    /// <param name="height">Height of the map in tiles.</param>
    public TileMap(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

        Width = width;
        Height = height;
        BaseTiles = new int[width, height];
    }

    /// <summary>
    /// Gets the base tile ID at the specified position.
    /// </summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <returns>The tile ID at the position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when coordinates are out of bounds.</exception>
    public int GetBaseTile(int x, int y)
    {
        ValidateCoordinates(x, y);
        return BaseTiles[x, y];
    }

    /// <summary>
    /// Gets the overlay tile ID at the specified position.
    /// </summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <returns>The overlay tile ID, or -1 if no overlay exists at this position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when coordinates are out of bounds.</exception>
    public int GetOverlayTile(int x, int y)
    {
        ValidateCoordinates(x, y);
        return OverlayTiles?[x, y] ?? -1;
    }

    /// <summary>
    /// Sets the base tile at the specified position.
    /// </summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <param name="tileId">The tile ID to set.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when coordinates are out of bounds.</exception>
    public void SetBaseTile(int x, int y, int tileId)
    {
        ValidateCoordinates(x, y);
        BaseTiles[x, y] = tileId;
    }

    /// <summary>
    /// Sets the overlay tile at the specified position.
    /// </summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <param name="tileId">The tile ID to set. Use -1 to clear the overlay.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when coordinates are out of bounds.</exception>
    public void SetOverlayTile(int x, int y, int tileId)
    {
        ValidateCoordinates(x, y);

        // Lazily initialize overlay layer when first needed
        OverlayTiles ??= InitializeOverlayLayer();

        OverlayTiles[x, y] = tileId;
    }

    /// <summary>
    /// Checks if the tile at the specified position is walkable.
    /// A tile is walkable if both the base tile and overlay tile (if present) are walkable.
    /// </summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <returns>True if the position is walkable, false otherwise.</returns>
    public bool IsWalkable(int x, int y)
    {
        if (!IsInBounds(x, y))
            return false;

        // Check base tile walkability
        var baseTileId = BaseTiles[x, y];
        var baseTile = TileRegistry.GetTile(baseTileId);
        if (baseTile == null || !baseTile.Walkable)
            return false;

        // Check overlay tile walkability if present
        if (OverlayTiles != null)
        {
            var overlayTileId = OverlayTiles[x, y];
            if (overlayTileId >= 0)
            {
                var overlayTile = TileRegistry.GetTile(overlayTileId);
                if (overlayTile != null && !overlayTile.Walkable)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if the specified coordinates are within the map bounds.
    /// </summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <returns>True if the coordinates are within bounds, false otherwise.</returns>
    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    private void ValidateCoordinates(int x, int y)
    {
        if (!IsInBounds(x, y))
            throw new ArgumentOutOfRangeException(
                $"Coordinates ({x}, {y}) are out of bounds for map size ({Width}, {Height}).");
    }

    private int[,] InitializeOverlayLayer()
    {
        var overlay = new int[Width, Height];
        // Initialize all overlay tiles to -1 (no overlay)
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                overlay[x, y] = -1;
            }
        }
        return overlay;
    }
}
