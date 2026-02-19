import { Vector3f, PackedQuaternion, Transform } from '../../Common/Math.js';
export class FixedVectorTrack {
    Co = new Vector3f();
}
export class DynamicVectorTrack {
    Co = [];
}
export class Framed16VectorTrack {
    Frames = [];
    Co = [];
}
export class Framed8VectorTrack {
    Frames = [];
    Co = [];
}
export class FixedRotationTrack {
    Co = new PackedQuaternion();
}
export class DynamicRotationTrack {
    Co = [];
}
export class Framed16RotationTrack {
    Frames = [];
    Co = [];
}
export class Framed8RotationTrack {
    Frames = [];
    Co = [];
}
export class BoneTrack {
    Name = '';
    Scale = null;
    Rotate = null;
    Translate = null;
}
export class BoneInit {
    IsInit = 0;
    Transform = new Transform();
}
export class BoneAnimation {
    Tracks = [];
    InitData = null;
}
export class Info {
    DoesLoop = 0;
    KeyFrames = 0;
    FrameRate = 0;
}
export class Animation {
    Info = new Info();
    Skeleton = new BoneAnimation();
}
//# sourceMappingURL=GfAnimation.js.map