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
    /// Parses a .map.json file and returns a MapJsonModel.
    /// </summary>
    /// <param name="path">Path to the .map.json file.</param>
    /// <returns>The parsed map model.</returns>
    public static MapJsonModel ParseJsonFile(string path)
    {
        var json = File.ReadAllText(path);
        var model = JsonSerializer.Deserialize<MapJsonModel>(json, JsonOptions);

        if (model == null)
        {
            throw new InvalidDataException($"Failed to parse map file: {path}");
        }

        return model;
    }
}
