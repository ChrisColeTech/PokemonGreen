#nullable enable

using System.Collections.Generic;

namespace PokemonGreen.Core.Maps;

public static partial class MapCatalog
{
    private static readonly IReadOnlyList<MapDefinition> AllMaps =
    [
        TestLegacyMap.Instance,
        TestTwoLayerMap.Instance
    ];

    private static readonly IReadOnlyDictionary<string, MapDefinition> MapsById = new Dictionary<string, MapDefinition>
    {
        ["test_legacy_map"] = TestLegacyMap.Instance,
        ["test_two_layer_map"] = TestTwoLayerMap.Instance
    };

    public static IReadOnlyList<MapDefinition> All => AllMaps;

    public static bool TryGetById(string mapId, out MapDefinition? map)
    {
        if (string.IsNullOrWhiteSpace(mapId))
        {
            map = null;
            return false;
        }

        return MapsById.TryGetValue(mapId, out map);
    }
}