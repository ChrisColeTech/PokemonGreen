using System.Text.Json.Serialization;

namespace PokemonGreen.Core.Encounters;

/// <summary>
/// Root encounter data for a single map, loaded from JSON.
/// </summary>
public class MapEncounterData
{
    [JsonPropertyName("mapId")]
    public string MapId { get; set; } = "";

    [JsonPropertyName("progressMultiplier")]
    public float ProgressMultiplier { get; set; }

    [JsonPropertyName("encounterGroups")]
    public EncounterTable[] EncounterGroups { get; set; } = [];
}
