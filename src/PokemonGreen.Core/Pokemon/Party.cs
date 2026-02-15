using System.Collections.Generic;

namespace PokemonGreen.Core.Pokemon;

/// <summary>
/// The player's party of up to 6 Pokemon.
/// </summary>
public class Party
{
    public const int MaxSize = 6;
    private readonly List<PartyPokemon> _members = new();

    public int Count => _members.Count;
    public PartyPokemon this[int index] => _members[index];

    public void Add(PartyPokemon pkmn)
    {
        if (_members.Count < MaxSize)
            _members.Add(pkmn);
    }

    /// <summary>Create a party with test data for development.</summary>
    public static Party CreateTestParty()
    {
        var party = new Party();
        party.Add(new PartyPokemon
        {
            Nickname = "Charmander", Species = "Charmander",
            Level = 5, CurrentHP = 20, MaxHP = 20,
            Gender = Gender.Male
        });
        party.Add(new PartyPokemon
        {
            Nickname = "Pidgey", Species = "Pidgey",
            Level = 3, CurrentHP = 12, MaxHP = 15,
            Gender = Gender.Female
        });
        party.Add(new PartyPokemon
        {
            Nickname = "Bulbasaur", Species = "Bulbasaur",
            Level = 4, CurrentHP = 0, MaxHP = 18,
            Gender = Gender.Male, Status = "FNT"
        });
        return party;
    }
}
