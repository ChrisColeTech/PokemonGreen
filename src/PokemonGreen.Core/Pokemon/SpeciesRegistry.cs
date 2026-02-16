using System.Collections.Generic;
using PokemonGreen.Core.Battle;

namespace PokemonGreen.Core.Pokemon;

/// <summary>
/// Static registry of all Pokemon species data.
/// Follows the same pattern as MoveRegistry.
/// </summary>
public static class SpeciesRegistry
{
    private static readonly Dictionary<int, SpeciesData> _species = new();

    static SpeciesRegistry()
    {
        // Gen 1 starters and common wild Pokemon
        Register(new SpeciesData { SpeciesId = 1, Name = "Bulbasaur", BaseHP = 45, BaseAttack = 49, BaseDefense = 49, BaseSpAttack = 65, BaseSpDefense = 65, BaseSpeed = 45, Type1 = MoveType.Grass, Type2 = MoveType.Poison, BaseEXPYield = 64, GrowthRate = GrowthRate.MediumSlow, CatchRate = 45 });
        Register(new SpeciesData { SpeciesId = 2, Name = "Ivysaur", BaseHP = 60, BaseAttack = 62, BaseDefense = 63, BaseSpAttack = 80, BaseSpDefense = 80, BaseSpeed = 60, Type1 = MoveType.Grass, Type2 = MoveType.Poison, BaseEXPYield = 142, GrowthRate = GrowthRate.MediumSlow, CatchRate = 45 });
        Register(new SpeciesData { SpeciesId = 3, Name = "Venusaur", BaseHP = 80, BaseAttack = 82, BaseDefense = 83, BaseSpAttack = 100, BaseSpDefense = 100, BaseSpeed = 80, Type1 = MoveType.Grass, Type2 = MoveType.Poison, BaseEXPYield = 236, GrowthRate = GrowthRate.MediumSlow, CatchRate = 45 });

        Register(new SpeciesData { SpeciesId = 4, Name = "Charmander", BaseHP = 39, BaseAttack = 52, BaseDefense = 43, BaseSpAttack = 60, BaseSpDefense = 50, BaseSpeed = 65, Type1 = MoveType.Fire, BaseEXPYield = 62, GrowthRate = GrowthRate.MediumSlow, CatchRate = 45 });
        Register(new SpeciesData { SpeciesId = 5, Name = "Charmeleon", BaseHP = 58, BaseAttack = 64, BaseDefense = 58, BaseSpAttack = 80, BaseSpDefense = 65, BaseSpeed = 80, Type1 = MoveType.Fire, BaseEXPYield = 142, GrowthRate = GrowthRate.MediumSlow, CatchRate = 45 });
        Register(new SpeciesData { SpeciesId = 6, Name = "Charizard", BaseHP = 78, BaseAttack = 84, BaseDefense = 78, BaseSpAttack = 109, BaseSpDefense = 85, BaseSpeed = 100, Type1 = MoveType.Fire, Type2 = MoveType.Flying, BaseEXPYield = 240, GrowthRate = GrowthRate.MediumSlow, CatchRate = 45 });

        Register(new SpeciesData { SpeciesId = 7, Name = "Squirtle", BaseHP = 44, BaseAttack = 48, BaseDefense = 65, BaseSpAttack = 50, BaseSpDefense = 64, BaseSpeed = 43, Type1 = MoveType.Water, BaseEXPYield = 63, GrowthRate = GrowthRate.MediumSlow, CatchRate = 45 });
        Register(new SpeciesData { SpeciesId = 8, Name = "Wartortle", BaseHP = 59, BaseAttack = 63, BaseDefense = 80, BaseSpAttack = 65, BaseSpDefense = 80, BaseSpeed = 58, Type1 = MoveType.Water, BaseEXPYield = 142, GrowthRate = GrowthRate.MediumSlow, CatchRate = 45 });
        Register(new SpeciesData { SpeciesId = 9, Name = "Blastoise", BaseHP = 79, BaseAttack = 83, BaseDefense = 100, BaseSpAttack = 85, BaseSpDefense = 105, BaseSpeed = 78, Type1 = MoveType.Water, BaseEXPYield = 239, GrowthRate = GrowthRate.MediumSlow, CatchRate = 45 });

        // Common wild Pokemon
        Register(new SpeciesData { SpeciesId = 10, Name = "Caterpie", BaseHP = 45, BaseAttack = 30, BaseDefense = 35, BaseSpAttack = 20, BaseSpDefense = 20, BaseSpeed = 45, Type1 = MoveType.Bug, BaseEXPYield = 39, GrowthRate = GrowthRate.MediumFast, CatchRate = 255 });
        Register(new SpeciesData { SpeciesId = 13, Name = "Weedle", BaseHP = 40, BaseAttack = 35, BaseDefense = 30, BaseSpAttack = 20, BaseSpDefense = 20, BaseSpeed = 50, Type1 = MoveType.Bug, Type2 = MoveType.Poison, BaseEXPYield = 39, GrowthRate = GrowthRate.MediumFast, CatchRate = 255 });
        Register(new SpeciesData { SpeciesId = 16, Name = "Pidgey", BaseHP = 40, BaseAttack = 45, BaseDefense = 40, BaseSpAttack = 35, BaseSpDefense = 35, BaseSpeed = 56, Type1 = MoveType.Normal, Type2 = MoveType.Flying, BaseEXPYield = 50, GrowthRate = GrowthRate.MediumSlow, CatchRate = 255 });
        Register(new SpeciesData { SpeciesId = 17, Name = "Pidgeotto", BaseHP = 63, BaseAttack = 60, BaseDefense = 55, BaseSpAttack = 50, BaseSpDefense = 50, BaseSpeed = 71, Type1 = MoveType.Normal, Type2 = MoveType.Flying, BaseEXPYield = 122, GrowthRate = GrowthRate.MediumSlow, CatchRate = 120 });
        Register(new SpeciesData { SpeciesId = 19, Name = "Rattata", BaseHP = 30, BaseAttack = 56, BaseDefense = 35, BaseSpAttack = 25, BaseSpDefense = 35, BaseSpeed = 72, Type1 = MoveType.Normal, BaseEXPYield = 51, GrowthRate = GrowthRate.MediumFast, CatchRate = 255 });
        Register(new SpeciesData { SpeciesId = 21, Name = "Spearow", BaseHP = 40, BaseAttack = 60, BaseDefense = 30, BaseSpAttack = 31, BaseSpDefense = 31, BaseSpeed = 70, Type1 = MoveType.Normal, Type2 = MoveType.Flying, BaseEXPYield = 52, GrowthRate = GrowthRate.MediumFast, CatchRate = 255 });
        Register(new SpeciesData { SpeciesId = 25, Name = "Pikachu", BaseHP = 35, BaseAttack = 55, BaseDefense = 40, BaseSpAttack = 50, BaseSpDefense = 50, BaseSpeed = 90, Type1 = MoveType.Electric, BaseEXPYield = 112, GrowthRate = GrowthRate.MediumFast, CatchRate = 190 });
        Register(new SpeciesData { SpeciesId = 41, Name = "Zubat", BaseHP = 40, BaseAttack = 45, BaseDefense = 35, BaseSpAttack = 30, BaseSpDefense = 40, BaseSpeed = 55, Type1 = MoveType.Poison, Type2 = MoveType.Flying, BaseEXPYield = 49, GrowthRate = GrowthRate.MediumFast, CatchRate = 255 });
        Register(new SpeciesData { SpeciesId = 74, Name = "Geodude", BaseHP = 40, BaseAttack = 80, BaseDefense = 100, BaseSpAttack = 30, BaseSpDefense = 30, BaseSpeed = 20, Type1 = MoveType.Rock, Type2 = MoveType.Ground, BaseEXPYield = 60, GrowthRate = GrowthRate.MediumSlow, CatchRate = 255 });
    }

    private static void Register(SpeciesData data)
    {
        _species[data.SpeciesId] = data;
    }

    public static SpeciesData? GetSpecies(int speciesId)
    {
        return _species.TryGetValue(speciesId, out var data) ? data : null;
    }
}
