using System.Text;
using System.Text.RegularExpressions;
using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Core.Models;

namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Models.Trinity;

public sealed class TrinityContainerReferenceResolver
{
    private static readonly Regex ReferenceRegex = new(
        @"(?i)([A-Za-z0-9_./\\-]+\.(?:trmdl|trmsh|trmbf|trmtr|trskl|bntx|tranm|traef|tracm|tracs|tracl|tracr|tracp))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly TrinityDependencyGraphBuilder _graphBuilder;

    public TrinityContainerReferenceResolver()
        : this(new TrinityDependencyGraphBuilder())
    {
    }

    public TrinityContainerReferenceResolver(TrinityDependencyGraphBuilder graphBuilder)
    {
        _graphBuilder = graphBuilder;
    }

    public TrinityContainerResolveResult Resolve(
        TrinityIndexedBundle bundle,
        IReadOnlyList<ArchiveEntry> availableEntries,
        Func<string, byte[]?> tryReadPayload)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(availableEntries);
        ArgumentNullException.ThrowIfNull(tryReadPayload);

        var containerEntries = bundle.Entries
            .Where(entry => entry.Role == TrinityEntryRole.ModelContainer)
            .OrderBy(entry => entry.EntryPath, StringComparer.Ordinal)
            .ToList();
        if (containerEntries.Count == 0)
        {
            return TrinityContainerResolveResult.Empty;
        }

        var graphEntries = availableEntries
            .Select((entry, index) => new TrinityGraphArchiveEntry(
                TrinityDependencyGraphBuilder.NormalizePath(entry.FileName),
                entry.RootHash,
                index))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.EntryPath))
            .OrderBy(entry => entry.EntryPath, StringComparer.Ordinal)
            .ThenBy(entry => entry.StableOrder)
            .ToList();

        var byExtension = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var unresolved = new List<string>();
        var seenResolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenUnresolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var containerEntry in containerEntries)
        {
            var payload = tryReadPayload(containerEntry.EntryPath);
            if (payload is null || payload.Length == 0)
            {
                continue;
            }

            foreach (var reference in ExtractReferences(payload))
            {
                var extension = Path.GetExtension(reference);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    continue;
                }

                var links = _graphBuilder.ResolveArchiveEntryLinks(
                    [new TrinityDependencyReference(reference, extension)],
                    graphEntries,
                    consumeMatches: false);
                var resolvedPath = links
                    .Select(item => TrinityDependencyGraphBuilder.NormalizePath(item.ResolvedEntryPath))
                    .FirstOrDefault();
                if (string.IsNullOrWhiteSpace(resolvedPath))
                {
                    if (seenUnresolved.Add(reference))
                    {
                        unresolved.Add(reference);
                    }

                    continue;
                }

                if (!seenResolved.Add(resolvedPath))
                {
                    continue;
                }

                if (!byExtension.TryGetValue(extension, out var entries))
                {
                    entries = new List<string>();
                    byExtension.Add(extension, entries);
                }

                entries.Add(resolvedPath);
            }
        }

        var immutableByExtension = byExtension
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(
                item => item.Key.ToLowerInvariant(),
                item => (IReadOnlyList<string>)item.Value
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        return new TrinityContainerResolveResult(
            immutableByExtension,
            unresolved.OrderBy(path => path, StringComparer.Ordinal).ToList());
    }

    private static IReadOnlyList<string> ExtractReferences(byte[] payload)
    {
        var text = Encoding.UTF8.GetString(payload);
        var references = ReferenceRegex.Matches(text)
            .Select(match => TrinityDependencyGraphBuilder.NormalizePath(match.Groups[1].Value))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        return references;
    }
}

public sealed record TrinityContainerResolveResult(
    IReadOnlyDictionary<string, IReadOnlyList<string>> ResolvedPathsByExtension,
    IReadOnlyList<string> UnresolvedReferences)
{
    public static TrinityContainerResolveResult Empty { get; } =
        new(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase), Array.Empty<string>());

    public IReadOnlyList<string> GetResolvedPaths(string extension)
    {
        return ResolvedPathsByExtension.TryGetValue(extension, out var paths)
            ? paths
            : Array.Empty<string>();
    }
}
