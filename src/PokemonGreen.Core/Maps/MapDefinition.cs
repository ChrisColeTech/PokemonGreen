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
    private readonly WarpConnection[] _warps;
    private readonly MapConnection[] _connections;

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

    /// <summary>World grid X position. Maps at adjacent grid positions auto-connect.</summary>
    public int WorldX { get; }

    /// <summary>World grid Y position. Maps at adjacent grid positions auto-connect.</summary>
    public int WorldY { get; }

    /// <summary>Warp connections (doors, teleporters) defined on this map.</summary>
    public IReadOnlyList<WarpConnection> Warps => _warps;

    /// <summary>Edge connections to adjacent maps.</summary>
    public IReadOnlyList<MapConnection> Connections => _connections;

    /// <summary>
    /// Creates a MapDefinition from flat row-major tile arrays.
    /// Automatically registers this map in MapCatalog.
    /// </summary>
    protected MapDefinition(
        string id, string name,
        int width, int height, int tileSize,
        int[] baseTileData, int?[] overlayTileData, int[] walkableTileIds,
        WarpConnection[]? warps = null,
        MapConnection[]? connections = null,
        int worldX = 0, int worldY = 0)
    {
        Id = id;
        Name = name;
        Width = width;
        Height = height;
        TileSize = tileSize;
        WorldX = worldX;
        WorldY = worldY;
        _baseTileData = baseTileData;
        _overlayTileData = overlayTileData;
        _walkableTileIds = new HashSet<int>(walkableTileIds);
        _warps = warps ?? [];
        _connections = connections ?? [];

        MapCatalog.TryRegister(this);
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

    /// <summary>
    /// Gets the warp connection at the specified position with the given trigger, or null if none.
    /// </summary>
    public WarpConnection? GetWarp(int x, int y, WarpTrigger trigger)
    {
        foreach (var warp in _warps)
        {
            if (warp.X == x && warp.Y == y && warp.Trigger == trigger)
                return warp;
        }
        return null;
    }

    /// <summary>
    /// Gets the edge connection for the given direction, or null if none.
    /// </summary>
    public MapConnection? GetConnection(MapEdge edge)
    {
        foreach (var conn in _connections)
        {
            if (conn.Edge == edge)
                return conn;
        }
        return null;
    }
}
