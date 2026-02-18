using PokemonGreen.SwitchToolboxCli.Formats.Common;

namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Models.Trinity;

public sealed class TrinityDependencyGraphBuilder
{
    public IReadOnlyList<string> BuildPathCandidates(string dependencyPath)
    {
        ArgumentNullException.ThrowIfNull(dependencyPath);

        var normalized = NormalizePath(dependencyPath);
        var trimmed = normalized.TrimStart('/');

        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        Add(candidates, seen, dependencyPath);
        Add(candidates, seen, normalized);
        Add(candidates, seen, trimmed);
        Add(candidates, seen, normalized.ToLowerInvariant());
        Add(candidates, seen, trimmed.ToLowerInvariant());

        return candidates;
    }

    public IReadOnlyList<ulong> BuildHashCandidates(string dependencyPath)
    {
        var pathCandidates = BuildPathCandidates(dependencyPath);
        var hashes = new List<ulong>(pathCandidates.Count);
        var seen = new HashSet<ulong>();

        foreach (var candidate in pathCandidates)
        {
            var hash = Fnv1a64.Compute(candidate);
            if (seen.Add(hash))
            {
                hashes.Add(hash);
            }
        }

        return hashes;
    }

    public IReadOnlyList<TrinityDependencyLink> ResolveArchiveEntryLinks(
        IReadOnlyList<TrinityDependencyReference> dependencies,
        IReadOnlyList<TrinityGraphArchiveEntry> availableEntries,
        bool consumeMatches,
        bool allowGenericBinHashMatches = false)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(availableEntries);

        if (dependencies.Count == 0 || availableEntries.Count == 0)
        {
            return Array.Empty<TrinityDependencyLink>();
        }

        var candidates = availableEntries
            .Select((entry, index) => new ResolvedCandidate(
                entry.EntryPath,
                NormalizePath(entry.EntryPath),
                Path.GetFileName(NormalizePath(entry.EntryPath)),
                Path.GetExtension(entry.EntryPath).ToLowerInvariant(),
                entry.RootHash,
                entry.StableOrder,
                index))
            .OrderBy(entry => entry.StableOrder)
            .ThenBy(entry => entry.NormalizedPath, StringComparer.Ordinal)
            .ThenBy(entry => entry.SourceIndex)
            .ToList();

        var byPath = candidates
            .GroupBy(entry => entry.NormalizedPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var byFileName = candidates
            .Where(entry => !string.IsNullOrWhiteSpace(entry.FileName))
            .GroupBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var byHash = candidates
            .Where(entry => entry.RootHash.HasValue)
            .GroupBy(entry => entry.RootHash!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var consumed = new HashSet<int>();
        var output = new List<TrinityDependencyLink>();

        foreach (var dependency in dependencies
                     .Select((dependency, index) => (dependency, index))
                     .OrderBy(item => NormalizePath(item.dependency.DependencyPath), StringComparer.Ordinal)
                     .ThenBy(item => item.dependency.Extension, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.index))
        {
            var normalizedDependency = NormalizePath(dependency.dependency.DependencyPath);
            var requiredExtension = NormalizeExtension(dependency.dependency.Extension);

            if (!TrySelectCandidate(
                    dependency.dependency.DependencyPath,
                    normalizedDependency,
                    requiredExtension,
                    byPath,
                    byFileName,
                    byHash,
                    candidates,
                    consumed,
                    consumeMatches,
                    allowGenericBinHashMatches,
                    out var selected,
                    out var matchKind,
                    out var matchedHash))
            {
                continue;
            }

            if (consumeMatches)
            {
                consumed.Add(selected.SourceIndex);
            }

            output.Add(new TrinityDependencyLink(
                dependency.dependency.DependencyPath,
                requiredExtension,
                selected.OriginalPath,
                selected.RootHash,
                matchKind,
                matchedHash));
        }

        return output;
    }

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var segments = path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment != "." && segment != "..")
            .ToList();

        return segments.Count == 0
            ? string.Empty
            : string.Join('/', segments);
    }

    private bool TrySelectCandidate(
        string dependencyPath,
        string normalizedDependency,
        string requiredExtension,
        IReadOnlyDictionary<string, List<ResolvedCandidate>> byPath,
        IReadOnlyDictionary<string, List<ResolvedCandidate>> byFileName,
        IReadOnlyDictionary<ulong, List<ResolvedCandidate>> byHash,
        IReadOnlyList<ResolvedCandidate> allCandidates,
        IReadOnlySet<int> consumed,
        bool consumeMatches,
        bool allowGenericBinHashMatches,
        out ResolvedCandidate selected,
        out string matchKind,
        out ulong? matchedHash)
    {
        selected = null!;
        matchKind = string.Empty;
        matchedHash = null;

        foreach (var pathCandidate in BuildPathCandidates(dependencyPath))
        {
            var normalizedPathCandidate = NormalizePath(pathCandidate);
            if (string.IsNullOrWhiteSpace(normalizedPathCandidate) ||
                !byPath.TryGetValue(normalizedPathCandidate, out var pathMatches))
            {
                continue;
            }

            var exact = pathMatches.FirstOrDefault(item =>
                MatchesExtension(item.Extension, requiredExtension) &&
                (!consumeMatches || !consumed.Contains(item.SourceIndex)));
            if (exact is not null)
            {
                selected = exact;
                matchKind = "path_exact";
                return true;
            }
        }

        var fileName = Path.GetFileName(normalizedDependency);
        if (!string.IsNullOrWhiteSpace(fileName) && byFileName.TryGetValue(fileName, out var fileNameMatches))
        {
            var byName = fileNameMatches.FirstOrDefault(item =>
                MatchesExtension(item.Extension, requiredExtension) &&
                (!consumeMatches || !consumed.Contains(item.SourceIndex)));
            if (byName is not null)
            {
                selected = byName;
                matchKind = "path_file_name";
                return true;
            }
        }

        foreach (var hashCandidate in BuildHashCandidates(dependencyPath))
        {
            if (!byHash.TryGetValue(hashCandidate, out var hashMatches))
            {
                continue;
            }

            var byHashCandidate = hashMatches.FirstOrDefault(item =>
                (MatchesExtension(item.Extension, requiredExtension) ||
                 (allowGenericBinHashMatches && item.Extension.Equals(".bin", StringComparison.OrdinalIgnoreCase))) &&
                (!consumeMatches || !consumed.Contains(item.SourceIndex)));
            if (byHashCandidate is not null)
            {
                selected = byHashCandidate;
                matchKind = "hash";
                matchedHash = hashCandidate;
                return true;
            }
        }

        var extensionFallback = allCandidates.FirstOrDefault(item =>
            MatchesExtension(item.Extension, requiredExtension) &&
            (!consumeMatches || !consumed.Contains(item.SourceIndex)));
        if (extensionFallback is not null)
        {
            selected = extensionFallback;
            matchKind = "extension";
            return true;
        }

        return false;
    }

    private static bool MatchesExtension(string extension, string requiredExtension)
    {
        if (string.IsNullOrWhiteSpace(requiredExtension))
        {
            return true;
        }

        return extension.Equals(requiredExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var normalized = extension.Trim();
        if (!normalized.StartsWith(".", StringComparison.Ordinal))
        {
            normalized = "." + normalized;
        }

        return normalized.ToLowerInvariant();
    }

    private static void Add(ICollection<string> output, ISet<string> seen, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        var normalized = NormalizePath(candidate);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (seen.Add(normalized))
        {
            output.Add(normalized);
        }
    }

    private sealed record ResolvedCandidate(
        string OriginalPath,
        string NormalizedPath,
        string FileName,
        string Extension,
        ulong? RootHash,
        int StableOrder,
        int SourceIndex);
}

public sealed record TrinityDependencyReference(string DependencyPath, string Extension);

public sealed record TrinityGraphArchiveEntry(string EntryPath, ulong? RootHash, int StableOrder);

public sealed record TrinityDependencyLink(
    string DependencyPath,
    string Extension,
    string ResolvedEntryPath,
    ulong? ResolvedEntryHash,
    string MatchKind,
    ulong? MatchedHash);
