using PokemonGreen.SwitchToolboxCli.Core.Abstractions;

namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Animations;

public sealed class AnimationClipArchiveExportService
{
    private readonly AnimationClipSignatureDetector _signatureDetector;

    public AnimationClipArchiveExportService()
        : this(new AnimationClipSignatureDetector())
    {
    }

    public AnimationClipArchiveExportService(AnimationClipSignatureDetector signatureDetector)
    {
        _signatureDetector = signatureDetector;
    }

    public IReadOnlyList<AnimationClipExportedFile> ExportClipLikeEntries(
        IReadOnlyList<AnimationClipArchiveSource> archives,
        string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(archives);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var outputRoot = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputRoot);

        var usedRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var exported = new List<AnimationClipExportedFile>();

        foreach (var archiveSource in archives.OrderBy(item => item.ArchivePath, StringComparer.Ordinal))
        {
            var archiveDirectory = NormalizeRelativePath(archiveSource.ArchivePath, "archive");
            var sortedEntries = archiveSource.Archive.Files
                .Select((entry, index) => (Entry: entry, Index: index))
                .OrderBy(item => item.Entry.FileName, StringComparer.Ordinal)
                .ThenBy(item => item.Index)
                .ToList();

            foreach (var item in sortedEntries)
            {
                var sourceName = item.Entry.FileName;
                var payload = ReadAllBytes(item.Entry.OpenRead);
                if (!_signatureDetector.TryGetClipExtension(sourceName, payload, out var detectedExtension))
                {
                    continue;
                }

                var baseName = string.IsNullOrWhiteSpace(sourceName)
                    ? $"clip_{item.Index:000}"
                    : sourceName;

                var normalizedEntryPath = NormalizeRelativePath(baseName, $"clip_{item.Index:000}");
                if (!Path.HasExtension(normalizedEntryPath))
                {
                    normalizedEntryPath += detectedExtension;
                }

                var combinedRelativePath = Path.Combine(archiveDirectory, normalizedEntryPath);
                var uniqueRelativePath = EnsureUniqueRelativePath(combinedRelativePath, usedRelativePaths);
                var destinationPath = Path.GetFullPath(Path.Combine(outputRoot, uniqueRelativePath));
                if (!IsSubPath(destinationPath, outputRoot))
                {
                    throw new InvalidOperationException($"Refused to write clip outside output root: {sourceName}");
                }

                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.WriteAllBytes(destinationPath, payload);

                exported.Add(new AnimationClipExportedFile(
                    archiveSource.ArchivePath.Replace('\\', '/'),
                    sourceName,
                    uniqueRelativePath.Replace('\\', '/'),
                    detectedExtension));
            }
        }

        return exported;
    }

    private static byte[] ReadAllBytes(Func<Stream> openRead)
    {
        using var stream = openRead();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
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

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars).Trim();
    }
}

public sealed record AnimationClipArchiveSource(string ArchivePath, IArchiveFile Archive);

public sealed record AnimationClipExportedFile(
    string ArchivePath,
    string EntryFileName,
    string OutputRelativePath,
    string DetectedExtension);
