import { Vector3, MathQuaternion, Matrix4 } from './Math.js';
import type { TRSKL, TRTransformNode, TRJointInfo, Matrix4x3f } from '../Flatbuffers/TR/Model/index.js';

interface JointInfoJson {
    SegmentScaleCompensate: boolean;
    InfluenceSkinning: boolean;
    HasInverseBind: boolean;
    InverseBind: Matrix4;
}

interface JointInfoParseResult {
    JointInfos: JointInfoJson[];
    NodeJointInfoIds: number[];
    NodeNames: string[];
}

/**
 * Data-only skeleton/armature decoded from TRSKL.
 * Ported from gftool Armature.cs — all GL rendering code removed.
 */
export class TrinityArmature {
    public Bones: Bone[] = [];
    private _parentIndices: number[] = [];
    private _jointInfoToNode: number[] = [];
    private _skinningPaletteOffset: number = 0;

    public get ParentIndices(): readonly number[] {
        return this._parentIndices;
    }

    public get JointInfoCount(): number {
        return this._jointInfoToNode.length;
    }

    public get BoneMetaCount(): number {
        return 0;
    }

    constructor(skel: TRSKL, sourcePath?: string, useTrsklInverseBind: boolean = true) {
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

    private ApplyJointInfoFromTrskl(skel: TRSKL): void {
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

    private static ApplyTrsklJointInfoToBone(bone: Bone, joint: TRJointInfo): void {
        bone.UseSegmentScaleCompensate = joint.SegmentScaleCompensate;
        bone.Skinning = joint.InfluenceSkinning;

        if (joint.InverseBindPoseMatrix != null) {
            bone.JointInverseBindWorld = TrinityArmature.CreateMatrixFromAxis(
                new Vector3(joint.InverseBindPoseMatrix.AxisX.X, joint.InverseBindPoseMatrix.AxisX.Y, joint.InverseBindPoseMatrix.AxisX.Z),
                new Vector3(joint.InverseBindPoseMatrix.AxisY.X, joint.InverseBindPoseMatrix.AxisY.Y, joint.InverseBindPoseMatrix.AxisY.Z),
                new Vector3(joint.InverseBindPoseMatrix.AxisZ.X, joint.InverseBindPoseMatrix.AxisZ.Y, joint.InverseBindPoseMatrix.AxisZ.Z),
                new Vector3(joint.InverseBindPoseMatrix.AxisW.X, joint.InverseBindPoseMatrix.AxisW.Y, joint.InverseBindPoseMatrix.AxisW.Z)
            );
            bone.HasJointInverseBind = true;
        }
    }

    private UpdateRestParentMatrices(): void {
        for (let i = 0; i < this.Bones.length; i++) {
            const bone = this.Bones[i];
            if (bone.ParentIndex >= 0 && bone.ParentIndex < this.Bones.length && bone.ParentIndex !== i) {
                bone.RestInvParentMatrix = Matrix4.Invert(this.Bones[bone.ParentIndex].RestLocalMatrix);
            } else {
                bone.RestInvParentMatrix = Matrix4.Identity;
            }
        }
    }

    private ComputeInverseBindMatrices(useTrsklInverseBind: boolean): void {
        if (this.Bones.length === 0) {
            return;
        }

        const bindWorld: Matrix4[] = new Array(this.Bones.length);
        const computed: boolean[] = new Array(this.Bones.length).fill(false);
        for (let i = 0; i < this.Bones.length; i++) {
            bindWorld[i] = this.ComputeBindWorld(i, useTrsklInverseBind, bindWorld, computed);
        }

        for (let i = 0; i < this.Bones.length; i++) {
            if (useTrsklInverseBind && this.Bones[i].HasJointInverseBind) {
                this.Bones[i].InverseBindWorld = this.Bones[i].JointInverseBindWorld;
            } else {
                this.Bones[i].InverseBindWorld = Matrix4.Invert(bindWorld[i]);
            }
        }
    }

    private ComputeBindWorld(index: number, useTrsklInverseBind: boolean, world: Matrix4[], computed: boolean[]): Matrix4 {
        if (computed[index]) {
            return world[index];
        }

        const bone = this.Bones[index];
        let local: Matrix4;
        if (useTrsklInverseBind && bone.HasJointInverseBind) {
            local = Matrix4.Invert(bone.JointInverseBindWorld);
        } else {
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
        } else {
            world[index] = local;
        }

        computed[index] = true;
        return world[index];
    }

    public BuildSkinningPalette(): number[] {
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

    public MapJointInfoIndex(jointInfoIndex: number): number {
        if (jointInfoIndex < 0 || jointInfoIndex >= this._jointInfoToNode.length) {
            return 0;
        }
        const mapped = this._jointInfoToNode[jointInfoIndex];
        return mapped >= 0 ? mapped : 0;
    }

    public MapBoneMetaIndex(_boneMetaIndex: number): number {
        return 0;
    }

    private ApplyJointInfoFromJson(_sourcePath?: string): void {
        // Stub - JSON parsing not implemented
    }

    private static CreateMatrixFromAxis(axisX: Vector3, axisY: Vector3, axisZ: Vector3, axisW: Vector3): Matrix4 {
        // Stub implementation
        return Matrix4.Identity;
    }
}

export class Bone {
    public Name: string;
    public RestPosition: Vector3;
    public RestRotation: MathQuaternion;
    public RestScale: Vector3;
    public RestEuler: Vector3;
    public RestLocalMatrix: Matrix4;
    public RestInvParentMatrix: Matrix4 = Matrix4.Identity;
    public InverseBindWorld: Matrix4 = Matrix4.Identity;
    public JointInverseBindWorld: Matrix4 = Matrix4.Identity;
    public HasJointInverseBind: boolean = false;
    public UseSegmentScaleCompensate: boolean = false;
    public ParentIndex: number;
    public Skinning: boolean = false;
    public Parent: Bone | null = null;
    public Children: Bone[] = [];

    // Mutable pose (used by animation)
    public Position: Vector3;
    public Rotation: MathQuaternion;
    public Scale: Vector3;

    constructor(node: TRTransformNode, skinning: boolean) {
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

    public AddChild(bone: Bone): void {
        bone.Parent = this;
        this.Children.push(bone);
    }

    public ResetPose(): void {
        this.Position = this.RestPosition;
        this.Rotation = this.RestRotation;
        this.Scale = this.RestScale;
    }

    private static FromEulerXYZ(euler: Vector3): MathQuaternion {
        const qx = MathQuaternion.FromAxisAngle(Vector3.UnitX, euler.x);
        const qy = MathQuaternion.FromAxisAngle(Vector3.UnitY, euler.y);
        const qz = MathQuaternion.FromAxisAngle(Vector3.UnitZ, euler.z);
        // q = qz * qy * qx
        return MathQuaternion.Identity; // Stub - should multiply quaternions
    }
}
