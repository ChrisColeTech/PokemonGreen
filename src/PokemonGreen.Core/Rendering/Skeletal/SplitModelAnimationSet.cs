using System.Text.Json;

namespace PokemonGreen.Core.Rendering.Skeletal;

public sealed class SplitModelAnimationSet
{
    public required string ModelPath { get; init; }
    public required SkeletonRig Skeleton { get; init; }

    /// <summary>Clips keyed by manifest id/name (e.g., "clip_000", "anim_0").</summary>
    public required IReadOnlyDictionary<string, SkeletalAnimationClip> Clips { get; init; }

    /// <summary>Clips keyed by tag (e.g., "Idle", "Walk", "Run", "Jump"). Case-insensitive.</summary>
    public required IReadOnlyDictionary<string, SkeletalAnimationClip> ClipsByTag { get; init; }
}

public static class SplitModelAnimationSetLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static SplitModelAnimationSet Load(string groupFolderPath, string modelName = "model")
    {
        string manifestPath = Path.Combine(groupFolderPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Missing split export manifest", manifestPath);
        }

        string json = File.ReadAllText(manifestPath);
        SplitManifest? manifest = JsonSerializer.Deserialize<SplitManifest>(json, JsonOptions);
        if (manifest is null)
        {
            throw new InvalidDataException("Invalid split export manifest");
        }

        // Resolve model file path
        string? modelFile = null;
        List<SplitManifestClip> clipEntries;

        if (manifest.Models is { Count: > 0 })
        {
            SplitManifestModel? modelEntry = manifest.Models.FirstOrDefault(x =>
                string.Equals(x.Name, modelName, StringComparison.Ordinal));
            modelEntry ??= manifest.Models[0];

            modelFile = modelEntry.ModelFile ?? modelEntry.File;

            // Clips are nested under models; fall back to top-level for legacy manifests
            clipEntries = modelEntry.Clips is { Count: > 0 }
                ? modelEntry.Clips
                : manifest.Clips ?? new List<SplitManifestClip>();
        }
        else
        {
            throw new InvalidDataException("Manifest has no models");
        }

        if (string.IsNullOrWhiteSpace(modelFile))
        {
            throw new InvalidDataException("Could not resolve model file from manifest");
        }

        string modelPath = Path.Combine(groupFolderPath, modelFile);
        SkeletonRig skeleton = ColladaSkeletalLoader.LoadSkeleton(modelPath);

        Dictionary<string, SkeletalAnimationClip> clips = new(StringComparer.Ordinal);
        Dictionary<string, SkeletalAnimationClip> clipsByTag = new(StringComparer.OrdinalIgnoreCase);

        foreach (SplitManifestClip clipEntry in clipEntries)
        {
            string clipPath = Path.GetFullPath(Path.Combine(groupFolderPath, clipEntry.File ?? $"clips/clip_{clipEntry.Index:D3}.dae"));
            string clipName = clipEntry.SemanticName ?? clipEntry.SourceName ?? clipEntry.Name ?? clipEntry.Id ?? $"clip_{clipEntry.Index:D3}";
            SkeletalAnimationClip clip = ColladaSkeletalLoader.LoadClip(clipPath, skeleton, clipName);

            // Key-based dictionary (existing behavior)
            string clipKey = clipEntry.Id ?? clipEntry.Name ?? $"clip_{clipEntry.Index:D3}";
            if (clips.ContainsKey(clipKey))
                clipKey = $"{clipKey}_{clipEntry.Index:D3}";
            clips[clipKey] = clip;

            // Tag-based dictionary — use manifest tag, fall back to inferred tag
            string? tag = clipEntry.SemanticName ?? InferTag(clipEntry.Name ?? clipEntry.Id);
            if (!string.IsNullOrWhiteSpace(tag))
                clipsByTag.TryAdd(tag, clip);
        }

        return new SplitModelAnimationSet
        {
            ModelPath = modelPath,
            Skeleton = skeleton,
            Clips = clips,
            ClipsByTag = clipsByTag
        };
    }

    /// <summary>
    /// Infer a tag from a clip name when the manifest has no semanticName.
    /// Parses the trailing number from names like "anim_0", "Motion_1", "clip_002".
    /// Only the core three (Idle/Walk/Run) are inferred — everything else needs explicit tagging.
    /// </summary>
    private static string? InferTag(string? clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName)) return null;
        int lastUnderscore = clipName.LastIndexOf('_');
        if (lastUnderscore < 0 || lastUnderscore >= clipName.Length - 1) return null;
        if (!int.TryParse(clipName.AsSpan(lastUnderscore + 1), out int index)) return null;
        return index switch
        {
            0 => "Idle",
            1 => "Walk",
            2 => "Run",
            _ => null
        };
    }

    private sealed class SplitManifest
    {
        public List<SplitManifestModel>? Models { get; set; }
        public List<SplitManifestClip>? Clips { get; set; }
    }

    private sealed class SplitManifestModel
    {
        public string? Name { get; set; }
        public string? ModelFile { get; set; }
        public List<SplitManifestClip>? Clips { get; set; }
        public string? File { get; set; }
    }

    private sealed class SplitManifestClip
    {
        public int Index { get; set; }
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? SourceName { get; set; }
        public string? SemanticName { get; set; }
        public string? File { get; set; }
    }
}
