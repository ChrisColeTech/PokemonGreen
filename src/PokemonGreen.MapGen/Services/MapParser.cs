using System.Text.Json;
using PokemonGreen.MapGen.Models;

namespace PokemonGreen.MapGen.Services;

public static class MapParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Parses a .map.json file, handling both schema v2 and legacy v1 formats.
    /// Always returns a normalized v2 model.
    /// </summary>
    public static MapJsonModel ParseJsonFile(string path)
    {
        var json = File.ReadAllText(path);
        var raw = JsonSerializer.Deserialize<MapJsonModel>(json, JsonOptions)
            ?? throw new InvalidDataException($"Failed to parse map file: {path}");

        // Schema v2: baseTiles present
        if (raw.BaseTiles is { Length: > 0 })
        {
            // Ensure mapId/displayName are populated
            if (string.IsNullOrEmpty(raw.MapId))
            {
                var stem = Path.GetFileNameWithoutExtension(path);
                if (stem.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
                    stem = stem[..^4];
                raw.MapId = stem.ToLowerInvariant().Replace(' ', '_');
            }
            if (string.IsNullOrEmpty(raw.DisplayName))
                raw.DisplayName = raw.MapId;

            return raw;
        }

        // Legacy v1: flat "tiles" array
        if (raw.LegacyTiles is { Length: > 0 })
        {
            var name = raw.LegacyName ?? raw.DisplayName ?? "Imported Map";
            var mapId = name.ToLowerInvariant()
                .Replace(' ', '_')
                .Replace("-", "_");

            return new MapJsonModel
            {
                SchemaVersion = 2,
                MapId = mapId,
                DisplayName = name,
                TileSize = raw.TileSize > 0 ? raw.TileSize : 32,
                Width = raw.Width,
                Height = raw.Height,
                BaseTiles = raw.LegacyTiles,
                OverlayTiles = null
            };
        }

        throw new InvalidDataException($"Map file has no tile data: {path}");
    }
}
