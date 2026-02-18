using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Core.Models;

namespace PokemonGreen.SwitchToolboxCli.Core.Extraction;

public sealed class TrinityBinIndexerService
{
    private static readonly TrinityPayloadSniffer PayloadSniffer = new();

    private static readonly IReadOnlyDictionary<string, TrinityEntryRole> ExtensionRoleMap =
        new Dictionary<string, TrinityEntryRole>(StringComparer.OrdinalIgnoreCase)
        {
            [".obj"] = TrinityEntryRole.DirectModel,
            [".dae"] = TrinityEntryRole.DirectModel,
            [".trmmt"] = TrinityEntryRole.ModelContainer,
            [".trmdt"] = TrinityEntryRole.ModelContainer,
            [".trmdl"] = TrinityEntryRole.Model,
            [".trmsh"] = TrinityEntryRole.Mesh,
            [".trskl"] = TrinityEntryRole.Skeleton,
            [".trmtr"] = TrinityEntryRole.Material,
            [".trmbf"] = TrinityEntryRole.BlendShape,
            [".bntx"] = TrinityEntryRole.Texture,
            [".tranm"] = TrinityEntryRole.AnimationClip,
            [".traef"] = TrinityEntryRole.EffectClip,
            [".tracm"] = TrinityEntryRole.AnimationClip,
            [".tracs"] = TrinityEntryRole.AnimationClip,
            [".tracl"] = TrinityEntryRole.AnimationClip,
            [".tracr"] = TrinityEntryRole.AnimationClip,
            [".tracp"] = TrinityEntryRole.AnimationClip,
        };

    private static readonly HashSet<string> RoleMarkerTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "model",
        "mdl",
        "mesh",
        "msh",
        "skeleton",
        "skel",
        "skl",
        "material",
        "mat",
        "mtr",
        "mmt",
        "mdt",
        "blend",
        "bf",
        "bntx",
        "anim",
        "anm",
        "effect",
        "efx",
        "fx",
        "aef",
        "control",
        "ctrl",
    };

    private static readonly HashSet<string> GenericStemPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "file_",
        "entry_",
    };

    public IReadOnlyList<TrinityIndexedBundle> IndexArchives(IEnumerable<TrinityArchiveSource> archives)
    {
        ArgumentNullException.ThrowIfNull(archives);

        var grouped = new List<TrinityIndexedBundle>();
        foreach (var source in archives.OrderBy(item => item.ArchivePath, StringComparer.Ordinal))
        {
            grouped.AddRange(IndexArchive(source));
        }

        return grouped;
    }

    private static IReadOnlyList<TrinityIndexedBundle> IndexArchive(TrinityArchiveSource source)
    {
        var bundles = new Dictionary<string, List<TrinityBundleEntry>>(StringComparer.Ordinal);
        foreach (var entry in source.Archive.Files.OrderBy(item => item.FileName, StringComparer.Ordinal))
        {
            var normalizedPath = NormalizePath(entry.FileName);
            var extension = string.IsNullOrWhiteSpace(entry.ExtensionHint)
                ? Path.GetExtension(normalizedPath)
                : entry.ExtensionHint!;
            var isSniffed = false;
            string? detailMessage = entry.DetailMessage;
            if (!ExtensionRoleMap.TryGetValue(extension, out var role))
            {
                if (!IsGenericBinPath(normalizedPath) ||
                    !TrySniffRole(entry, out extension, out role, out detailMessage))
                {
                    continue;
                }

                isSniffed = true;
            }

            var logicalKey = isSniffed && IsGenericBinPath(normalizedPath)
                ? BuildGenericBundleKey(normalizedPath)
                : BuildLogicalModelKey(normalizedPath);
            if (!bundles.TryGetValue(logicalKey, out var groupedEntries))
            {
                groupedEntries = new List<TrinityBundleEntry>();
                bundles.Add(logicalKey, groupedEntries);
            }

            groupedEntries.Add(new TrinityBundleEntry(normalizedPath, extension.ToLowerInvariant(), role, isSniffed, detailMessage));
        }

        return bundles
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item =>
            {
                var entries = item.Value
                    .OrderBy(entry => entry.EntryPath, StringComparer.Ordinal)
                    .ThenBy(entry => entry.Role)
                    .ToList();
                var clips = entries
                    .Where(entry => entry.Role is TrinityEntryRole.AnimationClip or TrinityEntryRole.EffectClip)
                    .Select(entry => new TrinityClipReference(entry.EntryPath, entry.Role))
                    .ToList();
                return new TrinityIndexedBundle(source.ArchivePath, item.Key, entries, clips);
            })
            .ToList();
    }

    private static string NormalizePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return "unnamed.bin";
        }

        var segments = rawPath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment != "." && segment != "..")
            .ToList();

        return segments.Count == 0 ? "unnamed.bin" : string.Join('/', segments);
    }

    private static string BuildLogicalModelKey(string normalizedPath)
    {
        // Deterministic grouping strategy:
        // 1) Normalize to slash-separated relative paths and keep the directory portion.
        // 2) Tokenize the base file name on '_', '-', and '.'.
        // 3) If a known role marker token appears (mesh/material/anim/etc), trim the key
        //    at that marker so role-specialized siblings still group with the same model.
        // 4) Lowercase the final key so path casing does not produce split bundles.
        var directory = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/') ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(normalizedPath);
        var tokens = stem
            .Split(['_', '-', '.'], StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (tokens.Count == 0)
        {
            tokens.Add("unnamed");
        }

        var markerIndex = tokens.FindIndex(token => RoleMarkerTokens.Contains(token));
        if (markerIndex > 0)
        {
            tokens = tokens.Take(markerIndex).ToList();
        }

        var keyStem = string.Join('_', tokens).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(directory))
        {
            return keyStem;
        }

        return $"{directory.ToLowerInvariant()}/{keyStem}";
    }

    private static bool TrySniffRole(
        ArchiveEntry entry,
        out string extension,
        out TrinityEntryRole role,
        out string? detailMessage)
    {
        extension = string.Empty;
        role = default;
        detailMessage = null;

        if (!TryReadPayloadForSniff(entry, out var payload))
        {
            return false;
        }

        if (!PayloadSniffer.TryClassify(payload, out var classification))
        {
            return false;
        }

        extension = classification.Extension;
        role = classification.Role;
        var sniffDetail = $"sniffed:{classification.Confidence}:{classification.Reason}";
        detailMessage = string.IsNullOrWhiteSpace(entry.DetailMessage)
            ? sniffDetail
            : $"{entry.DetailMessage}; {sniffDetail}";
        return true;
    }

    private static bool TryReadPayloadForSniff(ArchiveEntry entry, out byte[] payload)
    {
        payload = Array.Empty<byte>();
        try
        {
            using var stream = entry.OpenRead();
            var buffer = new byte[0x200];
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                return false;
            }

            payload = read == buffer.Length ? buffer : buffer.AsSpan(0, read).ToArray();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsGenericBinPath(string normalizedPath)
    {
        if (!Path.GetExtension(normalizedPath).Equals(".bin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(normalizedPath);
        return GenericStemPrefixes.Any(prefix => stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildGenericBundleKey(string normalizedPath)
    {
        var directory = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/').ToLowerInvariant() ?? string.Empty;
        return string.IsNullOrWhiteSpace(directory)
            ? "trinity_generic"
            : $"{directory}/trinity_generic";
    }
}
