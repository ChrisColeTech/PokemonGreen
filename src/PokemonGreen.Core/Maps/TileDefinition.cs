namespace PokemonGreen.Core.Maps;

public record TileDefinition(
    int Id,
    string Name,
    bool Walkable,
    string Color,
    TileCategory Category,
    string? OverlayBehavior = null,
    int? EntityId = null
);
