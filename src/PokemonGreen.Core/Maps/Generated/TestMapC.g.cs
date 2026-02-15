#nullable enable

namespace PokemonGreen.Core.Maps;

public sealed class TestMapC : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
        0, 0, 1, 1, 1, 1, 1, 1, 112, 1, 1, 1, 1, 1, 0, 0,
        0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 
        0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 
        0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 
        0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 
        0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 
        0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 
        0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 
        0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 
        0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 
        0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 
        0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 
        0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 
        0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 
        0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    private static readonly int?[] OverlayTileData =
    [
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null,
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null,
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null,
        null, null,   80, null,   16, null, null, null, null, null, null, null, null,   80, null, null,
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null,   22, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null,   17, null, null, null, null, null, null,   18,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null
    ];

    private static readonly int[] WalkableTileIds =
    [
        1, 2, 18, 112
    ];

    public static TestMapC Instance { get; } = new();

    private TestMapC()
        : base("small_world", "test_map_c", "Test Map C", 16, 16, 16, BaseTileData, OverlayTileData, WalkableTileIds, null, null, 0, 1)
    {
    }
}
