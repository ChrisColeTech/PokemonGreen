#nullable enable

namespace PokemonGreen.Core.Maps;

public sealed class TestMapA : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
        80, 80, 80, 80, 80, 80, 80, 80, 
        80, 1, 1, 1, 1, 1, 1, 80, 
        80, 1, 1, 1, 1, 1, 1, 80, 
        80, 1, 1, 1, 1, 1, 1, 80, 
        80, 1, 1, 1, 1, 1, 1, 80, 
        80, 1, 1, 1, 1, 1, 1, 80, 
        80, 1, 1, 1, 1, 1, 1, 80, 
        80, 1, 1, 2, 2, 1, 1, 80
    ];

    private static readonly int?[] OverlayTileData =
    [
        null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null
    ];

    private static readonly int[] WalkableTileIds =
    [
        1, 2
    ];

    private static readonly MapConnection[] ConnectionData =
    [
        new(MapEdge.South, "test_map_b", 0),
    ];

    public static TestMapA Instance { get; } = new();

    private TestMapA()
        : base("test_map_a", "Test Map A", 8, 8, 16, BaseTileData, OverlayTileData, WalkableTileIds, null, ConnectionData)
    {
    }
}
