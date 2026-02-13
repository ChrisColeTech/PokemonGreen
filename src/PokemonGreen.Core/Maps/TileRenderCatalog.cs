using System;
using System.Collections.Generic;

namespace PokemonGreen.Core.Maps;

public enum TileVisualKind
{
    AnimatedWater,
    Grass,
    Path,
    Tree,
    Rock,
    Flower,
    InteractiveObject,
    EntityMarker,
    TrainerMarker,
    Statue,
    Solid
}

public enum TileBaseLayer
{
    BaseTerrain,
    Structures
}

public enum TileOverlayKind
{
    None,
    Door,
    Sign,
    Warp,
    StrengthRock,
    CutTree,
    Pc,
    Statue,
    EntityNpc,
    EntityService,
    EntityItem,
    TrainerUp,
    TrainerDown,
    TrainerLeft,
    TrainerRight,
    TrainerBoss
}

public readonly record struct TileRenderRule(
    TileVisualKind VisualKind,
    TileBaseLayer BaseLayer,
    TileOverlayKind OverlayKind,
    byte Red,
    byte Green,
    byte Blue,
    bool IsTemporaryVisual)
{
    public static TileRenderRule Create(
        TileVisualKind visualKind,
        TileBaseLayer baseLayer,
        TileOverlayKind overlayKind = TileOverlayKind.None,
        bool isTemporaryVisual = false)
    {
        return new TileRenderRule(visualKind, baseLayer, overlayKind, 255, 255, 255, isTemporaryVisual);
    }

    public static TileRenderRule Create(
        TileVisualKind visualKind,
        TileBaseLayer baseLayer,
        byte red,
        byte green,
        byte blue,
        TileOverlayKind overlayKind = TileOverlayKind.None,
        bool isTemporaryVisual = false)
    {
        return new TileRenderRule(visualKind, baseLayer, overlayKind, red, green, blue, isTemporaryVisual);
    }
}

public sealed class TileRenderCatalog
{
    public const int MinKnownTileId = 0;
    public const int MaxKnownTileId = 50;

    private readonly Dictionary<int, TileRenderRule> _rules;

    public TileRenderCatalog()
    {
        _rules = BuildRules();
        ValidateCoverage(_rules);
    }

    public bool TryGetRule(int tileId, out TileRenderRule rule) => _rules.TryGetValue(tileId, out rule);

    private static Dictionary<int, TileRenderRule> BuildRules()
    {
        return new Dictionary<int, TileRenderRule>
        {
            [0] = TileRenderRule.Create(TileVisualKind.AnimatedWater, TileBaseLayer.BaseTerrain),
            [1] = TileRenderRule.Create(TileVisualKind.Grass, TileBaseLayer.BaseTerrain),
            [2] = TileRenderRule.Create(TileVisualKind.Path, TileBaseLayer.BaseTerrain),
            [3] = TileRenderRule.Create(TileVisualKind.Tree, TileBaseLayer.Structures),
            [4] = TileRenderRule.Create(TileVisualKind.InteractiveObject, TileBaseLayer.Structures, 139, 69, 19, TileOverlayKind.Door),
            [5] = TileRenderRule.Create(TileVisualKind.Path, TileBaseLayer.BaseTerrain, 107, 68, 35, isTemporaryVisual: true),
            [6] = TileRenderRule.Create(TileVisualKind.Path, TileBaseLayer.Structures, 85, 85, 85, isTemporaryVisual: true),
            [7] = TileRenderRule.Create(TileVisualKind.Grass, TileBaseLayer.BaseTerrain, 26, 138, 26, isTemporaryVisual: true),
            [8] = TileRenderRule.Create(TileVisualKind.Rock, TileBaseLayer.Structures),
            [9] = TileRenderRule.Create(TileVisualKind.InteractiveObject, TileBaseLayer.Structures, 139, 115, 85, TileOverlayKind.Sign),
            [10] = TileRenderRule.Create(TileVisualKind.EntityMarker, TileBaseLayer.Structures, 255, 107, 107, TileOverlayKind.EntityNpc),
            [11] = TileRenderRule.Create(TileVisualKind.EntityMarker, TileBaseLayer.Structures, 255, 217, 61, TileOverlayKind.EntityService),
            [12] = TileRenderRule.Create(TileVisualKind.EntityMarker, TileBaseLayer.Structures, 107, 203, 119, TileOverlayKind.EntityService),
            [13] = TileRenderRule.Create(TileVisualKind.EntityMarker, TileBaseLayer.Structures, 157, 78, 221, TileOverlayKind.EntityItem),
            [14] = TileRenderRule.Create(TileVisualKind.EntityMarker, TileBaseLayer.Structures, 224, 64, 251, TileOverlayKind.EntityItem),
            [15] = TileRenderRule.Create(TileVisualKind.Path, TileBaseLayer.BaseTerrain, 44, 44, 44, isTemporaryVisual: true),
            [16] = TileRenderRule.Create(TileVisualKind.InteractiveObject, TileBaseLayer.Structures, 0, 206, 201, TileOverlayKind.Warp),
            [17] = TileRenderRule.Create(TileVisualKind.AnimatedWater, TileBaseLayer.BaseTerrain, 41, 128, 185, isTemporaryVisual: true),
            [18] = TileRenderRule.Create(TileVisualKind.Path, TileBaseLayer.Structures, 121, 85, 72, isTemporaryVisual: true),
            [19] = TileRenderRule.Create(TileVisualKind.Flower, TileBaseLayer.BaseTerrain),
            [20] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 255, 146, 43, TileOverlayKind.TrainerUp),
            [21] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 245, 132, 32, TileOverlayKind.TrainerDown),
            [22] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 235, 121, 24, TileOverlayKind.TrainerLeft),
            [23] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 255, 163, 66, TileOverlayKind.TrainerRight),
            [24] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 255, 23, 68, TileOverlayKind.TrainerBoss),
            [25] = TileRenderRule.Create(TileVisualKind.AnimatedWater, TileBaseLayer.BaseTerrain, 30, 144, 255, isTemporaryVisual: true),
            [26] = TileRenderRule.Create(TileVisualKind.Rock, TileBaseLayer.Structures, 141, 110, 99, TileOverlayKind.StrengthRock),
            [27] = TileRenderRule.Create(TileVisualKind.Tree, TileBaseLayer.Structures, 76, 175, 80, TileOverlayKind.CutTree, true),
            [28] = TileRenderRule.Create(TileVisualKind.Grass, TileBaseLayer.BaseTerrain, 255, 215, 0, isTemporaryVisual: true),
            [29] = TileRenderRule.Create(TileVisualKind.Grass, TileBaseLayer.BaseTerrain, 255, 0, 255, isTemporaryVisual: true),
            [30] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 128, 0, 128, TileOverlayKind.TrainerBoss),
            [31] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 74, 0, 128, TileOverlayKind.TrainerUp),
            [32] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 88, 18, 141, TileOverlayKind.TrainerDown),
            [33] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 64, 0, 112, TileOverlayKind.TrainerLeft),
            [34] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 96, 24, 160, TileOverlayKind.TrainerRight),
            [35] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 106, 13, 173, TileOverlayKind.TrainerUp),
            [36] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 120, 28, 188, TileOverlayKind.TrainerDown),
            [37] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 90, 0, 150, TileOverlayKind.TrainerLeft),
            [38] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 132, 45, 198, TileOverlayKind.TrainerRight),
            [39] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 220, 20, 60, TileOverlayKind.TrainerDown),
            [40] = TileRenderRule.Create(TileVisualKind.EntityMarker, TileBaseLayer.Structures, 70, 130, 180, TileOverlayKind.EntityItem),
            [41] = TileRenderRule.Create(TileVisualKind.InteractiveObject, TileBaseLayer.Structures, 169, 169, 169, TileOverlayKind.Pc),
            [42] = TileRenderRule.Create(TileVisualKind.EntityMarker, TileBaseLayer.Structures, 255, 0, 0, TileOverlayKind.EntityItem),
            [43] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 192, 192, 192, TileOverlayKind.TrainerBoss),
            [44] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 255, 215, 0, TileOverlayKind.TrainerBoss),
            [45] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 240, 198, 0, TileOverlayKind.TrainerBoss),
            [46] = TileRenderRule.Create(TileVisualKind.TrainerMarker, TileBaseLayer.Structures, 200, 16, 54, TileOverlayKind.TrainerBoss),
            [47] = TileRenderRule.Create(TileVisualKind.EntityMarker, TileBaseLayer.Structures, 139, 69, 19, TileOverlayKind.EntityNpc),
            [48] = TileRenderRule.Create(TileVisualKind.EntityMarker, TileBaseLayer.Structures, 221, 160, 221, TileOverlayKind.EntityNpc),
            [49] = TileRenderRule.Create(TileVisualKind.Statue, TileBaseLayer.Structures, overlayKind: TileOverlayKind.Statue),
            [50] = TileRenderRule.Create(TileVisualKind.EntityMarker, TileBaseLayer.Structures, 0, 206, 209, TileOverlayKind.EntityItem),
        };
    }

    private static void ValidateCoverage(IReadOnlyDictionary<int, TileRenderRule> rules)
    {
        var missingIds = new List<int>();
        for (var tileId = MinKnownTileId; tileId <= MaxKnownTileId; tileId += 1)
        {
            if (!rules.ContainsKey(tileId))
            {
                missingIds.Add(tileId);
            }
        }

        if (missingIds.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException($"Tile render mapping is missing ids: {string.Join(", ", missingIds)}");
    }
}
