import { Vector3f, PackedQuaternion, Transform } from '../../Common/Math.js';
export declare class FixedVectorTrack {
    Co: Vector3f;
}
export declare class DynamicVectorTrack {
    Co: Vector3f[];
}
export declare class Framed16VectorTrack {
    Frames: number[];
    Co: Vector3f[];
}
export declare class Framed8VectorTrack {
    Frames: number[];
    Co: Vector3f[];
}
export declare class FixedRotationTrack {
    Co: PackedQuaternion;
}
export declare class DynamicRotationTrack {
    Co: PackedQuaternion[];
}
export declare class Framed16RotationTrack {
    Frames: number[];
    Co: PackedQuaternion[];
}
export declare class Framed8RotationTrack {
    Frames: number[];
    Co: PackedQuaternion[];
}
export type VectorTrackUnion = FixedVectorTrack | DynamicVectorTrack | Framed16VectorTrack | Framed8VectorTrack;
export type RotationTrackUnion = FixedRotationTrack | DynamicRotationTrack | Framed16RotationTrack | Framed8RotationTrack;
export declare class BoneTrack {
    Name: string;
    Scale: VectorTrackUnion | null;
    Rotate: RotationTrackUnion | null;
    Translate: VectorTrackUnion | null;
}
export declare class BoneInit {
    IsInit: number;
    Transform: Transform;
}
export declare class BoneAnimation {
    Tracks: BoneTrack[];
    InitData: BoneInit | null;
}
export declare class Info {
    DoesLoop: number;
    KeyFrames: number;
    FrameRate: number;
}
export declare class Animation {
    Info: Info;
    Skeleton: BoneAnimation;
}
