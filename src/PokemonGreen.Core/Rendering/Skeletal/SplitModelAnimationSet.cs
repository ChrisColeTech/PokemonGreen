using System.Text.Json;

namespace PokemonGreen.Core.Rendering.Skeletal;

public sealed class SplitModelAnimationSet
{
    public required string ModelPath { get; init; }
    public required SkeletonRig Skeleton { get; init; }
    public required IReadOnlyDictionary<string, SkeletalAnimationClip> Clips { get; init; }
}

public static class SplitModelAnimationSetLoader
{
    public static SplitModelAnimationSet Load(string groupFolderPath, string modelName = "model")
    {
        string manifestPath = Path.Combine(groupFolderPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Missing split export manifest", manifestPath);
        }

        string json = File.ReadAllText(manifestPath);
        SplitManifest? manifest = JsonSerializer.Deserialize<SplitManifest>(json);
        if (manifest is null)
        {
            throw new InvalidDataException("Invalid split export manifest");
        }

        SplitManifestModel? modelEntry = manifest.Models.FirstOrDefault(x => string.Equals(x.Name, modelName, StringComparison.Ordinal));
        if (modelEntry is null)
        {
            throw new InvalidDataException($"Model '{modelName}' not found in split manifest");
        }

        string modelPath = Path.Combine(groupFolderPath, modelEntry.ModelFile);
        SkeletonRig skeleton = ColladaSkeletalLoader.LoadSkeleton(modelPath);

        Dictionary<string, SkeletalAnimationClip> clips = new(StringComparer.Ordinal);
        foreach (SplitManifestClip clipEntry in modelEntry.Clips)
        {
            string clipPath = Path.GetFullPath(Path.Combine(groupFolderPath, clipEntry.File));
            SkeletalAnimationClip clip = ColladaSkeletalLoader.LoadClip(clipPath, skeleton, clipEntry.Name);
            string clipKey = string.IsNullOrWhiteSpace(clipEntry.Name) ? $"clip_{clipEntry.Index:D3}" : clipEntry.Name;
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
        public required List<SplitManifestModel> Models { get; init; }
    }

    private sealed class SplitManifestModel
    {
        public required string Name { get; init; }
        public required string ModelFile { get; init; }
        public required List<SplitManifestClip> Clips { get; init; }
    }

    private sealed class SplitManifestClip
    {
        public int Index { get; init; }
        public required string Name { get; init; }
        public required string File { get; init; }
    }
}
