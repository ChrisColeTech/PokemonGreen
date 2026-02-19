import { Vector3f, PackedQuaternion, Transform } from '../../Common/Math.js';
export declare enum PlayType {
    Once = 0,
    Looped = 1
}
export declare class FixedVectorTrack {
    Value: Vector3f;
}
export declare class FramedVectorTrack {
    Values: Vector3f[];
}
export declare class Keyed16VectorTrack {
    Keys: number[];
    Values: Vector3f[];
}
export declare class Keyed8VectorTrack {
    Keys: number[];
    Values: Vector3f[];
}
export declare class FixedRotationTrack {
    Value: PackedQuaternion;
}
export declare class FramedRotationTrack {
    Values: PackedQuaternion[];
}
export declare class Keyed16RotationTrack {
    Keys: number[];
    Values: PackedQuaternion[];
}
export declare class Keyed8RotationTrack {
    Keys: number[];
    Values: PackedQuaternion[];
}
export type VectorTrackUnion = FixedVectorTrack | FramedVectorTrack | Keyed16VectorTrack | Keyed8VectorTrack;
export type RotationTrackUnion = FixedRotationTrack | FramedRotationTrack | Keyed16RotationTrack | Keyed8RotationTrack;
export declare class SkeletalTrack {
    BoneName: string;
    ScaleChannel: VectorTrackUnion | null;
    RotationChannel: RotationTrackUnion | null;
    TranslateChannel: VectorTrackUnion | null;
}
export declare class PlaybackInfo {
    PlayType: PlayType;
    FrameCount: number;
    FrameRate: number;
}
export declare class BoneInit {
    IsInit: number;
    BoneTransform: Transform;
}
export declare class SkeletalAnimation {
    Tracks: SkeletalTrack[];
    Init: BoneInit;
}
export declare class TRANM {
    Info: PlaybackInfo;
    SkeletalAnimation: SkeletalAnimation;
}
