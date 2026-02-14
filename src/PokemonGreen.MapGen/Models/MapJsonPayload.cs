using PokemonGreen.Core.Maps;

namespace PokemonGreen.MapGen.Models;

public sealed class MapJsonPayload
{
    public required int SchemaVersion { get; init; }
    public required string MapId { get; init; }
    public required string DisplayName { get; init; }
    public required int TileSize { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int[][] BaseTiles { get; init; }
    public required int?[][] OverlayTiles { get; init; }
    public required IReadOnlyDictionary<int, TileTypeExport> TileTypes { get; init; }
}

public sealed record TileTypeExport(
    string Name,
    bool Walkable,
    string Category
)
{
    public static TileTypeExport FromDefinition(TileDefinition tile)
        => new(tile.Name, tile.Walkable, tile.Category.ToString().ToLowerInvariant());
}
