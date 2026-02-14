using System.Text.Json;
using System.Text.RegularExpressions;
using PokemonGreen.MapGen.Models;

namespace PokemonGreen.MapGen.Commands;

public static partial class ExportCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Exports .g.cs files back to .map.json format.
    /// </summary>
    /// <param name="inputFolder">Folder containing .g.cs files.</param>
    /// <param name="outputFolder">Output folder for .map.json files.</param>
    public static void Run(string inputFolder, string? outputFolder = null)
    {
        // Resolve paths
        inputFolder = Path.GetFullPath(inputFolder);

        if (!Directory.Exists(inputFolder))
        {
            Console.Error.WriteLine($"Error: Input folder does not exist: {inputFolder}");
            Environment.Exit(1);
        }

        // Default output folder is the same as input
        outputFolder ??= inputFolder;
        outputFolder = Path.GetFullPath(outputFolder);

        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        Console.WriteLine($"Input folder: {inputFolder}");
        Console.WriteLine($"Output folder: {outputFolder}");
        Console.WriteLine();

        // Find all .g.cs files that look like map definitions
        var csFiles = Directory.GetFiles(inputFolder, "*Map.g.cs", SearchOption.AllDirectories);

        if (csFiles.Length == 0)
        {
            Console.WriteLine("No *Map.g.cs files found.");
            return;
        }

        Console.WriteLine($"Found {csFiles.Length} generated map file(s).");
        Console.WriteLine();

        int successCount = 0;
        int errorCount = 0;

        foreach (var csFile in csFiles)
        {
            try
            {
                var code = File.ReadAllText(csFile);

                // Parse the generated C# file
                var map = ParseGeneratedCode(code);

                if (map == null)
                {
                    Console.WriteLine($"Skipped (not a valid map definition): {csFile}");
                    continue;
                }

                // Derive output file name from the map name
                var fileName = ToSnakeCase(map.Name.Replace(" ", ""));
                var outputFile = Path.Combine(outputFolder, $"{fileName}.map.json");

                var json = JsonSerializer.Serialize(map, JsonOptions);
                File.WriteAllText(outputFile, json);

                Console.WriteLine($"Exported: {outputFile}");
                successCount++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing {csFile}: {ex.Message}");
                errorCount++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Done. Exported {successCount} file(s), {errorCount} error(s).");
    }

    /// <summary>
    /// Parses a generated C# map file and extracts the map data.
    /// </summary>
    private static MapJsonModel? ParseGeneratedCode(string code)
    {
        // Extract Name property
        var nameMatch = NameRegex().Match(code);
        if (!nameMatch.Success)
            return null;
        var name = nameMatch.Groups[1].Value;

        // Extract Width property
        var widthMatch = WidthRegex().Match(code);
        if (!widthMatch.Success)
            return null;
        var width = int.Parse(widthMatch.Groups[1].Value);

        // Extract Height property
        var heightMatch = HeightRegex().Match(code);
        if (!heightMatch.Success)
            return null;
        var height = int.Parse(heightMatch.Groups[1].Value);

        // Initialize tiles array with zeros
        var tiles = new int[height][];
        for (int y = 0; y < height; y++)
        {
            tiles[y] = new int[width];
        }

        // Extract all SetBaseTile calls
        var tileMatches = SetBaseTileRegex().Matches(code);
        foreach (Match match in tileMatches)
        {
            var x = int.Parse(match.Groups[1].Value);
            var y = int.Parse(match.Groups[2].Value);
            var tileId = int.Parse(match.Groups[3].Value);

            if (y >= 0 && y < height && x >= 0 && x < width)
            {
                tiles[y][x] = tileId;
            }
        }

        return new MapJsonModel
        {
            Version = 1,
            Name = name,
            Width = width,
            Height = height,
            Tiles = tiles
        };
    }

    /// <summary>
    /// Converts PascalCase to snake_case.
    /// </summary>
    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new System.Text.StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }

    [GeneratedRegex(@"override\s+string\s+Name\s*=>\s*""([^""]+)""")]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"override\s+int\s+Width\s*=>\s*(\d+)")]
    private static partial Regex WidthRegex();

    [GeneratedRegex(@"override\s+int\s+Height\s*=>\s*(\d+)")]
    private static partial Regex HeightRegex();

    [GeneratedRegex(@"map\.SetBaseTile\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)")]
    private static partial Regex SetBaseTileRegex();
}
