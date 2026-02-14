namespace PokemonGreen.Core.Maps;

/// <summary>
/// Defines the properties of a tile type in the map system.
/// </summary>
/// <param name="Id">Unique identifier for the tile.</param>
/// <param name="Name">Display name of the tile.</param>
/// <param name="Walkable">Whether the player can walk on this tile.</param>
/// <param name="Color">Hex color code for rendering (e.g., "#7ec850").</param>
/// <param name="Category">The category this tile belongs to.</param>
/// <param name="OverlayBehavior">Optional behavior modifier for interactive tiles (e.g., "door", "warp", "trainer_up").</param>
public record TileDefinition(
    int Id,
    string Name,
    bool Walkable,
    string Color,
    TileCategory Category,
    string? OverlayBehavior = null
);
