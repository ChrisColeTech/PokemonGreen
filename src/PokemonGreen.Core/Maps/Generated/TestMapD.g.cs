#nullable enable

namespace PokemonGreen.Core.Maps;

public sealed class TestMapD : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
        80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 0, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 18, 1, 80, 0, 
        1, 1, 1, 1, 1, 1, 1, 22, 1, 1, 1, 1, 1, 1, 80, 0, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 
        2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 
        2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 
        2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 
        2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 
        23, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 17, 1, 80, 0, 
        1, 1, 1, 16, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 80, 0, 
        80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 0, 
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
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

    public static TestMapD Instance { get; } = new();

    private TestMapD()
        : base("test_map_d", "Test Map D", 16, 16, 16, BaseTileData, OverlayTileData, WalkableTileIds, null, null, 1, 0)
    {
    }
}
