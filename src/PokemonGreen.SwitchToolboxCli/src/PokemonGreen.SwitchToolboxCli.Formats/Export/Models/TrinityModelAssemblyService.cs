using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Core.Models;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Animations;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Dae;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Models.Trinity;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Obj;

namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Models;

public sealed class TrinityModelAssemblyService
{
    private static readonly HashSet<string> SupportedOutputFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "obj",
        "dae",
    };

    private static readonly HashSet<TrinityEntryRole> PendingConversionRoles =
    [
        TrinityEntryRole.ModelContainer,
        TrinityEntryRole.Model,
        TrinityEntryRole.Mesh,
        TrinityEntryRole.Skeleton,
        TrinityEntryRole.Material,
        TrinityEntryRole.BlendShape,
    ];

    private static readonly string[] TrinityClipExtensions =
    [
        ".tranm",
        ".traef",
        ".tracm",
        ".tracs",
        ".tracl",
        ".tracr",
        ".tracp",
    ];

    private static readonly HashSet<string> RawOnlyClipExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".traef",
        ".tracm",
        ".tracs",
        ".tracl",
        ".tracr",
        ".tracp",
    };

    private readonly ObjExporter _objExporter;
    private readonly DaeExporter _daeExporter;
    private readonly TrinityStaticMeshDecoder _trinityDecoder;
    private readonly TrinityArmatureBuilder _armatureBuilder;
    private readonly TrinityContainerReferenceResolver _containerResolver;
    private readonly TrinityAnimationLocator _animationLocator;
    private readonly TrinityAnimationDecoder _animationDecoder;
    private readonly TrinityAnimationDaeClipWriter _animationDaeClipWriter;

    public TrinityModelAssemblyService()
        : this(
            new ObjExporter(),
            new DaeExporter(),
            new TrinityStaticMeshDecoder(),
            new TrinityArmatureBuilder(),
            new TrinityContainerReferenceResolver(),
            new TrinityAnimationLocator(),
            new TrinityAnimationDecoder(),
            new TrinityAnimationDaeClipWriter())
    {
    }

    public TrinityModelAssemblyService(
        ObjExporter objExporter,
        DaeExporter daeExporter,
        TrinityStaticMeshDecoder trinityDecoder,
        TrinityArmatureBuilder armatureBuilder,
        TrinityContainerReferenceResolver containerResolver,
        TrinityAnimationLocator animationLocator,
        TrinityAnimationDecoder animationDecoder,
        TrinityAnimationDaeClipWriter animationDaeClipWriter)
    {
        _objExporter = objExporter;
        _daeExporter = daeExporter;
        _trinityDecoder = trinityDecoder;
        _armatureBuilder = armatureBuilder;
        _containerResolver = containerResolver;
        _animationLocator = animationLocator;
        _animationDecoder = animationDecoder;
        _animationDaeClipWriter = animationDaeClipWriter;
    }

    public TrinityAssemblyReport AssembleBundles(
        IReadOnlyList<TrinityIndexedBundle> bundles,
        IReadOnlyList<TrinityArchiveSource> archives,
        string modelsOutputDirectory,
        string texturesOutputDirectory,
        string clipsOutputDirectory,
        string requestedModelFormat)
    {
        ArgumentNullException.ThrowIfNull(bundles);
        ArgumentNullException.ThrowIfNull(archives);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelsOutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(texturesOutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(clipsOutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedModelFormat);

        if (!SupportedOutputFormats.Contains(requestedModelFormat))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedModelFormat), requestedModelFormat, "Expected 'obj' or 'dae'.");
        }

        var archiveLookup = archives.ToDictionary(item => item.ArchivePath, StringComparer.Ordinal);
        var modelOutputRoot = Path.GetFullPath(modelsOutputDirectory);
        var textureOutputRoot = Path.GetFullPath(texturesOutputDirectory);
        var clipOutputRoot = Path.GetFullPath(clipsOutputDirectory);

        Directory.CreateDirectory(modelOutputRoot);
        Directory.CreateDirectory(textureOutputRoot);
        Directory.CreateDirectory(clipOutputRoot);

        var usedModelPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedTexturePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedClipPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var models = new List<TrinityAssembledModel>();
        var textures = new List<TrinityAssembledTexture>();
        var clips = new List<TrinityAssembledClip>();

        foreach (var bundle in bundles
                     .OrderBy(item => item.SourceArchivePath, StringComparer.Ordinal)
                     .ThenBy(item => item.LogicalModelKey, StringComparer.Ordinal))
        {
            if (!archiveLookup.TryGetValue(bundle.SourceArchivePath, out var sourceArchive))
            {
                models.Add(new TrinityAssembledModel(
                    bundle.SourceArchivePath.Replace('\\', '/'),
                    bundle.LogicalModelKey,
                    "failed",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "bundle-missing-archive",
                    "Failed to assemble bundle because source archive was not available."));
                continue;
            }

            var entryLookup = BuildEntryLookup(sourceArchive.Archive);
            var availableArchiveEntries = sourceArchive.Archive.Files
                .OrderBy(entry => entry.FileName, StringComparer.Ordinal)
                .ToList();
            var bundleDirectory = BuildBundleDirectory(bundle.SourceArchivePath, bundle.LogicalModelKey);
            var containerResolution = _containerResolver.Resolve(
                bundle,
                availableArchiveEntries,
                path => TryReadEntryPayload(entryLookup, path, out var payload, out _) ? payload : null);
            var bundleProducedModel = false;
            ExportArmature? bundleArmature = null;

            foreach (var directModelEntry in bundle.Entries.Where(entry => entry.Role == TrinityEntryRole.DirectModel))
            {
                if (!TryGetArchiveEntry(entryLookup, directModelEntry.EntryPath, out var archiveEntry))
                {
                    models.Add(new TrinityAssembledModel(
                        bundle.SourceArchivePath.Replace('\\', '/'),
                        bundle.LogicalModelKey,
                        "failed",
                        directModelEntry.EntryPath,
                        string.Empty,
                        directModelEntry.Extension.TrimStart('.'),
                        "pass-through",
                        "Failed to locate source model entry in archive."));
                    continue;
                }

                var outputRelativePath = BuildUniqueBundlePath(
                    bundleDirectory,
                    Path.GetFileName(directModelEntry.EntryPath),
                    directModelEntry.Extension,
                    modelOutputRoot,
                    usedModelPaths);

                try
                {
                    var destinationPath = Path.Combine(modelOutputRoot, outputRelativePath);
                    using var sourceStream = archiveEntry.OpenRead();
                    using var destinationStream = File.Create(destinationPath);
                    sourceStream.CopyTo(destinationStream);

                    models.Add(new TrinityAssembledModel(
                        bundle.SourceArchivePath.Replace('\\', '/'),
                        bundle.LogicalModelKey,
                        "completed",
                        directModelEntry.EntryPath,
                        outputRelativePath.Replace('\\', '/'),
                        directModelEntry.Extension.TrimStart('.'),
                        "pass-through",
                        null));

                    bundleProducedModel = true;
                }
                catch (IOException ex)
                {
                    models.Add(new TrinityAssembledModel(
                        bundle.SourceArchivePath.Replace('\\', '/'),
                        bundle.LogicalModelKey,
                        "failed",
                        directModelEntry.EntryPath,
                        string.Empty,
                        directModelEntry.Extension.TrimStart('.'),
                        "pass-through",
                        ex.Message));
                }
            }

            var needsPendingPlaceholder = bundle.Entries.Any(entry => PendingConversionRoles.Contains(entry.Role));
            if (!bundleProducedModel && needsPendingPlaceholder)
            {
                if (TryDecodeAndExportNativeBundle(
                        bundle,
                        containerResolution,
                        entryLookup,
                        modelOutputRoot,
                        usedModelPaths,
                        requestedModelFormat,
                        out var decodedModel,
                        out var decodedArmature,
                        out var decodeDetail))
                {
                    models.Add(decodedModel);
                    bundleProducedModel = true;
                    bundleArmature = decodedArmature;
                }
                else
                {
                    var unresolvedDetail = containerResolution.UnresolvedReferences.Count == 0
                        ? string.Empty
                        : $" Unresolved container refs: {string.Join(", ", containerResolution.UnresolvedReferences)}.";

                    models.Add(new TrinityAssembledModel(
                        bundle.SourceArchivePath.Replace('\\', '/'),
                        bundle.LogicalModelKey,
                        "pending_conversion",
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        "trinity-native-static-mesh",
                        decodeDetail + unresolvedDetail));
                }
            }

            if (bundleArmature is null)
            {
                TryResolveBundleArmature(bundle, containerResolution, entryLookup, out bundleArmature);
            }

            var clipPaths = bundle.ClipReferences
                .Select(item => item.EntryPath)
                .Concat(TrinityClipExtensions.SelectMany(containerResolution.GetResolvedPaths))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            foreach (var clipPath in clipPaths)
            {
                if (!TryGetArchiveEntry(entryLookup, clipPath, out var archiveEntry))
                {
                    clips.Add(new TrinityAssembledClip(
                        bundle.SourceArchivePath.Replace('\\', '/'),
                        bundle.LogicalModelKey,
                        clipPath,
                        string.Empty,
                        Path.GetExtension(clipPath).ToLowerInvariant(),
                        "missing_source"));
                    continue;
                }

                var clipExtension = Path.GetExtension(clipPath).ToLowerInvariant();
                byte[] payload;
                try
                {
                    payload = ReadArchiveEntryPayload(archiveEntry);
                }
                catch
                {
                    clips.Add(new TrinityAssembledClip(
                        bundle.SourceArchivePath.Replace('\\', '/'),
                        bundle.LogicalModelKey,
                        clipPath,
                        string.Empty,
                        clipExtension,
                        "write_failed"));
                    continue;
                }

                if (bundleArmature is not null &&
                    !RawOnlyClipExtensions.Contains(clipExtension) &&
                    _animationLocator.TryLocate(clipPath, payload, out var located) &&
                    _animationDecoder.TryDecode(located, bundleArmature, out var decodedClip, out _))
                {
                    var clipOutputRelativePath = BuildUniqueBundlePath(
                        Path.Combine(bundleDirectory, "clips"),
                        Path.GetFileNameWithoutExtension(clipPath),
                        ".dae",
                        clipOutputRoot,
                        usedClipPaths);

                    try
                    {
                        var destinationPath = Path.Combine(clipOutputRoot, clipOutputRelativePath);
                        _animationDaeClipWriter.Write(destinationPath, bundleArmature, decodedClip);

                        clips.Add(new TrinityAssembledClip(
                            bundle.SourceArchivePath.Replace('\\', '/'),
                            bundle.LogicalModelKey,
                            clipPath,
                            clipOutputRelativePath.Replace('\\', '/'),
                            ".dae",
                            "dae-decoded"));

                        continue;
                    }
                    catch (IOException)
                    {
                        // Fall back to raw sidecar when DAE clip write fails.
                    }
                }

                var rawClipOutputRelativePath = BuildUniqueBundlePath(
                    Path.Combine(bundleDirectory, "clips"),
                    Path.GetFileName(clipPath),
                    clipExtension,
                    clipOutputRoot,
                    usedClipPaths);

                try
                {
                    var destinationPath = Path.Combine(clipOutputRoot, rawClipOutputRelativePath);
                    File.WriteAllBytes(destinationPath, payload);

                    clips.Add(new TrinityAssembledClip(
                        bundle.SourceArchivePath.Replace('\\', '/'),
                        bundle.LogicalModelKey,
                        clipPath,
                        rawClipOutputRelativePath.Replace('\\', '/'),
                        clipExtension,
                        "raw"));
                }
                catch (IOException)
                {
                    clips.Add(new TrinityAssembledClip(
                        bundle.SourceArchivePath.Replace('\\', '/'),
                        bundle.LogicalModelKey,
                        clipPath,
                        string.Empty,
                        clipExtension,
                        "write_failed"));
                }
            }

            var texturePaths = bundle.Entries
                .Where(entry => entry.Role == TrinityEntryRole.Texture)
                .Select(entry => entry.EntryPath)
                .Concat(containerResolution.GetResolvedPaths(".bntx"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            foreach (var texturePath in texturePaths)
            {
                if (!TryGetArchiveEntry(entryLookup, texturePath, out var archiveEntry))
                {
                    textures.Add(new TrinityAssembledTexture(
                        bundle.SourceArchivePath.Replace('\\', '/'),
                        bundle.LogicalModelKey,
                        texturePath,
                        string.Empty,
                        Path.GetExtension(texturePath).ToLowerInvariant(),
                        "missing_source"));
                    continue;
                }

                var textureExtension = Path.GetExtension(texturePath).ToLowerInvariant();
                var textureOutputRelativePath = BuildUniqueBundlePath(
                    Path.Combine(bundleDirectory, "textures"),
                    Path.GetFileName(texturePath),
                    textureExtension,
                    textureOutputRoot,
                    usedTexturePaths);

                try
                {
                    var destinationPath = Path.Combine(textureOutputRoot, textureOutputRelativePath);
                    using var sourceStream = archiveEntry.OpenRead();
                    using var destinationStream = File.Create(destinationPath);
                    sourceStream.CopyTo(destinationStream);

                    textures.Add(new TrinityAssembledTexture(
                        bundle.SourceArchivePath.Replace('\\', '/'),
                        bundle.LogicalModelKey,
                        texturePath,
                        textureOutputRelativePath.Replace('\\', '/'),
                        textureExtension,
                        "raw"));
                }
                catch (IOException)
                {
                    textures.Add(new TrinityAssembledTexture(
                        bundle.SourceArchivePath.Replace('\\', '/'),
                        bundle.LogicalModelKey,
                        texturePath,
                        string.Empty,
                        textureExtension,
                        "write_failed"));
                }
            }
        }

        return new TrinityAssemblyReport(models, textures, clips);
    }

    private bool TryDecodeAndExportNativeBundle(
        TrinityIndexedBundle bundle,
        TrinityContainerResolveResult containerResolution,
        IDictionary<string, Queue<ArchiveEntry>> entryLookup,
        string modelOutputRoot,
        HashSet<string> usedModelPaths,
        string requestedModelFormat,
        out TrinityAssembledModel assembledModel,
        out ExportArmature? assembledArmature,
        out string detail)
    {
        assembledModel = null!;
        assembledArmature = null;
        detail = string.Empty;

        var trmdlPreferredPaths = containerResolution.GetResolvedPaths(".trmdl");
        var trmdlEntry = SelectEntry(bundle, ".trmdl", trmdlPreferredPaths);
        if (trmdlEntry is null)
        {
            detail = "Missing required Trinity bin: .trmdl";
            return false;
        }

        if (!TryReadEntryPayload(entryLookup, trmdlEntry.EntryPath, out var trmdlPayload, out detail))
        {
            return false;
        }

        string? preferredMeshPath = null;
        string? preferredBufferPath = null;
        TrinityModelRoot? modelRoot = null;
        if (_trinityDecoder.TryReadModelReferences(trmdlPayload, out var parsedModelRoot))
        {
            modelRoot = parsedModelRoot;
            preferredMeshPath = modelRoot.Meshes?.FirstOrDefault()?.MeshPath;
            preferredBufferPath = modelRoot.Meshes?.FirstOrDefault()?.BufferPath;
        }

        var trmshPreferredPaths = BuildPreferredPaths(
            preferredMeshPath,
            containerResolution.GetResolvedPaths(".trmsh"));
        var trmshEntry = SelectEntry(bundle, ".trmsh", trmshPreferredPaths);
        if (trmshEntry is null)
        {
            detail = "Missing required Trinity bin: .trmsh";
            return false;
        }

        if (!TryReadEntryPayload(entryLookup, trmshEntry.EntryPath, out var trmshPayload, out detail))
        {
            return false;
        }

        var trmbfPreferredPaths = BuildPreferredPaths(
            preferredBufferPath,
            containerResolution.GetResolvedPaths(".trmbf"));
        var trmbfEntry = SelectEntry(bundle, ".trmbf", trmbfPreferredPaths);
        if (trmbfEntry is null)
        {
            detail = "Missing required Trinity bin: .trmbf";
            return false;
        }

        if (!TryReadEntryPayload(entryLookup, trmbfEntry.EntryPath, out var trmbfPayload, out detail))
        {
            return false;
        }

        if (!_trinityDecoder.TryDecode(
                trmdlPayload,
                trmshPayload,
                trmbfPayload,
                Path.GetFileNameWithoutExtension(trmdlEntry.EntryPath),
                out var exportModel,
                out detail))
        {
            return false;
        }

        var armatureDiagnostics = string.Empty;
        var preferredSkeletonPath = modelRoot?.SkeletonReferences?.FirstOrDefault();
        var trsklPreferredPaths = BuildPreferredPaths(
            preferredSkeletonPath,
            containerResolution.GetResolvedPaths(".trskl"));
        var trsklEntry = SelectEntry(bundle, ".trskl", trsklPreferredPaths);
        string trsklReadDetail = string.Empty;
        if (trsklEntry is not null &&
            TryReadEntryPayload(entryLookup, trsklEntry.EntryPath, out var trsklPayload, out trsklReadDetail))
        {
            if (_armatureBuilder.TryBuild(
                    trsklPayload,
                    Path.GetFileNameWithoutExtension(trsklEntry.EntryPath),
                    out var armature,
                    out var armatureDetail))
            {
                assembledArmature = armature;
                exportModel = new ExportModel
                {
                    ModelName = exportModel.ModelName,
                    Meshes = exportModel.Meshes,
                    Armature = armature,
                };
            }
            else if (!string.IsNullOrWhiteSpace(armatureDetail))
            {
                armatureDiagnostics = $"TRSKL fallback: {armatureDetail}";
            }
        }
        else if (!string.IsNullOrWhiteSpace(trsklReadDetail))
        {
            armatureDiagnostics = $"TRSKL fallback: {trsklReadDetail}";
        }

        var bundleDirectory = BuildBundleDirectory(bundle.SourceArchivePath, bundle.LogicalModelKey);
        var fileExtension = "." + requestedModelFormat.ToLowerInvariant();
        var fileName = Path.GetFileNameWithoutExtension(trmdlEntry.EntryPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "model";
        }

        var outputRelativePath = BuildUniqueBundlePath(
            bundleDirectory,
            fileName,
            fileExtension,
            modelOutputRoot,
            usedModelPaths);

        var outputPath = Path.Combine(modelOutputRoot, outputRelativePath);
        try
        {
            if (requestedModelFormat.Equals("obj", StringComparison.OrdinalIgnoreCase))
            {
                _objExporter.Export(outputPath, exportModel);
            }
            else
            {
                _daeExporter.Export(outputPath, exportModel);
            }
        }
        catch (IOException ex)
        {
            detail = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            detail = $"Failed to export decoded Trinity mesh: {ex.Message}";
            return false;
        }

        assembledModel = new TrinityAssembledModel(
            bundle.SourceArchivePath.Replace('\\', '/'),
            bundle.LogicalModelKey,
            "completed",
            trmdlEntry.EntryPath,
            outputRelativePath.Replace('\\', '/'),
            requestedModelFormat.ToLowerInvariant(),
            "trinity-native-static-mesh",
            string.IsNullOrWhiteSpace(armatureDiagnostics) ? null : armatureDiagnostics);
        detail = string.Empty;
        return true;
    }

    private static TrinityBundleEntry? SelectEntry(TrinityIndexedBundle bundle, string extension, IReadOnlyList<string> preferredPaths)
    {
        foreach (var preferredPath in preferredPaths)
        {
            var normalizedPreferredPath = NormalizeArchiveEntryPath(preferredPath);
            var preferredBundleEntry = bundle.Entries.FirstOrDefault(entry =>
                entry.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase) &&
                (NormalizeArchiveEntryPath(entry.EntryPath).Equals(normalizedPreferredPath, StringComparison.OrdinalIgnoreCase) ||
                 NormalizeArchiveEntryPath(entry.EntryPath).EndsWith("/" + Path.GetFileName(normalizedPreferredPath), StringComparison.OrdinalIgnoreCase)));
            if (preferredBundleEntry is not null)
            {
                return preferredBundleEntry;
            }

            if (Path.GetExtension(normalizedPreferredPath).Equals(extension, StringComparison.OrdinalIgnoreCase))
            {
                return new TrinityBundleEntry(
                    normalizedPreferredPath,
                    extension.ToLowerInvariant(),
                    TrinityEntryRole.Model,
                    IsSniffed: false,
                    DetailMessage: "container-reference");
            }
        }

        var extensionMatches = bundle.Entries
            .Where(entry => entry.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (extensionMatches.Count == 0)
        {
            return null;
        }

        return extensionMatches
            .OrderBy(entry => entry.EntryPath, StringComparer.Ordinal)
            .First();
    }

    private static IReadOnlyList<string> BuildPreferredPaths(string? first, IReadOnlyList<string> rest)
    {
        var preferred = new List<string>();
        if (!string.IsNullOrWhiteSpace(first))
        {
            preferred.Add(first);
        }

        preferred.AddRange(rest);
        return preferred
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryReadEntryPayload(
        IDictionary<string, Queue<ArchiveEntry>> entryLookup,
        string entryPath,
        out byte[] payload,
        out string detail)
    {
        payload = Array.Empty<byte>();
        detail = string.Empty;

        if (!TryGetArchiveEntry(entryLookup, entryPath, out var archiveEntry))
        {
            detail = $"Missing required Trinity bin entry: {entryPath}";
            return false;
        }

        try
        {
            using var stream = archiveEntry.OpenRead();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            payload = memory.ToArray();
            return true;
        }
        catch (IOException ex)
        {
            detail = $"Failed to read Trinity bin '{entryPath}': {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            detail = $"Failed to read Trinity bin '{entryPath}': {ex.Message}";
            return false;
        }
    }

    private bool TryResolveBundleArmature(
        TrinityIndexedBundle bundle,
        TrinityContainerResolveResult containerResolution,
        IDictionary<string, Queue<ArchiveEntry>> entryLookup,
        out ExportArmature? armature)
    {
        armature = null;

        var trsklEntry = SelectEntry(bundle, ".trskl", containerResolution.GetResolvedPaths(".trskl"));
        if (trsklEntry is null)
        {
            return false;
        }

        if (!TryReadEntryPayload(entryLookup, trsklEntry.EntryPath, out var trsklPayload, out _))
        {
            return false;
        }

        if (!_armatureBuilder.TryBuild(
                trsklPayload,
                Path.GetFileNameWithoutExtension(trsklEntry.EntryPath),
                out var builtArmature,
                out _))
        {
            return false;
        }

        armature = builtArmature;
        return true;
    }

    private static byte[] ReadArchiveEntryPayload(ArchiveEntry entry)
    {
        using var stream = entry.OpenRead();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static string BuildBundleDirectory(string archivePath, string bundleKey)
    {
        var archiveDirectory = NormalizeRelativePath(archivePath, "archive");
        var bundleDirectory = NormalizeRelativePath(bundleKey, "bundle");
        return Path.Combine(archiveDirectory, bundleDirectory);
    }

    private static Dictionary<string, Queue<ArchiveEntry>> BuildEntryLookup(IArchiveFile archive)
    {
        var lookup = new Dictionary<string, Queue<ArchiveEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Files.OrderBy(item => item.FileName, StringComparer.Ordinal))
        {
            var key = NormalizeArchiveEntryPath(entry.FileName);
            if (!lookup.TryGetValue(key, out var queue))
            {
                queue = new Queue<ArchiveEntry>();
                lookup.Add(key, queue);
            }

            queue.Enqueue(entry);
        }

        return lookup;
    }

    private static bool TryGetArchiveEntry(IDictionary<string, Queue<ArchiveEntry>> lookup, string entryPath, out ArchiveEntry entry)
    {
        var normalizedPath = NormalizeArchiveEntryPath(entryPath);
        if (lookup.TryGetValue(normalizedPath, out var queue) && queue.Count > 0)
        {
            entry = queue.Dequeue();
            return true;
        }

        entry = null!;
        return false;
    }

    private static string BuildUniqueBundlePath(
        string bundleDirectory,
        string fileName,
        string extension,
        string outputRoot,
        HashSet<string> usedRelativePaths)
    {
        var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "unnamed" : SanitizeSegment(fileName);
        if (!Path.HasExtension(safeFileName) && !string.IsNullOrWhiteSpace(extension))
        {
            safeFileName += extension;
        }

        var candidate = Path.Combine(bundleDirectory, safeFileName);
        var uniqueRelativePath = EnsureUniqueRelativePath(candidate, usedRelativePaths);
        var destinationPath = Path.GetFullPath(Path.Combine(outputRoot, uniqueRelativePath));
        if (!IsSubPath(destinationPath, outputRoot))
        {
            throw new InvalidOperationException($"Refused to write Trinity output outside output root: {fileName}");
        }

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        return uniqueRelativePath;
    }

    private static string EnsureUniqueRelativePath(string candidate, HashSet<string> usedPaths)
    {
        if (usedPaths.Add(candidate))
        {
            return candidate;
        }

        var extension = Path.GetExtension(candidate);
        var stem = extension.Length == 0
            ? candidate
            : candidate[..^extension.Length];

        var suffix = 1;
        while (true)
        {
            var withSuffix = $"{stem}_{suffix:000}{extension}";
            if (usedPaths.Add(withSuffix))
            {
                return withSuffix;
            }

            suffix++;
        }
    }

    private static bool IsSubPath(string candidatePath, string rootPath)
    {
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;

        return candidatePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(candidatePath, rootPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeArchiveEntryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "unnamed.bin";
        }

        var segments = path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment != "." && segment != "..")
            .ToList();

        return segments.Count == 0 ? "unnamed.bin" : string.Join('/', segments);
    }

    private static string NormalizeRelativePath(string path, string fallbackName)
    {
        var segments = path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment != "." && segment != "..")
            .Select(SanitizeSegment)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        return segments.Count == 0
            ? fallbackName
            : Path.Combine(segments.ToArray());
    }

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars).Trim();
    }
}

public sealed record TrinityAssemblyReport(
    IReadOnlyList<TrinityAssembledModel> Models,
    IReadOnlyList<TrinityAssembledTexture> Textures,
    IReadOnlyList<TrinityAssembledClip> Clips);

public sealed record TrinityAssembledModel(
    string ArchivePath,
    string BundleKey,
    string Status,
    string EntryFileName,
    string OutputRelativePath,
    string OutputFormat,
    string ExportKind,
    string? DetailMessage);

public sealed record TrinityAssembledClip(
    string ArchivePath,
    string BundleKey,
    string EntryFileName,
    string OutputRelativePath,
    string DetectedExtension,
    string ExportKind);

public sealed record TrinityAssembledTexture(
    string ArchivePath,
    string BundleKey,
    string EntryFileName,
    string OutputRelativePath,
    string DetectedExtension,
    string ExportKind);
