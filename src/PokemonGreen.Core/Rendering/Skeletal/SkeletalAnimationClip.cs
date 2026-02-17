using Microsoft.Xna.Framework;

namespace PokemonGreen.Core.Rendering.Skeletal;

public sealed class AnimationKeyframe
{
    public required float TimeSeconds { get; init; }
    public required Matrix Transform { get; init; }
}

public sealed class BoneAnimationTrack
{
    public required int BoneIndex { get; init; }
    public required IReadOnlyList<AnimationKeyframe> Keyframes { get; init; }

    public Matrix Sample(float timeSeconds)
    {
        if (Keyframes.Count == 0) return Matrix.Identity;
        if (Keyframes.Count == 1) return Keyframes[0].Transform;

        if (timeSeconds <= Keyframes[0].TimeSeconds) return Keyframes[0].Transform;
        int last = Keyframes.Count - 1;
        if (timeSeconds >= Keyframes[last].TimeSeconds) return Keyframes[last].Transform;

        for (int i = 1; i < Keyframes.Count; i++)
        {
            AnimationKeyframe next = Keyframes[i];
            if (timeSeconds > next.TimeSeconds) continue;

            AnimationKeyframe prev = Keyframes[i - 1];
            float span = next.TimeSeconds - prev.TimeSeconds;
            if (span <= 0f) return next.Transform;

            float alpha = (timeSeconds - prev.TimeSeconds) / span;
            return InterpolateTransform(prev.Transform, next.Transform, alpha);
        }

        return Keyframes[last].Transform;
    }

    private static Matrix InterpolateTransform(Matrix from, Matrix to, float alpha)
    {
        if (!from.Decompose(out Vector3 fromScale, out Quaternion fromRotation, out Vector3 fromTranslation) ||
            !to.Decompose(out Vector3 toScale, out Quaternion toRotation, out Vector3 toTranslation))
        {
            return alpha < 0.5f ? from : to;
        }

        Vector3 scale = Vector3.Lerp(fromScale, toScale, alpha);
        Quaternion rotation = Quaternion.Slerp(fromRotation, toRotation, alpha);
        Vector3 translation = Vector3.Lerp(fromTranslation, toTranslation, alpha);

        return Matrix.CreateScale(scale) * Matrix.CreateFromQuaternion(rotation) * Matrix.CreateTranslation(translation);
    }
}

public sealed class SkeletalAnimationClip
{
    public required string Name { get; init; }
    public required float DurationSeconds { get; init; }
    public required IReadOnlyList<BoneAnimationTrack> Tracks { get; init; }
}
