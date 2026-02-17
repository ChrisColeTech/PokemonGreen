using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokemonGreen.Core.Rendering.Skeletal;

public sealed class SplitModelAnimationSet
{
    public required string ModelPath { get; init; }
    public required SkeletonRig Skeleton { get; init; }
    public required IReadOnlyDictionary<string, SkeletalAnimationClip> Clips { get; init; }
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

        // Resolve model file path — try Models[].ModelFile (OhanaCli) then Models[].File (Spica)
        string? modelFile = null;
        List<SplitManifestClip> clipEntries;

        if (manifest.Models is { Count: > 0 })
        {
            SplitManifestModel? modelEntry = manifest.Models.FirstOrDefault(x =>
                string.Equals(x.Name, modelName, StringComparison.Ordinal));
            // If no name match, fall back to first model (Spica manifests don't set Name)
            modelEntry ??= manifest.Models[0];

            modelFile = modelEntry.ModelFile ?? modelEntry.File;

            // Clips: prefer per-model clips (OhanaCli), fall back to top-level clips (Spica)
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
        foreach (SplitManifestClip clipEntry in clipEntries)
        {
            string clipPath = Path.GetFullPath(Path.Combine(groupFolderPath, clipEntry.File ?? $"clips/clip_{clipEntry.Index:D3}.dae"));
            string clipName = clipEntry.Name ?? $"clip_{clipEntry.Index:D3}";
            SkeletalAnimationClip clip = ColladaSkeletalLoader.LoadClip(clipPath, skeleton, clipName);
            string clipKey = string.IsNullOrWhiteSpace(clipName) ? $"clip_{clipEntry.Index:D3}" : clipName;
            if (clips.ContainsKey(clipKey))
            {
                clipKey = $"{clipKey}_{clipEntry.Index:D3}";
            }

            clips[clipKey] = clip;
        }

        return new SplitModelAnimationSet
        {
            ModelPath = modelPath,
            Skeleton = skeleton,
            Clips = clips
        };
    }

    private sealed class SplitManifest
    {
        public List<SplitManifestModel>? Models { get; set; }
        // Spica format: top-level clips shared by all models
        public List<SplitManifestClip>? Clips { get; set; }
    }

    private sealed class SplitManifestModel
    {
        // OhanaCli format
        public string? Name { get; set; }
        public string? ModelFile { get; set; }
        public List<SplitManifestClip>? Clips { get; set; }
        // Spica format
        public string? File { get; set; }
    }

    private sealed class SplitManifestClip
    {
        public int Index { get; set; }
        public string? Name { get; set; }
        public string? File { get; set; }
    }
}
