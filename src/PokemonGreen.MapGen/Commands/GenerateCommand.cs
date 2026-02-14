using PokemonGreen.MapGen.Services;

namespace PokemonGreen.MapGen.Commands;

public static class GenerateCommand
{
    /// <summary>
    /// Generates .g.cs files from .map.json files.
    /// </summary>
    /// <param name="inputFolder">Folder containing .map.json files.</param>
    /// <param name="outputFolder">Output folder for generated .g.cs files. Defaults to PokemonGreen.Core/Maps/.</param>
    public static void Run(string inputFolder, string? outputFolder = null)
    {
        // Resolve paths
        inputFolder = Path.GetFullPath(inputFolder);

        if (!Directory.Exists(inputFolder))
        {
            Console.Error.WriteLine($"Error: Input folder does not exist: {inputFolder}");
            Environment.Exit(1);
        }

        // Default output folder is PokemonGreen.Core/Maps/ relative to the MapGen project
        outputFolder ??= Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "PokemonGreen.Core", "Maps"));
        outputFolder = Path.GetFullPath(outputFolder);

        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        Console.WriteLine($"Input folder: {inputFolder}");
        Console.WriteLine($"Output folder: {outputFolder}");
        Console.WriteLine();

        // Find all .map.json files
        var mapFiles = Directory.GetFiles(inputFolder, "*.map.json", SearchOption.AllDirectories);

        if (mapFiles.Length == 0)
        {
            Console.WriteLine("No .map.json files found.");
            return;
        }

        Console.WriteLine($"Found {mapFiles.Length} map file(s).");
        Console.WriteLine();

        int successCount = 0;
        int errorCount = 0;

        foreach (var mapFile in mapFiles)
        {
            try
            {
                // Parse the JSON file
                var map = MapParser.ParseJsonFile(mapFile);

                // Derive class name from file name (remove .map.json, convert to PascalCase)
                var fileName = Path.GetFileNameWithoutExtension(mapFile);
                if (fileName.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
                {
                    fileName = fileName[..^4]; // Remove .map
                }
                var className = ToPascalCase(fileName);

                // Generate the code
                var code = CodeGenerator.GenerateMapClass(map, className);

                // Write the output file
                var outputFile = Path.Combine(outputFolder, $"{className}Map.g.cs");
                File.WriteAllText(outputFile, code);

                Console.WriteLine($"Generated: {outputFile}");
                successCount++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing {mapFile}: {ex.Message}");
                errorCount++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Done. Generated {successCount} file(s), {errorCount} error(s).");
    }

    /// <summary>
    /// Converts a string to PascalCase.
    /// </summary>
    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new System.Text.StringBuilder();
        bool capitalizeNext = true;

        foreach (var c in input)
        {
            if (c == '_' || c == '-' || c == ' ')
            {
                capitalizeNext = true;
            }
            else if (capitalizeNext)
            {
                result.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
