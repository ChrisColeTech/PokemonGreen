using PokemonGreen.SwitchToolboxCli.Core.Loading;
using PokemonGreen.SwitchToolboxCli.App.Services;
using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Core.Extraction;

namespace PokemonGreen.SwitchToolboxCli.App.Commands;

public static class ExtractBinsCommand
{
    public static int Run(
        DiscoveryService discovery,
        ArchiveExtractionService extractionService,
        string inputPath,
        string outputDirectory,
        TextWriter stdout,
        TextWriter stderr)
    {
        return Run(discovery, extractionService, inputPath, outputDirectory, recursive: true, stdout, stderr);
    }

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
            stderr.WriteLine($"File not found: {inputPath}");
            return 1;
        }

        var loaded = loader.Open(inputPath);
        if (loaded is null)
        {
            stderr.WriteLine($"No supported format detected: {inputPath}");
            return 2;
        }

        if (loaded.Value.Value is not IArchiveFile archive)
        {
            stderr.WriteLine($"Detected format {loaded.Value.Format.FormatName} is not an archive: {inputPath}");
            return 2;
        }

        Directory.CreateDirectory(outputDirectory);

        var baseName = OutputPathService.SanitizeRelativePath(Path.GetFileName(inputPath));
        var targetDirectory = Path.Combine(outputDirectory, baseName);

        var extracted = extractionService.ExtractToDirectory(archive, targetDirectory);

        stdout.WriteLine($"Extract-bins complete. Extracted {extracted.Count} file(s) from {baseName}.");
        stdout.WriteLine($"Output: {Path.GetFullPath(outputDirectory)}");
        return 0;
    }

    public static int Run(
        DiscoveryService discovery,
        ArchiveExtractionService extractionService,
        string inputPath,
        string outputDirectory,
        bool recursive,
        TextWriter stdout,
        TextWriter stderr)
    {
        if (!recursive)
        {
            stderr.WriteLine("Non-recursive mode requires FileLoader. Use the overload with FileLoader parameter.");
            return 1;
        }

        var discovered = discovery.Discover(inputPath)
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.FormatName, StringComparer.Ordinal)
            .ToList();

        if (discovered.Count == 0)
        {
            stderr.WriteLine($"No supported format detected: {inputPath}");
            return 2;
        }

        var archives = discovered
            .Where(item => item.Value is IArchiveFile)
            .ToList();

        if (archives.Count == 0)
        {
            stderr.WriteLine($"No archive formats discovered for extraction: {inputPath}");
            return 2;
        }

        Directory.CreateDirectory(outputDirectory);

        var takenArchiveDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalExtractedFiles = 0;

        foreach (var archiveNode in archives)
        {
            var archive = (IArchiveFile)archiveNode.Value;
            var baseRelativePath = OutputPathService.SanitizeRelativePath(archiveNode.Path);
            var uniqueRelativePath = EnsureUniqueDirectoryPath(baseRelativePath, outputDirectory, takenArchiveDirs);
            var targetDirectory = Path.Combine(outputDirectory, uniqueRelativePath);

            var extracted = extractionService.ExtractToDirectory(archive, targetDirectory);
            totalExtractedFiles += extracted.Count;
        }

        stdout.WriteLine($"Extract-bins complete. Discovered {archives.Count} archive(s), extracted {totalExtractedFiles} file(s).");
        stdout.WriteLine($"Output: {Path.GetFullPath(outputDirectory)}");
        return 0;
    }

    private static string EnsureUniqueRelativePath(string basePath, HashSet<string> taken)
    {
        if (taken.Add(basePath))
        {
            return basePath;
        }

        var attempt = 1;
        while (true)
        {
            var candidate = $"{basePath}_{attempt:000}";
            if (taken.Add(candidate))
            {
                return candidate;
            }

            attempt++;
        }
    }

    private static string EnsureUniqueDirectoryPath(string basePath, string extractionRoot, HashSet<string> taken)
    {
        var candidate = EnsureUniqueRelativePath(basePath, taken);
        while (HasDirectoryConflict(Path.Combine(extractionRoot, candidate), extractionRoot))
        {
            candidate = EnsureUniqueRelativePath(basePath, taken);
        }

        return candidate;
    }

    private static bool HasDirectoryConflict(string directoryPath, string extractionRoot)
    {
        var fullPath = Path.GetFullPath(directoryPath);
        if (File.Exists(fullPath))
        {
            return true;
        }

        var root = Path.GetFullPath(extractionRoot);
        var parent = Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrWhiteSpace(parent) && parent.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(parent))
            {
                return true;
            }

            if (string.Equals(parent, root, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            parent = Path.GetDirectoryName(parent);
        }

        return false;
    }
}
