#nullable enable

namespace PokemonGreen.Core.Maps;

public sealed class TestMapCenter : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
        1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 
        2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 
        2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1
    ];

    private static readonly int?[] OverlayTileData =
    [
        null, null, null, null, null,   23, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null,   72,   72, null, 
        null, null, null, null, null, null, null, null, null, null,   72, null, null, null,   72, null, 
        null, null,   72,   72,   72, null, null, null, null, null,   72, null,   18, null,   72, null, 
        null, null,   72,   72, null, null, null,   22, null, null,   72,   72, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null,   72, null, null, null,   23, 
        null, null, null, null, null, null,   72,   18,   72, null,   72,   72, null, null, null, null, 
        null, null, null, null, null, null,   72,   72,   72,   72,   72,   72, null, null, null, null, 
        null, null, null, null, null,   72,   72,   72,   72,   72,   72,   18, null, null, null, null, 
          23, null, null, null, null,   18,   72,   72,   72,   72,   72, null,   17, null, null, null, 
        null, null, null,   16, null, null,   72,   18,   72, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null,   72,   72, null, null, null, 
        null, null,   18,   72,   72, null, null, null, null, null, null,   72,   72, null, null, null, 
        null, null,   72, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null,   23, null, null, null, null, null
    ];

    private static readonly int[] WalkableTileIds =
    [
        1, 2, 18, 72
    ];

    public static TestMapCenter Instance { get; } = new();

    private TestMapCenter()
        : base("small_world", "test_map_center", "Test Map center", 16, 16, 16, BaseTileData, OverlayTileData, WalkableTileIds)
    {
    }
}
