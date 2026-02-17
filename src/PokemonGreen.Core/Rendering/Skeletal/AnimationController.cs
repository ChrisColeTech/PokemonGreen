using Microsoft.Xna.Framework;

namespace PokemonGreen.Core.Rendering.Skeletal;

/// <summary>
/// High-level animation controller that resolves clips by tag.
/// Tags are assigned by exporters (slot-map) or manually in the BgEditor.
/// </summary>
public sealed class AnimationController
{
    private readonly SplitModelAnimationSet _animSet;
    private readonly SkeletalAnimator _animator;

    public AnimationController(SplitModelAnimationSet animSet)
    {
        _animSet = animSet;
        _animator = new SkeletalAnimator(animSet.Skeleton);
    }

    public SkeletalAnimator Animator => _animator;
    public SplitModelAnimationSet AnimationSet => _animSet;
    public string? ActiveTag { get; private set; }
    public Matrix[] SkinPose => _animator.SkinPose;

    /// <summary>
    /// Play a clip by tag (e.g., "Idle", "Walk", "Run", "Jump").
    /// Returns false if no clip is mapped to this tag.
    /// </summary>
    public bool Play(string tag, bool loop = true, bool resetTime = true)
    {
        if (!resetTime && string.Equals(ActiveTag, tag, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!_animSet.ClipsByTag.TryGetValue(tag, out var clip))
            return false;

        ActiveTag = tag;
        _animator.Play(clip, loop, resetTime);
        return true;
    }

    /// <summary>Check whether a tag has a mapped clip.</summary>
    public bool HasClip(string tag) => _animSet.ClipsByTag.ContainsKey(tag);

    public void Update(float deltaSeconds) => _animator.Update(deltaSeconds);
}
