using System.Text.Json;
using System.Text.Json.Serialization;
using PokemonGreen.Core.Maps;

namespace PokemonGreen.MapGen.Services;

public static class RegistryExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Exports the TileRegistry data as JSON matching the TypeScript format.
    /// </summary>
    /// <returns>JSON string representation of the tile registry.</returns>
    public static string ExportToJson()
    {
        var tiles = TileRegistry.AllTiles
            .OrderBy(t => t.Id)
            .Select(t => new TileExportModel
            {
                Id = t.Id,
                Name = t.Name,
                Walkable = t.Walkable,
                Color = t.Color,
                Category = t.Category.ToString(),
                OverlayBehavior = t.OverlayBehavior
            })
            .ToList();

        var export = new RegistryExportModel
        {
            Version = 1,
            Tiles = tiles
        };

        return JsonSerializer.Serialize(export, JsonOptions);
    }

    private class RegistryExportModel
    {
        public int Version { get; set; }
        public List<TileExportModel> Tiles { get; set; } = [];
    }

    private class TileExportModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool Walkable { get; set; }
        public string Color { get; set; } = "";
        public string Category { get; set; } = "";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? OverlayBehavior { get; set; }
    }
}
