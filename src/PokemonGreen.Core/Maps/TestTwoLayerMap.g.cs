#nullable enable

namespace PokemonGreen.Core.Maps;

public sealed class TestTwoLayerMap : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1
    ];

    private static readonly int?[] OverlayTileData =
    [
        null, null, null, null, null, null, null, null,
        null,   3, null, null, null, null, null, null,
        null, null, null, null,   19, null, null, null,
        null, null,   8, null,   51, null, null, null,
        null,   54, null,   49, null, null, null, null,
        null, null, null,   56, null, null, null, null
    ];

    private static readonly int[] WalkableTileIds =
    [
        1, 2, 19
    ];

    public static TestTwoLayerMap Instance { get; } = new();

    private TestTwoLayerMap()
        : base("test_two_layer_map", "Test Two Layer Map", 8, 6, 32, BaseTileData, OverlayTileData, WalkableTileIds)
    {
    }
}