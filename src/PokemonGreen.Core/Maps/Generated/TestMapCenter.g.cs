#nullable enable

namespace PokemonGreen.Core.Maps;

public sealed class TestMapCenter : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
        1, 1, 1, 1, 1, 23, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 72, 72, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 72, 1, 1, 1, 72, 1, 
        1, 1, 72, 72, 72, 1, 1, 1, 1, 1, 72, 1, 18, 1, 72, 1, 
        1, 1, 72, 72, 1, 1, 1, 22, 1, 1, 72, 72, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 72, 1, 1, 1, 23, 
        2, 1, 1, 1, 1, 1, 72, 18, 72, 1, 72, 72, 1, 1, 1, 2, 
        2, 1, 1, 1, 1, 1, 72, 72, 72, 72, 72, 72, 1, 1, 1, 2, 
        2, 1, 1, 1, 1, 72, 72, 72, 72, 72, 72, 18, 1, 1, 1, 2, 
        23, 1, 1, 1, 1, 18, 72, 72, 72, 72, 72, 1, 17, 1, 1, 2, 
        1, 1, 1, 16, 1, 1, 72, 18, 72, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 72, 72, 1, 1, 1, 
        1, 1, 18, 72, 72, 1, 1, 1, 1, 1, 1, 72, 72, 1, 1, 1, 
        1, 1, 72, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 23, 1, 1, 1, 1, 1
    ];

    private static readonly int?[] OverlayTileData =
    [
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null
    ];

    private static readonly int[] WalkableTileIds =
    [
        1, 2, 18, 72
    ];

    public static TestMapCenter Instance { get; } = new();

    private TestMapCenter()
        : base("test_map_center", "Test Map center", 16, 16, 16, BaseTileData, OverlayTileData, WalkableTileIds)
    {
    }
}
