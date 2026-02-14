using PokemonGreen.MapGen.Services;

namespace PokemonGreen.MapGen.Commands;

public static class ExportRegistryCommand
{
    /// <summary>
    /// Exports the TileRegistry to a JSON file.
    /// </summary>
    /// <param name="outputPath">Path to the output JSON file.</param>
    public static void Run(string outputPath)
    {
        outputPath = Path.GetFullPath(outputPath);

        Console.WriteLine($"Exporting TileRegistry to: {outputPath}");

        try
        {
            var json = RegistryExporter.ExportToJson();

            // Ensure the directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, json);

            Console.WriteLine("Done.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
