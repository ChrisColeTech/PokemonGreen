#nullable enable

namespace PokemonGreen.Core.Maps;

public sealed class TestMapB : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    private static readonly int?[] OverlayTileData =
    [
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80, 
        null,   80, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null,   80, null, null, null, null, null, null, null, null, null, null,   16, null, null, null, 
        null,   80, null,   17, null, null, null, null, null, null, null, null, null, null, null,   23, 
        null,   80, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null,   80, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null,   80, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null,   80, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null,   80, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null,   80, null, null, null, null, null, null,   22, null, null, null, null, null, null, null, 
        null,   80, null,   18, null, null, null, null, null, null, null, null, null, null, null, null, 
        null,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80,   80, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null
    ];

    private static readonly int[] WalkableTileIds =
    [
        1, 2, 18
    ];

    public static TestMapB Instance { get; } = new();

    private TestMapB()
        : base("test_map_b", "Test Map B", 16, 16, 16, BaseTileData, OverlayTileData, WalkableTileIds, null, null, -1, 0)
    {
    }
}
