using PokemonGreen.SwitchToolboxCli.Core.Abstractions;

namespace PokemonGreen.SwitchToolboxCli.Core.Extraction;

public sealed class ArchiveExtractionService
{
    public IReadOnlyList<ExtractedArchiveFile> ExtractToDirectory(IArchiveFile archive, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var outputRoot = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputRoot);

        var extractedFiles = new List<ExtractedArchiveFile>();
        var usedRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var occupiedFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var occupiedDirectoryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { outputRoot };
        var sortedEntries = archive.Files
            .OrderBy(entry => entry.FileName, StringComparer.Ordinal)
            .ToList();

        for (var index = 0; index < sortedEntries.Count; index++)
        {
            var entry = sortedEntries[index];
            var requestedRelativePath = string.IsNullOrWhiteSpace(entry.FileName)
                ? $"entry_{index:000}.bin"
                : entry.FileName;

            var normalizedRelativePath = NormalizeRelativePath(requestedRelativePath);
            var uniqueRelativePath = EnsureUniqueRelativePath(normalizedRelativePath, usedRelativePaths);
            var destinationPath = Path.GetFullPath(Path.Combine(outputRoot, uniqueRelativePath));

            while (HasPathConflict(destinationPath, outputRoot, occupiedFilePaths, occupiedDirectoryPaths))
            {
                uniqueRelativePath = EnsureUniqueRelativePath(normalizedRelativePath, usedRelativePaths);
                destinationPath = Path.GetFullPath(Path.Combine(outputRoot, uniqueRelativePath));
            }

            if (!IsSubPath(destinationPath, outputRoot))
            {
                throw new InvalidOperationException($"Refused to write archive entry outside output root: {entry.FileName}");
            }

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
                MarkDirectoryChain(destinationDirectory, outputRoot, occupiedDirectoryPaths);
            }

            using var source = entry.OpenRead();
            try
            {
                using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                source.CopyTo(destination);
            }
            finally
            {
                source?.Dispose();
            }

            extractedFiles.Add(new ExtractedArchiveFile(uniqueRelativePath.Replace('\\', '/'), destinationPath));
            occupiedFilePaths.Add(destinationPath);
        }

        return extractedFiles;
    }

    private static bool HasPathConflict(
        string destinationPath,
        string outputRoot,
        HashSet<string> occupiedFilePaths,
        HashSet<string> occupiedDirectoryPaths)
    {
        if (occupiedDirectoryPaths.Contains(destinationPath) || Directory.Exists(destinationPath))
        {
            return true;
        }

        var directory = Path.GetDirectoryName(destinationPath);
        while (!string.IsNullOrWhiteSpace(directory) && IsSubPath(directory, outputRoot))
        {
            if (occupiedFilePaths.Contains(directory) || File.Exists(directory))
            {
                return true;
            }

            if (string.Equals(directory, outputRoot, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return false;
    }

    private static void MarkDirectoryChain(string path, string outputRoot, HashSet<string> occupiedDirectoryPaths)
    {
        var current = path;
        while (!string.IsNullOrWhiteSpace(current) && IsSubPath(current, outputRoot))
        {
            occupiedDirectoryPaths.Add(current);
            if (string.Equals(current, outputRoot, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = Path.GetDirectoryName(current);
        }
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

    private static string NormalizeRelativePath(string path)
    {
        var segments = path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment != "." && segment != "..")
            .Select(SanitizeSegment)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        return segments.Count == 0
            ? "entry.bin"
            : Path.Combine(segments.ToArray());
    }

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars).Trim();
    }

    private static bool IsSubPath(string candidatePath, string rootPath)
    {
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;

        return candidatePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(candidatePath, rootPath, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record ExtractedArchiveFile(string RelativePath, string AbsolutePath);
