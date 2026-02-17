using Microsoft.Xna.Framework;

namespace PokemonGreen.Core.Rendering.Skeletal;

public sealed class SkeletonBone
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required string NodeId { get; init; }
    public required int ParentIndex { get; init; }
    public required Matrix BindLocalTransform { get; init; }
}

public sealed class SkeletonRig
{
    private readonly Dictionary<string, int> _boneIndexByName;

    public SkeletonRig(IReadOnlyList<SkeletonBone> bones)
    {
        Bones = bones;
        _boneIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);

        BindLocalTransforms = new Matrix[bones.Count];
        BindWorldTransforms = new Matrix[bones.Count];
        InverseBindTransforms = new Matrix[bones.Count];

        for (int i = 0; i < bones.Count; i++)
        {
            SkeletonBone bone = bones[i];
            BindLocalTransforms[i] = bone.BindLocalTransform;

            if (!_boneIndexByName.ContainsKey(bone.Name)) _boneIndexByName.Add(bone.Name, i);
            if (!string.IsNullOrWhiteSpace(bone.NodeId) && !_boneIndexByName.ContainsKey(bone.NodeId)) _boneIndexByName.Add(bone.NodeId, i);
            string targetName = bone.Name + "_bone_id";
            if (!_boneIndexByName.ContainsKey(targetName)) _boneIndexByName.Add(targetName, i);
        }

        for (int i = 0; i < bones.Count; i++)
        {
            int parent = bones[i].ParentIndex;
            BindWorldTransforms[i] = parent >= 0 ? BindLocalTransforms[i] * BindWorldTransforms[parent] : BindLocalTransforms[i];
            InverseBindTransforms[i] = Matrix.Invert(BindWorldTransforms[i]);
        }
    }

    public IReadOnlyList<SkeletonBone> Bones { get; }
    public Matrix[] BindLocalTransforms { get; }
    public Matrix[] BindWorldTransforms { get; }
    public Matrix[] InverseBindTransforms { get; }

    public bool TryGetBoneIndex(string name, out int index)
    {
        return _boneIndexByName.TryGetValue(name, out index);
    }
}
