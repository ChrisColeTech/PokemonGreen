#nullable enable

namespace PokemonGreen.Core.Maps;

public sealed class TestMapB : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
        80, 2, 2, 2, 2, 2, 2, 80, 
        80, 2, 2, 2, 2, 2, 2, 80, 
        80, 2, 2, 2, 2, 2, 2, 80, 
        80, 2, 2, 2, 2, 2, 2, 80, 
        80, 2, 2, 2, 2, 2, 2, 80, 
        80, 2, 2, 2, 2, 2, 2, 80, 
        80, 2, 2, 2, 2, 2, 2, 80, 
        80, 80, 80, 80, 80, 80, 80, 80
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
        2
    ];

    public static TestMapB Instance { get; } = new();

    private TestMapB()
        : base("test_map_b", "Test Map B", 8, 8, 16, BaseTileData, OverlayTileData, WalkableTileIds, null, null, 0, 1)
    {
    }
}
