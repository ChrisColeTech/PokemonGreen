using PokemonGreen.Core.Data;

namespace PokemonGreen.Core.Battle;

/// <summary>
/// Move database backed by SQLite (gamedata.db).
/// </summary>
public static class MoveRegistry
{
    public static MoveData? GetMove(int id) => GameDataDb.GetMove(id);
}
