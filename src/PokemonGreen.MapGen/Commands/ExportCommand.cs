using PokemonGreen.MapGen.Models;
using PokemonGreen.MapGen.Services;

namespace PokemonGreen.MapGen.Commands;

public static class ExportCommand
{
    public static int Execute(string inputDirectory, string outputDirectory)
    {
        if (!Directory.Exists(inputDirectory))
        {
            Console.Error.WriteLine($"Input directory does not exist: {inputDirectory}");
            return 1;
        }

        Directory.CreateDirectory(outputDirectory);

        var mapFiles = Directory
            .EnumerateFiles(inputDirectory, "*.g.cs", SearchOption.TopDirectoryOnly)
            .Where(f => !Path.GetFileName(f).Contains("GeneratedMapCatalog"))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (mapFiles.Count == 0)
        {
            Console.Error.WriteLine($"No map files found in: {inputDirectory}");
            return 1;
        }

        var successCount = 0;
        foreach (var mapFile in mapFiles)
        {
            var fileName = Path.GetFileName(mapFile);
            var parseResult = MapClassParser.ParseFile(mapFile);

            if (!parseResult.IsSuccess)
            {
                Console.Error.WriteLine($"{fileName}: {parseResult.ErrorMessage}");
                continue;
            }

            var exportResult = MapExporter.ExportToJson(parseResult.Value, outputDirectory);

            if (!exportResult.IsSuccess)
            {
                Console.Error.WriteLine($"{fileName}: {exportResult.ErrorMessage}");
                continue;
            }

            Console.WriteLine($"Exported: {exportResult.Value}");
            successCount++;
        }

        Console.WriteLine($"Export complete. {successCount}/{mapFiles.Count} maps exported.");
        return successCount > 0 ? 0 : 1;
    }
}
