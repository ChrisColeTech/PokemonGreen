#nullable enable

namespace PokemonGreen.Core.Maps;

/// <summary>
/// Abstract base class for generated map definitions.
/// Generated subclasses store tile data as flat row-major arrays
/// and pass them to the protected constructor.
/// </summary>
public abstract class MapDefinition
{
    private readonly int[] _baseTileData;
    private readonly int?[] _overlayTileData;
    private readonly HashSet<int> _walkableTileIds;

    /// <summary>Unique identifier for this map (e.g., "pallet_town").</summary>
    public string Id { get; }

    /// <summary>Display name for this map (e.g., "Pallet Town").</summary>
    public string Name { get; }

    /// <summary>Width of the map in tiles.</summary>
    public int Width { get; }

    /// <summary>Height of the map in tiles.</summary>
    public int Height { get; }

    /// <summary>Tile size in pixels used by the runtime renderer.</summary>
    public int TileSize { get; }

    /// <summary>
    /// Creates a MapDefinition from flat row-major tile arrays.
    /// </summary>
    /// <param name="id">Map identifier (snake_case).</param>
    /// <param name="name">Display name.</param>
    /// <param name="width">Width in tiles.</param>
    /// <param name="height">Height in tiles.</param>
    /// <param name="tileSize">Tile size in pixels.</param>
    /// <param name="baseTileData">Flat row-major base tile array (width * height elements).</param>
    /// <param name="overlayTileData">Flat row-major overlay tile array (null = no overlay).</param>
    /// <param name="walkableTileIds">Set of tile IDs that are walkable in this map.</param>
    protected MapDefinition(
        string id, string name,
        int width, int height, int tileSize,
        int[] baseTileData, int?[] overlayTileData, int[] walkableTileIds)
    {
        Id = id;
        Name = name;
        Width = width;
        Height = height;
        TileSize = tileSize;
        _baseTileData = baseTileData;
        _overlayTileData = overlayTileData;
        _walkableTileIds = new HashSet<int>(walkableTileIds);
    }

    /// <summary>
    /// Creates and populates a TileMap from the stored flat arrays.
    /// </summary>
    public TileMap CreateTileMap()
    {
        var map = new TileMap(Width, Height);

        for (int i = 0; i < _baseTileData.Length; i++)
        {
            int x = i % Width;
            int y = i / Width;
            map.SetBaseTile(x, y, _baseTileData[i]);
        }

        for (int i = 0; i < _overlayTileData.Length; i++)
        {
            if (_overlayTileData[i] is int tileId)
            {
                int x = i % Width;
                int y = i / Width;
                map.SetOverlayTile(x, y, tileId);
            }
        }

        return map;
    }

    /// <summary>
    /// Gets the base tile ID at position (x, y).
    /// </summary>
    public int GetBaseTile(int x, int y) => _baseTileData[y * Width + x];

    /// <summary>
    /// Gets the overlay tile ID at position (x, y), or null if no overlay.
    /// </summary>
    public int? GetOverlayTile(int x, int y) => _overlayTileData[y * Width + x];

    /// <summary>
    /// Checks if a tile ID is walkable according to this map's walkable set.
    /// </summary>
    public bool IsWalkableTile(int tileId) => _walkableTileIds.Contains(tileId);
}
