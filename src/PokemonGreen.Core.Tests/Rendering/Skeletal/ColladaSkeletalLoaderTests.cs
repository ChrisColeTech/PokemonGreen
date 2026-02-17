using Microsoft.Xna.Framework;
using PokemonGreen.Core.Rendering.Skeletal;
using Xunit;

namespace PokemonGreen.Core.Tests.Rendering.Skeletal;

public sealed class ColladaSkeletalLoaderTests
{
    [Fact]
    public void SplitExport_LoadsSkeletonAndClip_WithStableTransforms()
    {
        string groupPath = Path.Combine(GetRepoRoot(), "src", "PokemonGreen.Tests", "exports-split-verify-20260217", "4", "0000_model");
        Assert.True(Directory.Exists(groupPath), $"missing test export folder: {groupPath}");

        SplitModelAnimationSet set = SplitModelAnimationSetLoader.Load(groupPath, "model");
        Assert.True(set.Skeleton.Bones.Count > 0);
        Assert.True(set.Clips.Count > 0);

        SkeletalAnimationClip clip = set.Clips.Values.First();
        Assert.True(clip.Tracks.Count > 0);

        SkeletalAnimator animator = new SkeletalAnimator(set.Skeleton);
        animator.Play(clip, loop: true);
        animator.Update(1f / 30f);

        Assert.Equal(set.Skeleton.Bones.Count, animator.WorldPose.Length);
        for (int i = 0; i < animator.WorldPose.Length; i++)
        {
            Assert.True(IsFinite(animator.WorldPose[i]), $"world matrix has non-finite values at bone index {i}");
            Assert.True(IsFinite(animator.SkinPose[i]), $"skin matrix has non-finite values at bone index {i}");
        }

        if (set.Clips.Count > 1)
        {
            SkeletalAnimationClip second = set.Clips.Values.Skip(1).First();
            animator.Play(second, loop: true, resetTime: true);
            animator.Update(1f / 30f);
            Assert.Equal(second, animator.ActiveClip);
        }
    }

    private static bool IsFinite(Matrix m)
    {
        return float.IsFinite(m.M11) && float.IsFinite(m.M12) && float.IsFinite(m.M13) && float.IsFinite(m.M14)
            && float.IsFinite(m.M21) && float.IsFinite(m.M22) && float.IsFinite(m.M23) && float.IsFinite(m.M24)
            && float.IsFinite(m.M31) && float.IsFinite(m.M32) && float.IsFinite(m.M33) && float.IsFinite(m.M34)
            && float.IsFinite(m.M41) && float.IsFinite(m.M42) && float.IsFinite(m.M43) && float.IsFinite(m.M44);
    }

    private static string GetRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}
