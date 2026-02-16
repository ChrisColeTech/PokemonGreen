using PokemonGreen.Core.Pokemon;

namespace PokemonGreen.Core.Items;

public record ItemEffect(
    ItemEffectType Type,
    int Amount = 0,
    StatusCondition TargetStatus = StatusCondition.None,
    bool LowersFriendship = false
);
