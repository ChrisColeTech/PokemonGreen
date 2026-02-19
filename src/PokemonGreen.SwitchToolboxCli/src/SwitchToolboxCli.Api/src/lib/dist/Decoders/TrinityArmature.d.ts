import { Vector3, MathQuaternion, Matrix4 } from './Math.js';
import type { TRSKL, TRTransformNode } from '../Flatbuffers/TR/Model/index.js';
/**
 * Data-only skeleton/armature decoded from TRSKL.
 * Ported from gftool Armature.cs — all GL rendering code removed.
 */
export declare class TrinityArmature {
    Bones: Bone[];
    private _parentIndices;
    private _jointInfoToNode;
    private _skinningPaletteOffset;
    get ParentIndices(): readonly number[];
    get JointInfoCount(): number;
    get BoneMetaCount(): number;
    constructor(skel: TRSKL, sourcePath?: string, useTrsklInverseBind?: boolean);
    private ApplyJointInfoFromTrskl;
    private static ApplyTrsklJointInfoToBone;
    private UpdateRestParentMatrices;
    private ComputeInverseBindMatrices;
    private ComputeBindWorld;
    BuildSkinningPalette(): number[];
    MapJointInfoIndex(jointInfoIndex: number): number;
    MapBoneMetaIndex(_boneMetaIndex: number): number;
    private ApplyJointInfoFromJson;
    private static CreateMatrixFromAxis;
}
export declare class Bone {
    Name: string;
    RestPosition: Vector3;
    RestRotation: MathQuaternion;
    RestScale: Vector3;
    RestEuler: Vector3;
    RestLocalMatrix: Matrix4;
    RestInvParentMatrix: Matrix4;
    InverseBindWorld: Matrix4;
    JointInverseBindWorld: Matrix4;
    HasJointInverseBind: boolean;
    UseSegmentScaleCompensate: boolean;
    ParentIndex: number;
    Skinning: boolean;
    Parent: Bone | null;
    Children: Bone[];
    Position: Vector3;
    Rotation: MathQuaternion;
    Scale: Vector3;
    constructor(node: TRTransformNode, skinning: boolean);
    AddChild(bone: Bone): void;
    ResetPose(): void;
    private static FromEulerXYZ;
}
//# sourceMappingURL=TrinityArmature.d.ts.map