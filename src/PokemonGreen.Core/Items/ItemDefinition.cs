using PokemonGreen.Core.Items;

namespace PokemonGreen.Core.Items;

public record ItemDefinition(
    int Id,
    string Name,
    string SpriteName,
    ItemCategory Category,
    int BuyPrice,
    int SellPrice,
    bool UsableInBattle,
    bool UsableOverworld,
    string? Effect = null
);
