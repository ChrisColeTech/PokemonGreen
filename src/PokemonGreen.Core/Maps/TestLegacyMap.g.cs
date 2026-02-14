#nullable enable

namespace PokemonGreen.Core.Maps;

public sealed class TestLegacyMap : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
        1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1, 
        1, 1, 1, 1, 1
    ];

    private static readonly int?[] OverlayTileData =
    [
        null, null, null, null, null, 
        null,    3, null,   19, null, 
        null, null,    8, null, null, 
        null, null, null, null, null, 
        null, null, null, null, null
    ];

    private static readonly int[] WalkableTileIds =
    [
        1, 3, 8
    ];

    public static TestLegacyMap Instance { get; } = new();

    private TestLegacyMap()
        : base("test_legacy_map", "Test Legacy Map", 5, 5, 32, BaseTileData, OverlayTileData, WalkableTileIds)
    {
    }
}
