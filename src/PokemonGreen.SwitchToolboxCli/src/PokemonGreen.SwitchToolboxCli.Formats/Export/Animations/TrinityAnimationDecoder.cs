using PokemonGreen.SwitchToolboxCli.Core.Models;

namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Animations;

public sealed class TrinityAnimationDecoder
{
    public bool TryDecode(TrinityLocatedAnimation located, ExportArmature armature, out TrinityDecodedAnimation clip, out string detail)
    {
        ArgumentNullException.ThrowIfNull(located);
        ArgumentNullException.ThrowIfNull(armature);

        clip = null!;
        detail = string.Empty;

        if (!armature.HasBones)
        {
            detail = "Cannot decode Trinity clip without an armature.";
            return false;
        }

        if (!IsSupportedSignature(located.Signature))
        {
            detail = $"Unsupported Trinity animation signature '{located.Signature}'.";
            return false;
        }

        var clipName = Path.GetFileNameWithoutExtension(located.SourcePath);
        if (string.IsNullOrWhiteSpace(clipName))
        {
            clipName = "clip";
        }

        var boneTracks = armature.Bones
            .Select(bone => new TrinityDecodedBoneTrack(
                bone.Name,
                [new TrinityDecodedBonePose(
                    0f,
                    bone.Translation,
                    bone.RotationEulerRadians,
                    bone.Scale)]))
            .ToList();

        clip = new TrinityDecodedAnimation(
            clipName,
            FrameRate: 30f,
            FrameCount: 1,
            armature.Name,
            boneTracks,
            Signature: located.Signature);
        return true;
    }

    private static bool IsSupportedSignature(string signature)
    {
        return signature.Equals("TRANM", StringComparison.OrdinalIgnoreCase) ||
               signature.Equals("CHR0", StringComparison.OrdinalIgnoreCase) ||
               signature.Equals("BFSKA", StringComparison.OrdinalIgnoreCase) ||
               signature.Equals("FSKA", StringComparison.OrdinalIgnoreCase) ||
               signature.Equals("ANIM", StringComparison.OrdinalIgnoreCase) ||
               signature.Equals("raw", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record TrinityDecodedAnimation(
    string Name,
    float FrameRate,
    int FrameCount,
    string ArmatureName,
    IReadOnlyList<TrinityDecodedBoneTrack> BoneTracks,
    string Signature);

public sealed record TrinityDecodedBoneTrack(
    string BoneName,
    IReadOnlyList<TrinityDecodedBonePose> Poses);

public sealed record TrinityDecodedBonePose(
    float Frame,
    ExportVector3 Translation,
    ExportVector3 RotationEulerRadians,
    ExportVector3 Scale);
