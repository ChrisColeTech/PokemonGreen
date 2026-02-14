#nullable enable

namespace PokemonGreen.Core.Maps;

public sealed class TestMapC : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
        0, 0, 80, 1, 1, 23, 2, 2, 2, 2, 1, 1, 1, 80, 0, 0, 
        0, 0, 80, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 0, 
        0, 0, 80, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 0, 
        0, 0, 80, 1, 16, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 0, 
        0, 0, 80, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 0, 
        0, 0, 80, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 0, 
        0, 0, 80, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 0, 
        0, 0, 80, 1, 1, 1, 1, 1, 1, 1, 1, 22, 1, 80, 0, 0, 
        0, 0, 80, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 0, 
        0, 0, 80, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 0, 
        0, 0, 80, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 0, 
        0, 0, 80, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 0, 
        0, 0, 80, 1, 1, 17, 1, 1, 1, 1, 1, 1, 18, 80, 0, 0, 
        0, 0, 80, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 0, 
        0, 0, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 0, 0, 
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
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
        1, 2, 18
    ];

    public static TestMapC Instance { get; } = new();

    private TestMapC()
        : base("test_map_c", "Test Map C", 16, 16, 16, BaseTileData, OverlayTileData, WalkableTileIds, null, null, 0, 1)
    {
    }
}
