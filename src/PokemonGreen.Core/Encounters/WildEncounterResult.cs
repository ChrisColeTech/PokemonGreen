namespace PokemonGreen.Core.Encounters;

/// <summary>
/// The result of a successful wild encounter roll.
/// </summary>
public record WildEncounterResult(
    int SpeciesId,
    int Level,
    string EncounterType
);
