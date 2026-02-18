using PokemonGreen.SwitchToolboxCli.App.Commands;
using PokemonGreen.SwitchToolboxCli.App.Services;
using PokemonGreen.SwitchToolboxCli.Core.Extraction;
using PokemonGreen.SwitchToolboxCli.Core.Loading;
using PokemonGreen.SwitchToolboxCli.Formats.Archives.Pokemon;
using PokemonGreen.SwitchToolboxCli.Formats.Archives.Rom3DS;
using PokemonGreen.SwitchToolboxCli.Formats.Archives.TRPAK;
using PokemonGreen.SwitchToolboxCli.Formats.Archives.TRPFS;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Animations;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Models;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Textures;

var argsList = args.ToList();
if (argsList.Count == 0)
{
    PrintHelp();
    return 1;
}

var registry = BuildRegistry();
var loader = new FileLoader(registry);
var walker = new ArchiveWalker(loader);
var discovery = new DiscoveryService(walker);
var extractor = new ArchiveExtractionService();
var manifestWriter = new ManifestFileWriter();
var textureExporter = new TextureArchiveExportService();
var clipExporter = new AnimationClipArchiveExportService();
var modelExporter = new ModelArchiveExportService();
var trinityAssemblyService = new TrinityModelAssemblyService();

var command = argsList[0].ToLowerInvariant();
var commandArgs = argsList.Skip(1).ToArray();

switch (command)
{
    case "info":
        if (commandArgs.Length < 1)
        {
            Console.Error.WriteLine("Missing input path for info.");
            PrintInfoUsage();
            return 1;
        }

        return InfoCommand.Run(walker, commandArgs[0], Console.Out, Console.Error);

    case "scan":
        if (!CliArguments.TryParse(commandArgs, out var scanArgs, out var scanParseError))
        {
            Console.Error.WriteLine(scanParseError);
            PrintScanUsage();
            return 1;
        }

        if (!scanArgs.TryGetRequiredOption("-o", out var scanOutput, out var scanOptionError) ||
            !scanArgs.EnsureOnlyOptions(["-o"], out scanOptionError))
        {
            Console.Error.WriteLine(scanOptionError);
            PrintScanUsage();
            return 1;
        }

        return ScanCommand.Run(discovery, scanArgs.Input, scanOutput, Console.Out, Console.Error);

    case "extract-garc":
        if (!CliArguments.TryParse(commandArgs, out var extractArgs, out var extractParseError))
        {
            Console.Error.WriteLine(extractParseError);
            PrintExtractGarcUsage();
            return 1;
        }

        if (!extractArgs.TryGetRequiredOption("-o", out var extractOutputDir, out var extractOptionError) ||
            !extractArgs.EnsureOnlyOptions(["-o"], out extractOptionError))
        {
            Console.Error.WriteLine(extractOptionError);
            PrintExtractGarcUsage();
            return 1;
        }

        return ExtractGarcCommand.Run(loader, extractor, extractArgs.Input, extractOutputDir, Console.Out, Console.Error);

    case "extract-bins":
        if (!CliArguments.TryParse(commandArgs, out var extractBinsArgs, out var extractBinsParseError))
        {
            Console.Error.WriteLine(extractBinsParseError);
            PrintExtractBinsUsage();
            return 1;
        }

        if (!extractBinsArgs.TryGetRequiredOption("-o", out var extractBinsOutputDir, out var extractBinsOptionError) ||
            !extractBinsArgs.EnsureOnlyOptions(["-o"], out extractBinsOptionError))
        {
            Console.Error.WriteLine(extractBinsOptionError);
            PrintExtractBinsUsage();
            return 1;
        }

        return ExtractBinsCommand.Run(loader, extractor, extractBinsArgs.Input, extractBinsOutputDir, Console.Out, Console.Error);

    case "batch":
        if (!CliArguments.TryParse(commandArgs, out var batchArgs, out var batchParseError))
        {
            Console.Error.WriteLine(batchParseError);
            PrintBatchUsage();
            return 1;
        }

        if (!batchArgs.TryGetRequiredOption("-o", out var batchOutputDir, out var batchOptionError) ||
            !batchArgs.EnsureOnlyOptions(["-o", "--model-format"], out batchOptionError))
        {
            Console.Error.WriteLine(batchOptionError);
            PrintBatchUsage();
            return 1;
        }

        var batchModelFormat = "obj";
        if (batchArgs.TryGetOption("--model-format", out var parsedBatchModelFormat, out var batchModelFormatError))
        {
            batchModelFormat = parsedBatchModelFormat;
        }
        else if (!string.IsNullOrEmpty(batchModelFormatError))
        {
            Console.Error.WriteLine(batchModelFormatError);
            PrintBatchUsage();
            return 1;
        }

        return BatchCommand.Run(discovery, extractor, manifestWriter, modelExporter, textureExporter, clipExporter, batchArgs.Input, batchOutputDir, batchModelFormat, Console.Out, Console.Error);

    case "convert":
        if (!CliArguments.TryParse(commandArgs, out var convertArgs, out var convertParseError))
        {
            Console.Error.WriteLine(convertParseError);
            PrintConvertUsage();
            return 1;
        }

        if (!convertArgs.TryGetRequiredOption("-o", out var convertOutputDir, out var convertOptionError) ||
            !convertArgs.TryGetRequiredOption("--model-format", out var modelFormat, out convertOptionError) ||
            !convertArgs.EnsureOnlyOptions(["-o", "--model-format", "--extract"], out convertOptionError))
        {
            Console.Error.WriteLine(convertOptionError);
            PrintConvertUsage();
            return 1;
        }

        var extractArchives = false;
        if (convertArgs.TryGetOption("--extract", out var extractOptionValue, out var convertExtractOptionError))
        {
            if (!bool.TryParse(extractOptionValue, out extractArchives))
            {
                Console.Error.WriteLine("Invalid value for --extract. Expected true or false.");
                PrintConvertUsage();
                return 1;
            }
        }
        else if (!string.IsNullOrEmpty(convertExtractOptionError))
        {
            Console.Error.WriteLine(convertExtractOptionError);
            PrintConvertUsage();
            return 1;
        }

        return ConvertCommand.Run(discovery, extractor, manifestWriter, modelExporter, trinityAssemblyService, textureExporter, clipExporter, convertArgs.Input, convertOutputDir, modelFormat, extractArchives, Console.Out, Console.Error);

    case "bulk-convert":
        if (!CliArguments.TryParse(commandArgs, out var bulkConvertArgs, out var bulkConvertParseError))
        {
            Console.Error.WriteLine(bulkConvertParseError);
            PrintBulkConvertUsage();
            return 1;
        }

        if (!bulkConvertArgs.TryGetRequiredOption("-o", out var bulkConvertOutputDir, out var bulkConvertOptionError) ||
            !bulkConvertArgs.TryGetRequiredOption("--model-format", out var bulkConvertModelFormat, out bulkConvertOptionError) ||
            !bulkConvertArgs.EnsureOnlyOptions(["-o", "--model-format", "--limit", "--resume"], out bulkConvertOptionError))
        {
            Console.Error.WriteLine(bulkConvertOptionError);
            PrintBulkConvertUsage();
            return 1;
        }

        int? bulkConvertLimit = null;
        if (bulkConvertArgs.TryGetOption("--limit", out var bulkConvertLimitValue, out var bulkConvertLimitError))
        {
            if (!int.TryParse(bulkConvertLimitValue, out var parsedBulkConvertLimit) || parsedBulkConvertLimit <= 0)
            {
                Console.Error.WriteLine("Invalid value for --limit. Expected a positive integer.");
                PrintBulkConvertUsage();
                return 1;
            }

            bulkConvertLimit = parsedBulkConvertLimit;
        }
        else if (!string.IsNullOrEmpty(bulkConvertLimitError))
        {
            Console.Error.WriteLine(bulkConvertLimitError);
            PrintBulkConvertUsage();
            return 1;
        }

        var bulkConvertResume = false;
        if (bulkConvertArgs.TryGetOption("--resume", out var bulkConvertResumeValue, out var bulkConvertResumeError))
        {
            if (!bool.TryParse(bulkConvertResumeValue, out bulkConvertResume))
            {
                Console.Error.WriteLine("Invalid value for --resume. Expected true or false.");
                PrintBulkConvertUsage();
                return 1;
            }
        }
        else if (!string.IsNullOrEmpty(bulkConvertResumeError))
        {
            Console.Error.WriteLine(bulkConvertResumeError);
            PrintBulkConvertUsage();
            return 1;
        }

        return BulkConvertCommand.Run(
            discovery,
            extractor,
            manifestWriter,
            modelExporter,
            trinityAssemblyService,
            textureExporter,
            clipExporter,
            bulkConvertArgs.Input,
            bulkConvertOutputDir,
            bulkConvertModelFormat,
            bulkConvertLimit,
            bulkConvertResume,
            Console.Out,
            Console.Error);

    case "bulk-extract-bins":
        if (!CliArguments.TryParse(commandArgs, out var bulkExtractBinsArgs, out var bulkExtractBinsParseError))
        {
            Console.Error.WriteLine(bulkExtractBinsParseError);
            PrintBulkExtractBinsUsage();
            return 1;
        }

        if (!bulkExtractBinsArgs.TryGetRequiredOption("-o", out var bulkExtractBinsOutputDir, out var bulkExtractBinsOptionError) ||
            !bulkExtractBinsArgs.EnsureOnlyOptions(["-o", "--limit", "--resume"], out bulkExtractBinsOptionError))
        {
            Console.Error.WriteLine(bulkExtractBinsOptionError);
            PrintBulkExtractBinsUsage();
            return 1;
        }

        int? bulkExtractBinsLimit = null;
        if (bulkExtractBinsArgs.TryGetOption("--limit", out var bulkExtractBinsLimitValue, out var bulkExtractBinsLimitError))
        {
            if (!int.TryParse(bulkExtractBinsLimitValue, out var parsedBulkExtractBinsLimit) || parsedBulkExtractBinsLimit <= 0)
            {
                Console.Error.WriteLine("Invalid value for --limit. Expected a positive integer.");
                PrintBulkExtractBinsUsage();
                return 1;
            }

            bulkExtractBinsLimit = parsedBulkExtractBinsLimit;
        }
        else if (!string.IsNullOrEmpty(bulkExtractBinsLimitError))
        {
            Console.Error.WriteLine(bulkExtractBinsLimitError);
            PrintBulkExtractBinsUsage();
            return 1;
        }

        var bulkExtractBinsResume = false;
        if (bulkExtractBinsArgs.TryGetOption("--resume", out var bulkExtractBinsResumeValue, out var bulkExtractBinsResumeError))
        {
            if (!bool.TryParse(bulkExtractBinsResumeValue, out bulkExtractBinsResume))
            {
                Console.Error.WriteLine("Invalid value for --resume. Expected true or false.");
                PrintBulkExtractBinsUsage();
                return 1;
            }
        }
        else if (!string.IsNullOrEmpty(bulkExtractBinsResumeError))
        {
            Console.Error.WriteLine(bulkExtractBinsResumeError);
            PrintBulkExtractBinsUsage();
            return 1;
        }

        return BulkExtractBinsCommand.Run(
            discovery,
            extractor,
            bulkExtractBinsArgs.Input,
            bulkExtractBinsOutputDir,
            bulkExtractBinsLimit,
            bulkExtractBinsResume,
            Console.Out,
            Console.Error);

    default:
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 1;
}

static FormatRegistry BuildRegistry()
{
    var registry = new FormatRegistry();
    registry.Register(new TrpfsFormat());
    registry.Register(new GarcFormat());
    registry.Register(new TrpakFormat());
    registry.Register(new NcsdFormat());
    registry.Register(new NcchFormat());
    registry.Register(new RomFsFormat());
    return registry;
}

static void PrintHelp()
{
    Console.WriteLine("PokemonGreen.SwitchToolboxCli");
    Console.WriteLine("Usage:");
    Console.WriteLine("  info <input>");
    Console.WriteLine("  scan <input> -o <report.json>");
    Console.WriteLine("  extract-garc <input.garc> -o <outDir>");
    Console.WriteLine("  extract-bins <input> -o <outDir>");
    Console.WriteLine("  bulk-extract-bins <inputDir> -o <outDir> [--limit N] [--resume true|false]");
    Console.WriteLine("  batch <input> -o <outDir> [--model-format obj|dae]");
    Console.WriteLine("  convert <input> -o <outDir> --model-format obj|dae [--extract true|false]");
    Console.WriteLine("  bulk-convert <inputDir> -o <outDir> --model-format obj|dae [--limit N] [--resume true|false]");
}

static void PrintInfoUsage() => Console.Error.WriteLine("Usage: info <input>");
static void PrintScanUsage() => Console.Error.WriteLine("Usage: scan <input> -o <report.json>");
static void PrintExtractGarcUsage() => Console.Error.WriteLine("Usage: extract-garc <input.garc> -o <outDir>");
static void PrintExtractBinsUsage() => Console.Error.WriteLine("Usage: extract-bins <input> -o <outDir>");
static void PrintBulkExtractBinsUsage() => Console.Error.WriteLine("Usage: bulk-extract-bins <inputDir> -o <outDir> [--limit N] [--resume true|false]");
static void PrintBatchUsage() => Console.Error.WriteLine("Usage: batch <input> -o <outDir> [--model-format obj|dae]");
static void PrintConvertUsage() => Console.Error.WriteLine("Usage: convert <input> -o <outDir> --model-format obj|dae [--extract true|false]");
static void PrintBulkConvertUsage() => Console.Error.WriteLine("Usage: bulk-convert <inputDir> -o <outDir> --model-format obj|dae [--limit N] [--resume true|false]");
