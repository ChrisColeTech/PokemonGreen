namespace PokemonGreen.Core.Maps;

/// <summary>
/// Abstract base class for generated map definitions.
/// Subclasses define specific maps with their layouts and properties.
/// </summary>
public abstract class MapDefinition
{
    /// <summary>
    /// Unique identifier for this map (e.g., "pallet_town", "route_1").
    /// </summary>
    public abstract string Id { get; }

    /// <summary>
    /// Display name for this map (e.g., "Pallet Town", "Route 1").
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Width of the map in tiles.
    /// </summary>
    public abstract int Width { get; }

    /// <summary>
    /// Height of the map in tiles.
    /// </summary>
    public abstract int Height { get; }

    /// <summary>
    /// Creates and populates the TileMap for this map definition.
    /// </summary>
    /// <returns>A fully populated TileMap instance.</returns>
    public abstract TileMap CreateTileMap();

    /// <summary>
    /// Helper method to fill a rectangular region with a specific base tile.
    /// </summary>
    /// <param name="map">The TileMap to modify.</param>
    /// <param name="tileId">The tile ID to fill with.</param>
    /// <param name="startX">Starting X coordinate.</param>
    /// <param name="startY">Starting Y coordinate.</param>
    /// <param name="width">Width of the region.</param>
    /// <param name="height">Height of the region.</param>
    protected static void FillBaseTiles(TileMap map, int tileId, int startX, int startY, int width, int height)
    {
        for (int x = startX; x < startX + width && x < map.Width; x++)
        {
            for (int y = startY; y < startY + height && y < map.Height; y++)
            {
                if (map.IsInBounds(x, y))
                    map.SetBaseTile(x, y, tileId);
            }
        }
    }

    /// <summary>
    /// Helper method to fill a rectangular region with a specific overlay tile.
    /// </summary>
    /// <param name="map">The TileMap to modify.</param>
    /// <param name="tileId">The tile ID to fill with.</param>
    /// <param name="startX">Starting X coordinate.</param>
    /// <param name="startY">Starting Y coordinate.</param>
    /// <param name="width">Width of the region.</param>
    /// <param name="height">Height of the region.</param>
    protected static void FillOverlayTiles(TileMap map, int tileId, int startX, int startY, int width, int height)
    {
        for (int x = startX; x < startX + width && x < map.Width; x++)
        {
            for (int y = startY; y < startY + height && y < map.Height; y++)
            {
                if (map.IsInBounds(x, y))
                    map.SetOverlayTile(x, y, tileId);
            }
        }
    }

    /// <summary>
    /// Helper method to fill the entire map with a base tile.
    /// </summary>
    /// <param name="map">The TileMap to modify.</param>
    /// <param name="tileId">The tile ID to fill with.</param>
    protected static void FillAllBaseTiles(TileMap map, int tileId)
    {
        FillBaseTiles(map, tileId, 0, 0, map.Width, map.Height);
    }

    /// <summary>
    /// Helper method to place a horizontal line of base tiles.
    /// </summary>
    /// <param name="map">The TileMap to modify.</param>
    /// <param name="tileId">The tile ID to place.</param>
    /// <param name="startX">Starting X coordinate.</param>
    /// <param name="y">Y coordinate of the line.</param>
    /// <param name="length">Length of the line.</param>
    protected static void PlaceHorizontalBaseLine(TileMap map, int tileId, int startX, int y, int length)
    {
        FillBaseTiles(map, tileId, startX, y, length, 1);
    }

    /// <summary>
    /// Helper method to place a vertical line of base tiles.
    /// </summary>
    /// <param name="map">The TileMap to modify.</param>
    /// <param name="tileId">The tile ID to place.</param>
    /// <param name="x">X coordinate of the line.</param>
    /// <param name="startY">Starting Y coordinate.</param>
    /// <param name="length">Length of the line.</param>
    protected static void PlaceVerticalBaseLine(TileMap map, int tileId, int x, int startY, int length)
    {
        FillBaseTiles(map, tileId, x, startY, 1, length);
    }

    /// <summary>
    /// Helper method to place a rectangular border of base tiles.
    /// </summary>
    /// <param name="map">The TileMap to modify.</param>
    /// <param name="tileId">The tile ID for the border.</param>
    /// <param name="startX">Starting X coordinate.</param>
    /// <param name="startY">Starting Y coordinate.</param>
    /// <param name="width">Width of the bordered region.</param>
    /// <param name="height">Height of the bordered region.</param>
    protected static void PlaceBaseBorder(TileMap map, int tileId, int startX, int startY, int width, int height)
    {
        // Top and bottom edges
        PlaceHorizontalBaseLine(map, tileId, startX, startY, width);
        PlaceHorizontalBaseLine(map, tileId, startX, startY + height - 1, width);

        // Left and right edges (excluding corners already placed)
        PlaceVerticalBaseLine(map, tileId, startX, startY + 1, height - 2);
        PlaceVerticalBaseLine(map, tileId, startX + width - 1, startY + 1, height - 2);
    }
}
