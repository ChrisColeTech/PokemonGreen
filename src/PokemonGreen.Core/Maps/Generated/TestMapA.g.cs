#nullable enable

namespace PokemonGreen.Core.Maps;

public sealed class TestMapA : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
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
        0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 1, 1, 1, 1, 0, 0
    ];

    private static readonly int?[] OverlayTileData =
    [
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80,   18, null, null, null, null, null, null,   17, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null,   22, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null,   16, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null, null, null, null,   80, null, null, 
        null, null,   80, null, null, null, null, null, null, null,   23, null, null,   80, null, null
    ];

    private static readonly int[] WalkableTileIds =
    [
        1, 2, 18
    ];

    public static TestMapA Instance { get; } = new();

    private TestMapA()
        : base("small_world", "test_map_a", "Test Map A", 16, 16, 16, BaseTileData, OverlayTileData, WalkableTileIds, null, null, 0, -1)
    {
    }
}
