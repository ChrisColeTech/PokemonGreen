import { Vector3f, PackedQuaternion, Transform } from '../../Common/Math.js';
export var PlayType;
(function (PlayType) {
    PlayType[PlayType["Once"] = 0] = "Once";
    PlayType[PlayType["Looped"] = 1] = "Looped";
})(PlayType || (PlayType = {}));
export class FixedVectorTrack {
    Value = new Vector3f();
}
export class FramedVectorTrack {
    Values = [];
}
export class Keyed16VectorTrack {
    Keys = [];
    Values = [];
}
export class Keyed8VectorTrack {
    Keys = [];
    Values = [];
}
export class FixedRotationTrack {
    Value = new PackedQuaternion();
}
export class FramedRotationTrack {
    Values = [];
}
export class Keyed16RotationTrack {
    Keys = [];
    Values = [];
}
export class Keyed8RotationTrack {
    Keys = [];
    Values = [];
}
export class SkeletalTrack {
    BoneName = '';
    ScaleChannel = null;
    RotationChannel = null;
    TranslateChannel = null;
}
export class PlaybackInfo {
    PlayType = PlayType.Once;
    FrameCount = 0;
    FrameRate = 0;
}
export class BoneInit {
    IsInit = 0;
    BoneTransform = new Transform();
}
export class SkeletalAnimation {
    Tracks = [];
    Init = new BoneInit();
}
export class TRANM {
    Info = new PlaybackInfo();
    SkeletalAnimation = new SkeletalAnimation();
}
//# sourceMappingURL=SkeletalAnimation.js.map