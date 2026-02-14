using System.Text.Json.Serialization;

namespace PokemonGreen.MapGen.Models;

/// <summary>
/// Schema v2 JSON model for .map.json files.
/// </summary>
public class MapJsonModel
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 2;

    [JsonPropertyName("mapId")]
    public string MapId { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("tileSize")]
    public int TileSize { get; set; } = 32;

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    /// <summary>2D jagged array [y][x] of base tile IDs.</summary>
    [JsonPropertyName("baseTiles")]
    public int[][] BaseTiles { get; set; } = [];

    /// <summary>2D jagged array [y][x] of overlay tile IDs (null = no overlay).</summary>
    [JsonPropertyName("overlayTiles")]
    public int?[][]? OverlayTiles { get; set; }

    // ---- Legacy v1 fields (read-only, for import compat) ----

    [JsonPropertyName("name")]
    public string? LegacyName { get; set; }

    [JsonPropertyName("tiles")]
    public int[][]? LegacyTiles { get; set; }

    [JsonPropertyName("version")]
    public int? LegacyVersion { get; set; }
}
