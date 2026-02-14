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

    /// <summary>Warp connections linking positions on this map to other maps.</summary>
    [JsonPropertyName("warps")]
    public List<WarpJsonModel>? Warps { get; set; }

    /// <summary>Edge connections to adjacent maps.</summary>
    [JsonPropertyName("connections")]
    public List<ConnectionJsonModel>? Connections { get; set; }

    // ---- Legacy v1 fields (read-only, for import compat) ----

    [JsonPropertyName("name")]
    public string? LegacyName { get; set; }

    [JsonPropertyName("tiles")]
    public int[][]? LegacyTiles { get; set; }

    [JsonPropertyName("version")]
    public int? LegacyVersion { get; set; }
}

/// <summary>
/// JSON model for a warp connection.
/// </summary>
public class WarpJsonModel
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("targetMapId")]
    public string TargetMapId { get; set; } = "";

    [JsonPropertyName("targetX")]
    public int TargetX { get; set; }

    [JsonPropertyName("targetY")]
    public int TargetY { get; set; }

    /// <summary>"step" or "interact". Defaults to "step".</summary>
    [JsonPropertyName("trigger")]
    public string Trigger { get; set; } = "step";
}

/// <summary>
/// JSON model for an edge connection to an adjacent map.
/// </summary>
public class ConnectionJsonModel
{
    /// <summary>"north", "south", "east", or "west".</summary>
    [JsonPropertyName("edge")]
    public string Edge { get; set; } = "";

    [JsonPropertyName("targetMapId")]
    public string TargetMapId { get; set; } = "";

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}
