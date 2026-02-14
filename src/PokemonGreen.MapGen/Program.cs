using PokemonGreen.MapGen.Commands;

namespace PokemonGreen.MapGen;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "generate":
                HandleGenerateCommand(args[1..]);
                break;

            case "export":
                HandleExportCommand(args[1..]);
                break;

            case "export-registry":
                HandleExportRegistryCommand(args[1..]);
                break;

            case "--help":
            case "-h":
            case "help":
                PrintUsage();
                break;

            default:
                Console.Error.WriteLine($"Unknown command: {command}");
                Console.WriteLine();
                PrintUsage();
                Environment.Exit(1);
                break;
        }
    }

    private static void HandleGenerateCommand(string[] args)
    {
        string? inputFolder = null;
        string? outputFolder = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input":
                case "-i":
                    if (i + 1 < args.Length)
                    {
                        inputFolder = args[++i];
                    }
                    break;

                case "--output":
                case "-o":
                    if (i + 1 < args.Length)
                    {
                        outputFolder = args[++i];
                    }
                    break;
            }
        }

        if (string.IsNullOrEmpty(inputFolder))
        {
            Console.Error.WriteLine("Error: --input <folder> is required for generate command.");
            Environment.Exit(1);
        }

        GenerateCommand.Run(inputFolder, outputFolder);
    }

    private static void HandleExportCommand(string[] args)
    {
        string? inputFolder = null;
        string? outputFolder = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input":
                case "-i":
                    if (i + 1 < args.Length)
                    {
                        inputFolder = args[++i];
                    }
                    break;

                case "--output":
                case "-o":
                    if (i + 1 < args.Length)
                    {
                        outputFolder = args[++i];
                    }
                    break;
            }
        }

        if (string.IsNullOrEmpty(inputFolder))
        {
            Console.Error.WriteLine("Error: --input <folder> is required for export command.");
            Environment.Exit(1);
        }

        ExportCommand.Run(inputFolder, outputFolder);
    }

    private static void HandleExportRegistryCommand(string[] args)
    {
        string? outputPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--output":
                case "-o":
                    if (i + 1 < args.Length)
                    {
                        outputPath = args[++i];
                    }
                    break;
            }
        }

        if (string.IsNullOrEmpty(outputPath))
        {
            Console.Error.WriteLine("Error: --output <file> is required for export-registry command.");
            Environment.Exit(1);
        }

        ExportRegistryCommand.Run(outputPath);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("PokemonGreen.MapGen - Map generation and export tool");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run -- generate --input <folder> [--output <folder>]");
        Console.WriteLine("      Generate C# map classes from .map.json files.");
        Console.WriteLine("      Output defaults to PokemonGreen.Core/Maps/");
        Console.WriteLine();
        Console.WriteLine("  dotnet run -- export --input <folder> [--output <folder>]");
        Console.WriteLine("      Export .g.cs map files back to .map.json format.");
        Console.WriteLine("      Output defaults to the input folder.");
        Console.WriteLine();
        Console.WriteLine("  dotnet run -- export-registry --output <file>");
        Console.WriteLine("      Export TileRegistry to JSON format.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --input, -i    Input folder path");
        Console.WriteLine("  --output, -o   Output folder/file path");
        Console.WriteLine("  --help, -h     Show this help message");
    }
}
