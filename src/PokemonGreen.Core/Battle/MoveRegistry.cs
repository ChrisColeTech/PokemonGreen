using System.Collections.Generic;

namespace PokemonGreen.Core.Battle;

/// <summary>
/// Hardcoded move database. Enough starter moves for testing.
/// </summary>
public static class MoveRegistry
{
    private static readonly Dictionary<int, MoveData> _moves = new();

    static MoveRegistry()
    {
        Register(new MoveData(1,  "Tackle",     MoveType.Normal,   MoveCategory.Physical, 40,  100, 35));
        Register(new MoveData(2,  "Scratch",    MoveType.Normal,   MoveCategory.Physical, 40,  100, 35));
        Register(new MoveData(3,  "Growl",      MoveType.Normal,   MoveCategory.Status,    0,  100, 40));
        Register(new MoveData(4,  "Leer",       MoveType.Normal,   MoveCategory.Status,    0,  100, 30));
        Register(new MoveData(5,  "Ember",      MoveType.Fire,     MoveCategory.Special,  40,  100, 25));
        Register(new MoveData(6,  "Vine Whip",  MoveType.Grass,    MoveCategory.Physical, 45,  100, 25));
        Register(new MoveData(7,  "Water Gun",  MoveType.Water,    MoveCategory.Special,  40,  100, 25));
        Register(new MoveData(8,  "Sand Attack", MoveType.Ground,  MoveCategory.Status,    0,  100, 15));
        Register(new MoveData(9,  "Gust",       MoveType.Flying,   MoveCategory.Special,  40,  100, 35));
        Register(new MoveData(10, "Quick Attack", MoveType.Normal, MoveCategory.Physical, 40,  100, 30));
        Register(new MoveData(11, "Thundershock", MoveType.Electric, MoveCategory.Special, 40, 100, 30));
        Register(new MoveData(12, "Pound",      MoveType.Normal,   MoveCategory.Physical, 40,  100, 35));
    }

    private static void Register(MoveData move) => _moves[move.Id] = move;

    public static MoveData? GetMove(int id) =>
        _moves.TryGetValue(id, out var move) ? move : null;
}
