namespace PokemonGreen.Core.Pokemon;

public enum Gender : byte { Male, Female, Unknown }

/// <summary>
/// Minimal placeholder for a Pokemon in the player's party.
/// Will be expanded when battle/species systems are built.
/// </summary>
public class PartyPokemon
{
    public string Nickname { get; set; } = "MissingNo";
    public string Species { get; set; } = "MissingNo";
    public int Level { get; set; } = 1;
    public int CurrentHP { get; set; } = 10;
    public int MaxHP { get; set; } = 10;
    public Gender Gender { get; set; } = Gender.Unknown;
    public string? Status { get; set; }
    public int? HeldItemId { get; set; }

    public float HPPercent => MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;
    public bool IsFainted => CurrentHP <= 0;
}
