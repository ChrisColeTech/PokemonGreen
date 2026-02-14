using System.Text.Json;
using System.Text.Json.Serialization;
using PokemonGreen.Core.Maps;
using PokemonGreen.MapGen.Models;

namespace PokemonGreen.MapGen.Commands;

public static class ExportRegistryCommand
{
    private const string RegistryId = "pokemon-green-default";
    private const string RegistryName = "Pokemon Green Default Registry";
    private const string RegistryVersion = "1.0.0";
    private static readonly IReadOnlyList<BuildingTemplate> BuildingTemplates =
    [
        // Temporary source for Phase 1 parity with `tools/map-editor/src/data/buildings.ts`.
        // Building definitions will move to a shared source in a later phase.
        new("pokecenter", "Pokecenter", [[3, 3, 3, 3], [3, 4, 4, 3], [3, 4, 4, 3], [6, 4, 4, 6]], 0),
        new("pokemart", "Pokemart", [[3, 3, 3, 3], [3, 6, 6, 3], [3, 11, 6, 3], [6, 4, 4, 6]], 1),
        new("gym", "Gym", [[3, 3, 3, 3, 3], [3, 6, 6, 6, 3], [3, 6, 12, 6, 3], [3, 6, 4, 6, 3], [6, 6, 4, 6, 6]], 2),
        new("house-small", "House Small", [[3, 3, 3], [3, 4, 3], [6, 4, 6]], 3),
        new("house-large", "House Large", [[3, 3, 3, 3], [3, 6, 6, 3], [3, 4, 6, 3], [6, 4, 6, 6]], 4),
        new("lab", "Lab", [[3, 3, 3, 3, 3], [3, 6, 6, 6, 3], [3, 4, 41, 4, 3], [6, 4, 4, 4, 6]], 5),
        new("cave-entrance", "Cave Entrance", [[3, 3, 3], [15, 15, 15]], 6),
        new("gate", "Gate", [[6, 6, 6, 6], [6, 16, 16, 6], [6, 6, 6, 6]], 7),
        new("pond", "Pond", [[17, 0, 0, 17], [0, 0, 0, 0], [17, 0, 0, 17]], 8),
        new("fence-h", "Fence H", [[18, 18, 18, 18]], 9),
        new("fence-v", "Fence V", [[18], [18], [18], [18]], 10),
    ];

    public static int Execute(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.Error.WriteLine("Output path is required.");
            return 1;
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var payload = new RegistryJsonPayload
        {
            Metadata = new RegistryMetadataPayload
            {
                Id = RegistryId,
                Name = RegistryName,
                Version = RegistryVersion,
            },
            Categories = BuildCategories(),
            Tiles = BuildTiles(),
            Buildings = BuildBuildings(),
        };

        var validationErrors = ValidateBuildingTileReferences(payload.Buildings, payload.Tiles);
        if (validationErrors.Count > 0)
        {
            foreach (var validationError in validationErrors)
            {
                Console.Error.WriteLine(validationError);
            }

            return 1;
        }

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });

        File.WriteAllText(fullOutputPath, json);
        Console.WriteLine($"Exported registry JSON: {fullOutputPath}");
        Console.WriteLine($"Categories: {payload.Categories.Count}, Tiles: {payload.Tiles.Count}, Buildings: {payload.Buildings.Count}");
        return 0;
    }

    private static List<RegistryCategoryPayload> BuildCategories()
    {
        return Enum
            .GetValues<TileCategory>()
            .Select(category => new RegistryCategoryPayload
            {
                Id = ToCategoryId(category),
                Label = ToCategoryLabel(category),
                ShowInPalette = category is TileCategory.Terrain
                    or TileCategory.Encounter
                    or TileCategory.Interactive
                    or TileCategory.Entity
                    or TileCategory.Trainer,
            })
            .OrderBy(category => category.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static List<RegistryTilePayload> BuildTiles()
    {
        return TileRegistry.List
            .OrderBy(tile => tile.Id)
            .Select(tile => new RegistryTilePayload
            {
                Id = tile.Id,
                Name = tile.Name,
                Color = ToHexColor(tile.Red, tile.Green, tile.Blue),
                Walkable = tile.Walkable,
                Category = ToCategoryId(tile.Category),
                Direction = ToDirection(tile.OverlayKind),
                IsOverlay = IsOverlayTile(tile),
            })
            .ToList();
    }

    private static List<RegistryBuildingPayload> BuildBuildings() => BuildingTemplates
        .OrderBy(template => template.SortOrder)
        .ThenBy(template => template.Id, StringComparer.Ordinal)
        .Select(template => new RegistryBuildingPayload
        {
            Id = template.Id,
            Name = template.Name,
            Tiles = template.Tiles,
        })
        .ToList();

    private static List<string> ValidateBuildingTileReferences(
        IReadOnlyList<RegistryBuildingPayload> buildings,
        IReadOnlyList<RegistryTilePayload> tiles)
    {
        var knownTileIds = tiles.Select(tile => tile.Id).ToHashSet();
        var errors = new List<string>();

        foreach (var building in buildings)
        {
            for (var y = 0; y < building.Tiles.Length; y += 1)
            {
                var row = building.Tiles[y];
                for (var x = 0; x < row.Length; x += 1)
                {
                    var tileId = row[x];
                    if (tileId is null || knownTileIds.Contains(tileId.Value))
                    {
                        continue;
                    }

                    errors.Add($"Building '{building.Id}' has unknown tile id {tileId.Value} at ({x}, {y}).");
                }
            }
        }

        return errors;
    }

    private static string ToCategoryId(TileCategory category) => category.ToString().ToLowerInvariant();

    private static string ToCategoryLabel(TileCategory category) => category switch
    {
        TileCategory.Encounter => "Encounters",
        TileCategory.Entity => "Entities",
        TileCategory.Trainer => "Trainers",
        TileCategory.Item => "Items",
        _ => category.ToString(),
    };

    private static string? ToDirection(TileOverlayKind overlayKind) => overlayKind switch
    {
        TileOverlayKind.TrainerUp => "up",
        TileOverlayKind.TrainerDown => "down",
        TileOverlayKind.TrainerLeft => "left",
        TileOverlayKind.TrainerRight => "right",
        _ => null,
    };

    private static bool? IsOverlayTile(TileDefinition tile)
    {
        var isOverlay = tile.Category is TileCategory.Decoration or TileCategory.Item || tile.OverlayKind != TileOverlayKind.None;
        return isOverlay ? true : null;
    }

    private static string ToHexColor(byte red, byte green, byte blue) => $"#{red:x2}{green:x2}{blue:x2}";

    private sealed record BuildingTemplate(string Id, string Name, int?[][] Tiles, int SortOrder);
}
