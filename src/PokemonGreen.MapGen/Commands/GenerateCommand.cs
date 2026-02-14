using PokemonGreen.MapGen.Services;

namespace PokemonGreen.MapGen.Commands;

public static class GenerateCommand
{
    /// <summary>
    /// Generates .g.cs files from .map.json files.
    /// </summary>
    public static void Run(string inputFolder, string? outputFolder = null)
    {
        inputFolder = Path.GetFullPath(inputFolder);

        if (!Directory.Exists(inputFolder))
        {
            Console.Error.WriteLine($"Error: Input folder does not exist: {inputFolder}");
            Environment.Exit(1);
        }

        // Default output: PokemonGreen.Core/Maps/Generated/ relative to MapGen project
        outputFolder ??= Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "PokemonGreen.Core", "Maps", "Generated"));
        outputFolder = Path.GetFullPath(outputFolder);

        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        Console.WriteLine($"Input folder: {inputFolder}");
        Console.WriteLine($"Output folder: {outputFolder}");
        Console.WriteLine();

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
        var generatedClassNames = new List<string>();

        foreach (var mapFile in mapFiles)
        {
            try
            {
                var map = MapParser.ParseJsonFile(mapFile);
                var className = ToPascalCase(map.MapId);

                var code = CodeGenerator.GenerateMapClass(map, className);

                var outputFile = Path.Combine(outputFolder, $"{className}.g.cs");
                File.WriteAllText(outputFile, code);

                Console.WriteLine($"Generated: {outputFile}");
                generatedClassNames.Add(className);
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
