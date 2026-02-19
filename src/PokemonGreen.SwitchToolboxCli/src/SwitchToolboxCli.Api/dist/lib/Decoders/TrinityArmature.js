import { Vector3, Quaternion, Matrix4 } from './Math.js';
/**
 * Data-only skeleton/armature decoded from TRSKL.
 * Ported from gftool Armature.cs — all GL rendering code removed.
 */
export class TrinityArmature {
    Bones = [];
    _parentIndices = [];
    _jointInfoToNode = [];
    _skinningPaletteOffset = 0;
    get ParentIndices() {
        return this._parentIndices;
    }
    get JointInfoCount() {
        return this._jointInfoToNode.length;
    }
    get BoneMetaCount() {
        return 0;
    }
    constructor(skel, sourcePath, useTrsklInverseBind = true) {
        this._skinningPaletteOffset = skel.SkinningPaletteOffset ?? 0;
        for (const transNode of skel.TransformNodes ?? []) {
            const bone = new Bone(transNode, false);
            this.Bones.push(bone);
            this._parentIndices.push(transNode.ParentNodeIndex);
        }
        this.ApplyJointInfoFromTrskl(skel);
        this.ApplyJointInfoFromJson(sourcePath);
        for (let i = 0; i < this.Bones.length; i++) {
            const parentIndex = this.Bones[i].ParentIndex;
            if (parentIndex >= 0 && parentIndex < this.Bones.length && parentIndex !== i) {
                this.Bones[parentIndex].AddChild(this.Bones[i]);
            }
        }
        this.UpdateRestParentMatrices();
        this.ComputeInverseBindMatrices(useTrsklInverseBind);
    }
    ApplyJointInfoFromTrskl(skel) {
        if (!skel.JointInfos || skel.JointInfos.length === 0 || !skel.TransformNodes || skel.TransformNodes.length === 0) {
            return;
        }
        this._jointInfoToNode = new Array(skel.JointInfos.length).fill(-1);
        const count = Math.min(this.Bones.length, skel.TransformNodes.length);
        for (let i = 0; i < count; i++) {
            const node = skel.TransformNodes[i];
            const jointId = node.JointInfoIndex;
            if (jointId < 0 || jointId >= skel.JointInfos.length) {
                continue;
            }
            this._jointInfoToNode[jointId] = i;
            TrinityArmature.ApplyTrsklJointInfoToBone(this.Bones[i], skel.JointInfos[jointId]);
        }
    }
    static ApplyTrsklJointInfoToBone(bone, joint) {
        bone.UseSegmentScaleCompensate = joint.SegmentScaleCompensate;
        bone.Skinning = joint.InfluenceSkinning;
        if (joint.InverseBindPoseMatrix != null) {
            bone.JointInverseBindWorld = TrinityArmature.CreateMatrixFromAxis(new Vector3(joint.InverseBindPoseMatrix.AxisX.X, joint.InverseBindPoseMatrix.AxisX.Y, joint.InverseBindPoseMatrix.AxisX.Z), new Vector3(joint.InverseBindPoseMatrix.AxisY.X, joint.InverseBindPoseMatrix.AxisY.Y, joint.InverseBindPoseMatrix.AxisY.Z), new Vector3(joint.InverseBindPoseMatrix.AxisZ.X, joint.InverseBindPoseMatrix.AxisZ.Y, joint.InverseBindPoseMatrix.AxisZ.Z), new Vector3(joint.InverseBindPoseMatrix.AxisW.X, joint.InverseBindPoseMatrix.AxisW.Y, joint.InverseBindPoseMatrix.AxisW.Z));
            bone.HasJointInverseBind = true;
        }
    }
    UpdateRestParentMatrices() {
        for (let i = 0; i < this.Bones.length; i++) {
            const bone = this.Bones[i];
            if (bone.ParentIndex >= 0 && bone.ParentIndex < this.Bones.length && bone.ParentIndex !== i) {
                bone.RestInvParentMatrix = Matrix4.Invert(this.Bones[bone.ParentIndex].RestLocalMatrix);
            }
            else {
                bone.RestInvParentMatrix = Matrix4.Identity;
            }
        }
    }
    ComputeInverseBindMatrices(useTrsklInverseBind) {
        if (this.Bones.length === 0) {
            return;
        }
        const bindWorld = new Array(this.Bones.length);
        const computed = new Array(this.Bones.length).fill(false);
        for (let i = 0; i < this.Bones.length; i++) {
            bindWorld[i] = this.ComputeBindWorld(i, useTrsklInverseBind, bindWorld, computed);
        }
        for (let i = 0; i < this.Bones.length; i++) {
            if (useTrsklInverseBind && this.Bones[i].HasJointInverseBind) {
                this.Bones[i].InverseBindWorld = this.Bones[i].JointInverseBindWorld;
            }
            else {
                this.Bones[i].InverseBindWorld = Matrix4.Invert(bindWorld[i]);
            }
        }
    }
    ComputeBindWorld(index, useTrsklInverseBind, world, computed) {
        if (computed[index]) {
            return world[index];
        }
        const bone = this.Bones[index];
        let local;
        if (useTrsklInverseBind && bone.HasJointInverseBind) {
            local = Matrix4.Invert(bone.JointInverseBindWorld);
        }
        else {
            local = bone.RestLocalMatrix;
        }
        if (bone.ParentIndex >= 0 && bone.ParentIndex < this.Bones.length && bone.ParentIndex !== index) {
            if (bone.UseSegmentScaleCompensate) {
                const parent = this.Bones[bone.ParentIndex];
                // Apply segment scale compensation
                local = new Matrix4(); // Stub - multiply by scale matrix
            }
            const parentWorld = this.ComputeBindWorld(bone.ParentIndex, useTrsklInverseBind, world, computed);
            world[index] = new Matrix4(); // Stub - multiply matrices
        }
        else {
            world[index] = local;
        }
        computed[index] = true;
        return world[index];
    }
    BuildSkinningPalette() {
        if (this._jointInfoToNode == null || this._jointInfoToNode.length === 0) {
            return [];
        }
        const palette = new Array(this._jointInfoToNode.length);
        for (let i = 0; i < palette.length; i++) {
            const nodeIndex = this._jointInfoToNode[i];
            palette[i] = nodeIndex >= 0 ? nodeIndex : 0;
        }
        return palette;
    }
    MapJointInfoIndex(jointInfoIndex) {
        if (jointInfoIndex < 0 || jointInfoIndex >= this._jointInfoToNode.length) {
            return 0;
        }
        const mapped = this._jointInfoToNode[jointInfoIndex];
        return mapped >= 0 ? mapped : 0;
    }
    MapBoneMetaIndex(_boneMetaIndex) {
        return 0;
    }
    ApplyJointInfoFromJson(_sourcePath) {
        // Stub - JSON parsing not implemented
    }
    static CreateMatrixFromAxis(axisX, axisY, axisZ, axisW) {
        // Stub implementation
        return Matrix4.Identity;
    }
}
export class Bone {
    Name;
    RestPosition;
    RestRotation;
    RestScale;
    RestEuler;
    RestLocalMatrix;
    RestInvParentMatrix = Matrix4.Identity;
    InverseBindWorld = Matrix4.Identity;
    JointInverseBindWorld = Matrix4.Identity;
    HasJointInverseBind = false;
    UseSegmentScaleCompensate = false;
    ParentIndex;
    Skinning = false;
    Parent = null;
    Children = [];
    // Mutable pose (used by animation)
    Position;
    Rotation;
    Scale;
    constructor(node, skinning) {
        this.Name = node.Name ?? '';
        this.Position = new Vector3(node.Transform.Translate.X, node.Transform.Translate.Y, node.Transform.Translate.Z);
        this.RestEuler = new Vector3(node.Transform.Rotate.X, node.Transform.Rotate.Y, node.Transform.Rotate.Z);
        this.Rotation = Bone.FromEulerXYZ(this.RestEuler);
        this.Scale = new Vector3(node.Transform.Scale.X, node.Transform.Scale.Y, node.Transform.Scale.Z);
        this.RestPosition = this.Position;
        this.RestRotation = this.Rotation;
        this.RestScale = this.Scale;
        this.RestLocalMatrix = Matrix4.Identity; // Stub - should be computed from scale, rotation, translation
        this.ParentIndex = node.ParentNodeIndex;
        this.Skinning = skinning;
        this.HasJointInverseBind = false;
        this.UseSegmentScaleCompensate = false;
    }
    AddChild(bone) {
        bone.Parent = this;
        this.Children.push(bone);
    }
    ResetPose() {
        this.Position = this.RestPosition;
        this.Rotation = this.RestRotation;
        this.Scale = this.RestScale;
    }
    static FromEulerXYZ(euler) {
        const qx = Quaternion.FromAxisAngle(Vector3.UnitX, euler.x);
        const qy = Quaternion.FromAxisAngle(Vector3.UnitY, euler.y);
        const qz = Quaternion.FromAxisAngle(Vector3.UnitZ, euler.z);
        // q = qz * qy * qx
        return Quaternion.Identity; // Stub - should multiply quaternions
    }
}
//# sourceMappingURL=TrinityArmature.js.map