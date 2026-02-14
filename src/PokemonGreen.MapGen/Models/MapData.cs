namespace PokemonGreen.MapGen.Models;

public sealed record MapData(
    string MapId,
    string DisplayName,
    int Width,
    int Height,
    int TileSize,
    int[] BaseTiles,
    int?[] OverlayTiles,
    int[] WalkableTileIds
);
