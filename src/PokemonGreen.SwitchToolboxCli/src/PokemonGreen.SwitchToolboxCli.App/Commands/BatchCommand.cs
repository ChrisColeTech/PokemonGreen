using PokemonGreen.SwitchToolboxCli.App.Services;
using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Core.Extraction;
using PokemonGreen.SwitchToolboxCli.Core.Models;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Animations;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Models;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Textures;

namespace PokemonGreen.SwitchToolboxCli.App.Commands;

public static class BatchCommand
{
    private static readonly HashSet<string> AllowedModelFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "obj",
        "dae",
    };

    public static int Run(
        DiscoveryService discovery,
        ArchiveExtractionService extractionService,
        ManifestFileWriter manifestWriter,
        ModelArchiveExportService modelExportService,
        TextureArchiveExportService textureExportService,
        AnimationClipArchiveExportService clipExportService,
        string inputPath,
        string outputDirectory,
        string modelFormat,
        TextWriter stdout,
        TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(manifestWriter);

        if (!AllowedModelFormats.Contains(modelFormat))
        {
            stderr.WriteLine($"Unsupported model format '{modelFormat}'. Allowed values: obj, dae");
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

        var extractionRoot = Path.Combine(outputDirectory, "extracted");
        Directory.CreateDirectory(extractionRoot);
        var texturesRoot = Path.Combine(outputDirectory, "textures");
        Directory.CreateDirectory(texturesRoot);
        var clipsRoot = Path.Combine(outputDirectory, "clips");
        Directory.CreateDirectory(clipsRoot);
        var modelsRoot = Path.Combine(outputDirectory, "models");
        Directory.CreateDirectory(modelsRoot);

        var takenArchiveDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var archiveReports = new List<ArchiveExtractionReport>();

        foreach (var archiveNode in discovered.Where(item => item.Value is IArchiveFile))
        {
            var archive = (IArchiveFile)archiveNode.Value;
            var baseRelativeDir = OutputPathService.SanitizeRelativePath(archiveNode.Path);
            var uniqueRelativeDir = EnsureUniqueDirectoryPath(baseRelativeDir, extractionRoot, takenArchiveDirs);
            var targetDir = Path.Combine(extractionRoot, uniqueRelativeDir);

            var extracted = extractionService.ExtractToDirectory(archive, targetDir);
            archiveReports.Add(new ArchiveExtractionReport(archiveNode.Path, archiveNode.FormatName, uniqueRelativeDir.Replace('\\', '/'), extracted.Count));
        }

        var textureArchives = discovered
            .Where(item => item.Value is IArchiveFile)
            .Select(item => new TextureArchiveSource(item.Path, (IArchiveFile)item.Value))
            .ToList();
        var textureExports = textureExportService.ExportTextureLikeEntries(textureArchives, texturesRoot);

        var modelArchives = discovered
            .Where(item => item.Value is IArchiveFile)
            .Select(item => new ModelArchiveSource(item.Path, (IArchiveFile)item.Value))
            .ToList();
        var modelExports = modelExportService.ExportModelLikeEntries(modelArchives, modelsRoot, modelFormat);

        var clipArchives = discovered
            .Where(item => item.Value is IArchiveFile)
            .Select(item => new AnimationClipArchiveSource(item.Path, (IArchiveFile)item.Value))
            .ToList();
        var clipExports = clipExportService.ExportClipLikeEntries(clipArchives, clipsRoot);

        var modelStatus = modelExports.Count > 0 ? "completed" : "pending";

        var manifest = new BatchManifest(
            Path.GetFullPath(inputPath),
            Path.GetFullPath(outputDirectory),
            modelFormat.ToLowerInvariant(),
            modelStatus,
            discovered.Select(item => new ManifestDetectedItem(item.Path, item.FormatName)).ToList(),
            archiveReports,
            modelExports.Select(item => new ModelManifestItem(item.ArchivePath, item.EntryFileName, item.OutputRelativePath, item.OutputFormat, item.ExportKind)).ToList(),
            textureExports.Select(item => new TextureManifestItem(item.ArchivePath, item.EntryFileName, item.OutputRelativePath, item.DetectedExtension)).ToList(),
            clipExports.Select(item => new ClipManifestItem(item.ArchivePath, item.EntryFileName, item.OutputRelativePath, item.DetectedExtension)).ToList());

        var manifestPath = Path.Combine(outputDirectory, "batch-manifest.json");
        manifestWriter.WriteBatchManifest(manifestPath, manifest);

        stdout.WriteLine($"Batch complete. Discovered {discovered.Count} item(s), extracted {archiveReports.Sum(item => item.ExtractedFileCount)} file(s), exported {modelExports.Count} model file(s), exported {textureExports.Count} texture file(s), exported {clipExports.Count} clip file(s).");
        stdout.WriteLine($"Manifest: {Path.GetFullPath(manifestPath)}");
        stdout.WriteLine($"Model conversion status: {modelStatus}.");
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
