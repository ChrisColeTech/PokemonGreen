namespace PokemonGreen.Core.Maps;

public static class MapRegistry
{
    /// <summary>
    /// Initializes all generated map singletons, registering them in MapCatalog.
    /// </summary>
    public static void Initialize()
    {
        _ = TestMapA.Instance;
        _ = TestMapB.Instance;
        _ = BattleArena.Instance;
        _ = CoastalRoute.Instance;
        _ = DepartmentStore.Instance;
        _ = EvergreenExpanse.Instance;
        _ = GreenleafMetro.Instance;
        _ = LakeSerenity.Instance;
        _ = Map.Instance;
        _ = MtGraniteCave.Instance;
        _ = SafariZone.Instance;
        _ = TestLegacyMap.Instance;
        _ = TestTwoLayerMap.Instance;
        _ = Town1.Instance;
    }
}
