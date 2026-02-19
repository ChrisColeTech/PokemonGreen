import { Vector3f, Vector4f } from '../../Common/Math.js';
export class Matrix4x3f {
    AxisX = new Vector3f();
    AxisY = new Vector3f();
    AxisZ = new Vector3f();
    AxisW = new Vector3f();
}
export class SRT {
    Scale = new Vector3f();
    Rotate = new Vector3f();
    Translate = new Vector3f();
}
export class TRTransformNode {
    Name = '';
    Transform = new SRT();
    ScalePivot = new Vector3f();
    RotatePivot = new Vector3f();
    ParentNodeIndex = -1;
    JointInfoIndex = -1;
    ParentNodeName = '';
    Priority = 0;
    PriorityPass = false;
    IgnoreParentRotation = false;
}
export class TRJointInfo {
    SegmentScaleCompensate = false;
    InfluenceSkinning = true;
    InverseBindPoseMatrix = new Matrix4x3f();
}
export class TRHelperBoneInfo {
    Output = '';
    Target = '';
    Reference = '';
    Type = '';
    UpType = '';
    Weight = new Vector3f();
    Adjust = new Vector4f();
}
export class TRSKL {
    Version = 2;
    TransformNodes = [];
    JointInfos = [];
    HelperBones = [];
    SkinningPaletteOffset = -1;
    IsInteriorMap = false;
}
//# sourceMappingURL=TRSKL.js.map