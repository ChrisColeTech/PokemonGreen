using Microsoft.Xna.Framework;

namespace PokemonGreen.Core.Rendering.Skeletal;

public sealed class SkeletalAnimator
{
    private readonly Dictionary<int, BoneAnimationTrack> _activeTracks = new();

    public SkeletalAnimator(SkeletonRig rig)
    {
        Rig = rig;
        LocalPose = new Matrix[rig.Bones.Count];
        WorldPose = new Matrix[rig.Bones.Count];
        SkinPose = new Matrix[rig.Bones.Count];
        CopyBindPose();
        RebuildWorldAndSkin();
    }

    public SkeletonRig Rig { get; }
    public SkeletalAnimationClip? ActiveClip { get; private set; }
    public bool Loop { get; private set; }
    public float CurrentTimeSeconds { get; private set; }
    public Matrix[] LocalPose { get; }
    public Matrix[] WorldPose { get; }
    public Matrix[] SkinPose { get; }

    public void Play(SkeletalAnimationClip clip, bool loop = true, bool resetTime = true)
    {
        ActiveClip = clip;
        Loop = loop;
        if (resetTime) CurrentTimeSeconds = 0f;

        _activeTracks.Clear();
        for (int i = 0; i < clip.Tracks.Count; i++)
        {
            BoneAnimationTrack track = clip.Tracks[i];
            _activeTracks[track.BoneIndex] = track;
        }

        EvaluatePose();
    }

    public void Stop()
    {
        ActiveClip = null;
        _activeTracks.Clear();
        CurrentTimeSeconds = 0f;
        CopyBindPose();
        RebuildWorldAndSkin();
    }

    public void Update(float deltaSeconds)
    {
        if (ActiveClip is null)
        {
            return;
        }

        float duration = Math.Max(ActiveClip.DurationSeconds, 0f);
        if (duration <= 0f)
        {
            CurrentTimeSeconds = 0f;
        }
        else if (Loop)
        {
            CurrentTimeSeconds = (CurrentTimeSeconds + deltaSeconds) % duration;
            if (CurrentTimeSeconds < 0f) CurrentTimeSeconds += duration;
        }
        else
        {
            CurrentTimeSeconds = Math.Clamp(CurrentTimeSeconds + deltaSeconds, 0f, duration);
        }

        EvaluatePose();
    }

    private void EvaluatePose()
    {
        CopyBindPose();

        foreach ((int boneIndex, BoneAnimationTrack track) in _activeTracks)
        {
            LocalPose[boneIndex] = track.Sample(CurrentTimeSeconds);
        }

        RebuildWorldAndSkin();
    }

    private void CopyBindPose()
    {
        Array.Copy(Rig.BindLocalTransforms, LocalPose, LocalPose.Length);
    }

    private void RebuildWorldAndSkin()
    {
        for (int i = 0; i < Rig.Bones.Count; i++)
        {
            int parent = Rig.Bones[i].ParentIndex;
            WorldPose[i] = parent >= 0 ? LocalPose[i] * WorldPose[parent] : LocalPose[i];
            SkinPose[i] = Rig.InverseBindTransforms[i] * WorldPose[i];
        }
    }
}
