namespace PokemonGreen.Core.Maps;

/// <summary>
/// Categories for tile classification in the map system.
/// </summary>
public enum TileCategory
{
    /// <summary>Base terrain tiles like grass, water, paths.</summary>
    Terrain,

    /// <summary>Visual decoration tiles like trees, rocks, flowers.</summary>
    Decoration,

    /// <summary>Tiles that trigger actions like doors, warps, PCs.</summary>
    Interactive,

    /// <summary>NPC entity tiles.</summary>
    Entity,

    /// <summary>Trainer tiles with directional facing.</summary>
    Trainer,

    /// <summary>Wild Pokemon encounter zones.</summary>
    Encounter,

    /// <summary>Structural tiles like walls, ledges, stairs.</summary>
    Structure,

    /// <summary>Item pickup tiles.</summary>
    Item
}
