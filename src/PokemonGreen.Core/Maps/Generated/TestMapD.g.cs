#nullable enable

// Tile Legend:
//     0 = Water (Terrain)
//    16 = Tree (Decoration)
//    17 = Rock (Decoration)
//    18 = Flower (Decoration)
//    22 = Boulder (Decoration)
//    80 = Wall (Structure)
//   114 = Transition West (Terrain)
//   122 = FireGround (Terrain)
namespace PokemonGreen.Core.Maps;

public sealed class TestMapD : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
        80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 0, 
        122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 80, 0, 
        122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 80, 0, 
        122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 80, 0, 
        122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 80, 0, 
        114, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 80, 0, 
        114, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 80, 0, 
        122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 80, 0, 
        122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 80, 0, 
        122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 80, 0, 
        122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 80, 0, 
        80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 0, 
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    private static readonly int?[] OverlayTileData =
    [
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null,   18, null, null, null, 
        null, null, null, null, null, null, null,   22, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null,   17, null, null, null, 
        null, null, null,   16, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null
    ];

    private static readonly int[] WalkableTileIds =
    [
        18, 114, 122
    ];

    public static TestMapD Instance { get; } = new();

    private TestMapD()
        : base("small_world", "test_map_d", "Test Map D", 16, 16, 16, BaseTileData, OverlayTileData, WalkableTileIds, null, null, 1, 0)
    {
    }
}
