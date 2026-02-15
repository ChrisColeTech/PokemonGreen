#nullable enable

// Tile Legend:
//    80 = Wall (Structure)
//   115 = Transition East (Terrain)
//   116 = FlowerRed (Decoration)
//   121 = Flames (Encounter)
//   122 = FireGround (Terrain)
//   123 = FireRock (Decoration)
//   124 = FireBoulder (Decoration)
//   125 = LavaSplatter (Decoration)
//   126 = LavaPool (Terrain)
namespace PokemonGreen.Core.Maps;

public sealed class TestMapB : MapDefinition
{
    private static readonly int[] BaseTileData =
    [
        126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126,
        126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126,
        126, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80,
        126, 80, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122,
        126, 80, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122,
        126, 80, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122,
        126, 80, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122,
        126, 80, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 115,
        126, 80, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 115,
        126, 80, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122,
        126, 80, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122,
        126, 80, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122,
        126, 80, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122, 122,
        126, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 80,
        126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126,
        126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126, 126
    ];

    private static readonly int?[] OverlayTileData =
    [
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
        null, null,  125, null, null, null, null,  125, null, null, null, null, null,  125, null, null,
        null, null, null, null, null,  123, null, null, null, null, null, null,  116, null, null, null,
        null, null, null,  116, null, null, null, null, null,  121, null, null, null, null, null, null,
        null, null, null, null, null, null,  121,  121,  121,  121,  121, null, null, null, null, null,
        null, null, null, null, null,  121,  121,  121,  121,  121, null, null, null, null, null, null,
        null, null, null,  121,  121,  121, null,  121, null, null, null, null, null, null, null, null,
        null, null, null, null,  124, null, null, null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null,  116, null, null,  116, null,  123, null, null,  125, null,
        null, null, null,  125, null, null, null, null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null
    ];

    private static readonly int[] WalkableTileIds =
    [
        115, 116, 121, 122
    ];

    public static TestMapB Instance { get; } = new();

    private TestMapB()
        : base("small_world", "test_map_b", "Test Map B", 16, 16, 16, BaseTileData, OverlayTileData, WalkableTileIds, null, null, -1, 0)
    {
    }
}
