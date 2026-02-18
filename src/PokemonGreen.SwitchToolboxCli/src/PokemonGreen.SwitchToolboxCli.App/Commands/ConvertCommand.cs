using PokemonGreen.SwitchToolboxCli.App.Services;
using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Core.Extraction;
using PokemonGreen.SwitchToolboxCli.Core.Models;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Animations;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Models;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Textures;

namespace PokemonGreen.SwitchToolboxCli.App.Commands;

public static class ConvertCommand
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
        TrinityModelAssemblyService trinityAssemblyService,
        TextureArchiveExportService textureExportService,
        AnimationClipArchiveExportService clipExportService,
        string inputPath,
        string outputDirectory,
        string modelFormat,
        bool extractArchives,
        TextWriter stdout,
        TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(manifestWriter);
        ArgumentNullException.ThrowIfNull(trinityAssemblyService);

        if (!AllowedModelFormats.Contains(modelFormat))
        {
            stderr.WriteLine($"Unsupported model format '{modelFormat}'. Allowed values: obj, dae");
            return 1;
        }

        var discovered = discovery.Discover(inputPath)
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.FormatName, StringComparer.Ordinal)
            .ToList();

        // Force GC after discovery to clean up any accumulated memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var trinityArchiveSources = discovered
            .Where(item => item.Value is IArchiveFile &&
                           (item.FormatName.Equals("TRPAK", StringComparison.OrdinalIgnoreCase) ||
                            item.FormatName.Equals("TRPFS", StringComparison.OrdinalIgnoreCase)))
            .Select(item => new TrinityArchiveSource(item.Path, (IArchiveFile)item.Value))
            .ToList();
        var trinityBundles = trinityArchiveSources.Count == 0
            ? Array.Empty<TrinityIndexedBundle>()
            : new TrinityBinIndexerService().IndexArchives(trinityArchiveSources).ToArray();

        if (discovered.Count == 0)
        {
            stderr.WriteLine($"No supported format detected: {inputPath}");
            return 2;
        }

        var texturesRoot = Path.Combine(outputDirectory, "textures");
        var clipsRoot = Path.Combine(outputDirectory, "clips");
        var modelsRoot = Path.Combine(outputDirectory, "models");

        var archiveReports = new List<ArchiveExtractionReport>();
        var extractionRoot = Path.Combine(outputDirectory, "extracted");
        if (extractArchives)
        {
            Directory.CreateDirectory(extractionRoot);
            var takenArchiveDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var archiveNode in discovered.Where(item => item.Value is IArchiveFile))
            {
                var archive = (IArchiveFile)archiveNode.Value;
                var baseRelativePath = OutputPathService.SanitizeRelativePath(archiveNode.Path);
                var uniqueRelativePath = EnsureUniqueDirectoryPath(baseRelativePath, extractionRoot, takenArchiveDirs);
                var targetDir = Path.Combine(extractionRoot, uniqueRelativePath);

                var extracted = extractionService.ExtractToDirectory(archive, targetDir);
                archiveReports.Add(new ArchiveExtractionReport(archiveNode.Path, archiveNode.FormatName, uniqueRelativePath.Replace('\\', '/'), extracted.Count));

                // Force GC after each large archive extraction to prevent memory buildup
                if (extracted.Count > 100)
                {
                    GC.Collect();
                }
            }

            CleanupIfEmpty(extractionRoot);

            // Force full GC after all extractions
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        var trinityArchivePathSet = new HashSet<string>(trinityArchiveSources.Select(item => item.ArchivePath), StringComparer.Ordinal);

        var textureArchives = discovered
            .Where(item => item.Value is IArchiveFile)
            .Where(item => !trinityArchivePathSet.Contains(item.Path))
            .Select(item => new TextureArchiveSource(item.Path, (IArchiveFile)item.Value))
            .ToList();
        var textureExports = textureExportService.ExportTextureLikeEntries(textureArchives, texturesRoot);
        CleanupIfEmpty(texturesRoot);

        // Release texture archive references
        textureArchives.Clear();

        var modelArchives = discovered
            .Where(item => item.Value is IArchiveFile)
            .Where(item => !trinityArchivePathSet.Contains(item.Path))
            .Select(item => new ModelArchiveSource(item.Path, (IArchiveFile)item.Value))
            .ToList();
        var modelExports = modelExportService.ExportModelLikeEntries(modelArchives, modelsRoot, modelFormat);

        // Release model archive references before Trinity processing
        modelArchives.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var trinityAssembly = trinityAssemblyService.AssembleBundles(trinityBundles, trinityArchiveSources, modelsRoot, texturesRoot, clipsRoot, modelFormat);

        // Release Trinity references after assembly
        trinityArchiveSources.Clear();
        trinityBundles = Array.Empty<TrinityIndexedBundle>();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        textureExports = textureExports
            .Concat(trinityAssembly.Textures.Select(item => new TextureExportedFile(
                item.ArchivePath,
                item.EntryFileName,
                item.OutputRelativePath,
                item.DetectedExtension)))
            .ToList();

        var clipArchives = discovered
            .Where(item => item.Value is IArchiveFile)
            .Where(item => !trinityArchivePathSet.Contains(item.Path))
            .Select(item => new AnimationClipArchiveSource(item.Path, (IArchiveFile)item.Value))
            .ToList();
        var clipExports = clipExportService.ExportClipLikeEntries(clipArchives, clipsRoot)
            .Concat(trinityAssembly.Clips.Select(item => new AnimationClipExportedFile(
                item.ArchivePath,
                item.EntryFileName,
                item.OutputRelativePath,
                item.DetectedExtension)))
            .ToList();

        var manifestModelItems = modelExports
            .Select(item => new ModelManifestItem(
                item.ArchivePath,
                item.EntryFileName,
                item.OutputRelativePath,
                item.OutputFormat,
                item.ExportKind,
                null,
                "completed"))
            .Concat(trinityAssembly.Models.Select(item => new ModelManifestItem(
                item.ArchivePath,
                item.EntryFileName,
                item.OutputRelativePath,
                item.OutputFormat,
                item.ExportKind,
                item.BundleKey,
                item.Status,
                item.DetailMessage)))
            .ToList();

        var completedModelCount = manifestModelItems.Where(item => string.Equals(item.BundleStatus, "completed", StringComparison.OrdinalIgnoreCase)).Count();

        CleanupIfEmpty(texturesRoot);
        CleanupIfEmpty(modelsRoot);
        CleanupIfEmpty(clipsRoot);

        var modelStatus = ResolveModelStatus(manifestModelItems);

        if (completedModelCount == 0)
        {
            var pendingCount = manifestModelItems.Where(item => string.Equals(item.BundleStatus, "pending_conversion", StringComparison.OrdinalIgnoreCase)).Count();
            var failedCount = manifestModelItems.Where(item => string.Equals(item.BundleStatus, "failed", StringComparison.OrdinalIgnoreCase)).Count();

            stdout.WriteLine($"Convert complete. No {modelFormat.ToUpperInvariant()} model outputs were produced. Skipping manifest.");
            stdout.WriteLine($"Discovered {discovered.Count} item(s), exported {textureExports.Count} texture file(s), exported {clipExports.Count} clip file(s). Extract archives: {extractArchives}. Trinity bundles: {trinityBundles.Length}, pending: {pendingCount}, failed: {failedCount}.");
            WriteTrinityIndexReport(stdout, trinityArchiveSources.Count, trinityBundles);
            return 0;
        }

        var manifest = new ConvertManifest(
            Path.GetFullPath(inputPath),
            Path.GetFullPath(outputDirectory),
            modelFormat.ToLowerInvariant(),
            modelStatus,
            discovered.Select(item => new ManifestDetectedItem(item.Path, item.FormatName)).ToList(),
            archiveReports,
            manifestModelItems,
            textureExports.Select(item => new TextureManifestItem(item.ArchivePath, item.EntryFileName, item.OutputRelativePath, item.DetectedExtension)).ToList(),
            clipExports.Select(item => new ClipManifestItem(item.ArchivePath, item.EntryFileName, item.OutputRelativePath, item.DetectedExtension)).ToList());

        var manifestPath = Path.Combine(outputDirectory, "manifest.json");
        manifestWriter.WriteConvertManifest(manifestPath, manifest);

        stdout.WriteLine($"Convert foundation complete. Discovered {discovered.Count} item(s), extracted {archiveReports.Sum(item => item.ExtractedFileCount)} file(s), exported {completedModelCount} model file(s), exported {textureExports.Count} texture file(s), exported {clipExports.Count} clip file(s).");
        WriteTrinityIndexReport(stdout, trinityArchiveSources.Count, trinityBundles);
        stdout.WriteLine($"Manifest: {Path.GetFullPath(manifestPath)}");
        stdout.WriteLine($"Model conversion status: {modelStatus}.");
        return 0;
    }

    private static string ResolveModelStatus(IReadOnlyList<ModelManifestItem> modelItems)
    {
        if (modelItems.Count == 0)
        {
            return "pending_conversion";
        }

        if (modelItems.Any(item => string.Equals(item.BundleStatus, "failed", StringComparison.OrdinalIgnoreCase)))
        {
            return "failed";
        }

        if (modelItems.Any(item => string.Equals(item.BundleStatus, "pending_conversion", StringComparison.OrdinalIgnoreCase)))
        {
            return "pending_conversion";
        }

        return "completed";
    }

    private static void WriteTrinityIndexReport(TextWriter stdout, int sourceArchiveCount, IReadOnlyList<TrinityIndexedBundle> bundles)
    {
        if (sourceArchiveCount == 0)
        {
            return;
        }

        var clipRefCount = bundles.Sum(bundle => bundle.ClipReferences.Count);
        stdout.WriteLine($"Trinity indexing foundation: grouped {bundles.Count} bundle(s) from {sourceArchiveCount} TRPAK/TRPFS archive(s), with {clipRefCount} clip reference(s).");
    }

    private static void CleanupIfEmpty(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        if (Directory.EnumerateFileSystemEntries(directory).Any())
        {
            return;
        }

        Directory.Delete(directory);
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
