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
                Category = t.Category.ToString().ToLowerInvariant(),
                OverlayBehavior = t.OverlayBehavior
            })
            .ToList();

        var categories = Enum.GetValues<TileCategory>()
            .Select(c => new CategoryExportModel
            {
                Id = c.ToString().ToLowerInvariant(),
                Label = c.ToString(),
                ShowInPalette = true
            })
            .ToList();

        var export = new RegistryExportModel
        {
            Id = "pokemon-green-tiles",
            Name = "Pokemon Green Tile Registry",
            Version = "1.0.0",
            Categories = categories,
            Tiles = tiles
        };

        return JsonSerializer.Serialize(export, JsonOptions);
    }

    private class RegistryExportModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public List<CategoryExportModel> Categories { get; set; } = [];
        public List<TileExportModel> Tiles { get; set; } = [];
    }

    private class CategoryExportModel
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public bool ShowInPalette { get; set; }
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
