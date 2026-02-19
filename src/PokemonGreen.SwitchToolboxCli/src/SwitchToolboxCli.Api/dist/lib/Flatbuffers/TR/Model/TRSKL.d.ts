import { Vector3f, Vector4f } from '../../Common/Math.js';
export declare class Matrix4x3f {
    AxisX: Vector3f;
    AxisY: Vector3f;
    AxisZ: Vector3f;
    AxisW: Vector3f;
}
export declare class SRT {
    Scale: Vector3f;
    Rotate: Vector3f;
    Translate: Vector3f;
}
export declare class TRTransformNode {
    Name: string;
    Transform: SRT;
    ScalePivot: Vector3f;
    RotatePivot: Vector3f;
    ParentNodeIndex: number;
    JointInfoIndex: number;
    ParentNodeName: string;
    Priority: number;
    PriorityPass: boolean;
    IgnoreParentRotation: boolean;
}
export declare class TRJointInfo {
    SegmentScaleCompensate: boolean;
    InfluenceSkinning: boolean;
    InverseBindPoseMatrix: Matrix4x3f;
}
export declare class TRHelperBoneInfo {
    Output: string;
    Target: string;
    Reference: string;
    Type: string;
    UpType: string;
    Weight: Vector3f;
    Adjust: Vector4f;
}
export declare class TRSKL {
    Version: number;
    TransformNodes: TRTransformNode[];
    JointInfos: TRJointInfo[];
    HelperBones: TRHelperBoneInfo[];
    SkinningPaletteOffset: number;
    IsInteriorMap: boolean;
}
