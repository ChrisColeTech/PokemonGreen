using PokemonGreen.Core.Maps;
using PokemonGreen.MapGen.Models;

namespace PokemonGreen.MapGen.Services;

public static class MapExporter
{
    public static Result<string> ExportToJson(MapData map, string outputDirectory)
    {
        if (map.BaseTiles.Length != map.Width * map.Height)
        {
            return Result<string>.Fail($"BaseTiles length mismatch for map {map.MapId}");
        }

        if (map.OverlayTiles.Length != map.Width * map.Height)
        {
            return Result<string>.Fail($"OverlayTiles length mismatch for map {map.MapId}");
        }

        var baseTiles2D = To2D(map.BaseTiles, map.Width, map.Height);
        var overlayTiles2D = ToNullable2D(map.OverlayTiles, map.Width, map.Height);
        var tileTypes = BuildTileTypesExport();

        var payload = new MapJsonPayload
        {
            SchemaVersion = 2,
            MapId = map.MapId,
            DisplayName = map.DisplayName,
            TileSize = map.TileSize,
            Width = map.Width,
            Height = map.Height,
            BaseTiles = baseTiles2D,
            OverlayTiles = overlayTiles2D,
            TileTypes = tileTypes
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        var fileName = $"{map.MapId}.map.json";
        var outputPath = Path.Combine(outputDirectory, fileName);
        File.WriteAllText(outputPath, json);

        return Result<string>.Ok(fileName);
    }

    private static Dictionary<int, TileTypeExport> BuildTileTypesExport()
    {
        var result = new Dictionary<int, TileTypeExport>();
        foreach (var tile in TileRegistry.List)
        {
            result[tile.Id] = TileTypeExport.FromDefinition(tile);
        }
        return result;
    }

    private static int[][] To2D(int[] flat, int width, int height)
    {
        var result = new int[height][];
        for (var y = 0; y < height; y++)
        {
            result[y] = new int[width];
            for (var x = 0; x < width; x++)
            {
                result[y][x] = flat[y * width + x];
            }
        }
        return result;
    }

    private static int?[][] ToNullable2D(int?[] flat, int width, int height)
    {
        var result = new int?[height][];
        for (var y = 0; y < height; y++)
        {
            result[y] = new int?[width];
            for (var x = 0; x < width; x++)
            {
                result[y][x] = flat[y * width + x];
            }
        }
        return result;
    }
}
