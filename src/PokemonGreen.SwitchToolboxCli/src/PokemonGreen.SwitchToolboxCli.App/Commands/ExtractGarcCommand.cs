using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Core.Extraction;
using PokemonGreen.SwitchToolboxCli.Core.Loading;

namespace PokemonGreen.SwitchToolboxCli.App.Commands;

public static class ExtractGarcCommand
{
    public static int Run(
        FileLoader loader,
        ArchiveExtractionService extractionService,
        string inputPath,
        string outputDirectory,
        TextWriter stdout,
        TextWriter stderr)
    {
        if (!File.Exists(inputPath))
        {
            stderr.WriteLine($"Input file not found: {inputPath}");
            return 2;
        }

        var opened = loader.Open(inputPath);
        if (opened is null || !string.Equals(opened.Value.Format.FormatName, "GARC", StringComparison.Ordinal))
        {
            stderr.WriteLine($"Input is not a detected GARC archive: {inputPath}");
            return 2;
        }

        if (opened.Value.Value is not IArchiveFile archive)
        {
            stderr.WriteLine($"Detected GARC did not expose archive entries: {inputPath}");
            return 2;
        }

        var extracted = extractionService.ExtractToDirectory(archive, outputDirectory);
        stdout.WriteLine($"Extracted {extracted.Count} file(s) to {Path.GetFullPath(outputDirectory)}");
        return 0;
    }
}
