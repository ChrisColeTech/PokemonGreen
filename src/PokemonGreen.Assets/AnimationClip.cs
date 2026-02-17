#nullable enable
namespace PokemonGreen.Assets;

/// <summary>
/// A single animation clip (idle, walk, attack, etc.) loaded from a clip-only DAE.
/// Contains keyframe channels for each animated bone, referenced by bone index
/// into the parent SkeletalModelData's bone array.
/// </summary>
public class AnimationClip
{
    public string Name { get; }
    public double Duration { get; }       // in ticks
    public double TicksPerSecond { get; } // typically 1.0 (times in seconds)
    internal AnimChannel[] Channels { get; }

    internal AnimationClip(string name, double duration, double ticksPerSecond, AnimChannel[] channels)
    {
        Name = name;
        Duration = duration;
        TicksPerSecond = ticksPerSecond;
        Channels = channels;
    }
}
