using FlatSharp;
using PokemonGreen.SwitchToolboxCli.Core.Models;

namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Models.Trinity;

public sealed class TrinityArmatureBuilder
{
    private static readonly FlatBufferSerializer Serializer = new();

    public bool TryBuild(
        byte[] trsklPayload,
        string fallbackName,
        out ExportArmature armature,
        out string detail)
    {
        armature = null!;
        detail = string.Empty;

        if (!TryParse(trsklPayload, out var skeletonRoot, out detail))
        {
            return false;
        }

        var nodes = skeletonRoot.TransformNodes;
        if (nodes is null || nodes.Count == 0)
        {
            detail = "TRSKL decode failed: transform node vector is empty.";
            return false;
        }

        var joints = skeletonRoot.JointInfos;
        var bones = new List<ExportBone>(nodes.Count);
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node is null)
            {
                continue;
            }

            var parentIndex = node.ParentNodeIndex;
            if (parentIndex < 0 || parentIndex >= nodes.Count || parentIndex == i)
            {
                parentIndex = -1;
            }

            var transform = node.Transform;
            var translation = ToVector3(transform?.Translate, new ExportVector3(0f, 0f, 0f));
            var rotation = ToVector3(transform?.Rotate, new ExportVector3(0f, 0f, 0f));
            var scale = ToVector3(transform?.Scale, new ExportVector3(1f, 1f, 1f));

            var inverseBindMatrix = ExportMatrix4x4.Identity;
            var jointIndex = node.JointInfoIndex;
            if (joints is not null && jointIndex >= 0 && jointIndex < joints.Count)
            {
                inverseBindMatrix = ToMatrix(joints[jointIndex]?.InverseBindPoseMatrix);
            }

            bones.Add(new ExportBone
            {
                Name = string.IsNullOrWhiteSpace(node.Name) ? $"bone_{i:000}" : node.Name,
                ParentIndex = parentIndex,
                Translation = translation,
                RotationEulerRadians = rotation,
                Scale = scale,
                InverseBindMatrix = inverseBindMatrix,
            });
        }

        if (bones.Count == 0)
        {
            detail = "TRSKL decode failed: no valid bones were produced.";
            return false;
        }

        armature = new ExportArmature
        {
            Name = string.IsNullOrWhiteSpace(fallbackName) ? "armature" : fallbackName,
            Bones = bones,
        };

        return true;
    }

    private static bool TryParse(byte[] payload, out TrinitySkeletonRoot skeletonRoot, out string detail)
    {
        skeletonRoot = null!;
        detail = string.Empty;

        if (payload.Length < 4)
        {
            detail = $"TRSKL decode failed: payload is too small ({payload.Length} bytes).";
            return false;
        }

        try
        {
            var parsed = Serializer.Parse<TrinitySkeletonRoot>(payload);
            if (parsed is null)
            {
                detail = "TRSKL decode failed: parsed object was null.";
                return false;
            }

            skeletonRoot = parsed;
            return true;
        }
        catch (Exception ex)
        {
            detail = $"TRSKL decode failed: {ex.Message}";
            return false;
        }
    }

    private static ExportVector3 ToVector3(TrinityVector3f? source, ExportVector3 fallback)
    {
        return source is null
            ? fallback
            : new ExportVector3(source.X, source.Y, source.Z);
    }

    private static ExportMatrix4x4 ToMatrix(TrinityMatrix4x3f? matrix)
    {
        if (matrix?.AxisX is null || matrix.AxisY is null || matrix.AxisZ is null || matrix.AxisW is null)
        {
            return ExportMatrix4x4.Identity;
        }

        return new ExportMatrix4x4(
            matrix.AxisX.X, matrix.AxisX.Y, matrix.AxisX.Z, 0f,
            matrix.AxisY.X, matrix.AxisY.Y, matrix.AxisY.Z, 0f,
            matrix.AxisZ.X, matrix.AxisZ.Y, matrix.AxisZ.Z, 0f,
            matrix.AxisW.X, matrix.AxisW.Y, matrix.AxisW.Z, 1f);
    }
}
