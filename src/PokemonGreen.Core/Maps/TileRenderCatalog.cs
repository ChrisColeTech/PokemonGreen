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
    Solid,
    Item
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
    byte Blue
);

public sealed class TileRenderCatalog
{
    public const int MinKnownTileId = 0;
    public const int MaxKnownTileId = 70;

    private readonly Dictionary<int, TileRenderRule> _rules;

    public TileRenderCatalog()
    {
        _rules = BuildRulesFromRegistry();
        ValidateCoverage();
    }

    public bool TryGetRule(int tileId, out TileRenderRule rule) => _rules.TryGetValue(tileId, out rule);

    private static Dictionary<int, TileRenderRule> BuildRulesFromRegistry()
    {
        var rules = new Dictionary<int, TileRenderRule>();

        foreach (var tile in TileRegistry.List)
        {
            var baseLayer = tile.Category is TileCategory.Decoration or TileCategory.Interactive 
                or TileCategory.Entity or TileCategory.Trainer or TileCategory.Item or TileCategory.Structure
                ? TileBaseLayer.Structures 
                : TileBaseLayer.BaseTerrain;

            rules[tile.Id] = new TileRenderRule(
                tile.VisualKind,
                baseLayer,
                tile.OverlayKind,
                tile.Red,
                tile.Green,
                tile.Blue
            );
        }

        return rules;
    }

    private void ValidateCoverage()
    {
        var missingIds = new List<int>();
        for (var tileId = MinKnownTileId; tileId <= MaxKnownTileId; tileId++)
        {
            if (!_rules.ContainsKey(tileId))
            {
                missingIds.Add(tileId);
            }
        }

        if (missingIds.Count > 0)
        {
            throw new InvalidOperationException($"Tile render catalog is missing ids: {string.Join(", ", missingIds)}");
        }
    }
}
