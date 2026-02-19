import { Vector2f, Vector3f, Vector4f, RGBA } from '../../Common/Math.js';
export var UVWrapMode;
(function (UVWrapMode) {
    UVWrapMode[UVWrapMode["WRAP"] = 0] = "WRAP";
    UVWrapMode[UVWrapMode["CLAMP"] = 1] = "CLAMP";
    UVWrapMode[UVWrapMode["MIRROR"] = 6] = "MIRROR";
    UVWrapMode[UVWrapMode["MIRROR_ONCE"] = 7] = "MIRROR_ONCE";
})(UVWrapMode || (UVWrapMode = {}));
export class TRFloatParameter {
    Name = '';
    Value = 0;
}
export class TRVec2fParameter {
    Name = '';
    Value = new Vector2f();
}
export class TRVec3fParameter {
    Name = '';
    Value = new Vector3f();
}
export class TRVec4fParameter {
    Name = '';
    Value = new Vector4f();
}
export class TRStringParameter {
    Name = '';
    Value = '';
}
export class TRSampler {
    State0 = 0;
    State1 = 0;
    State2 = 0;
    State3 = 0;
    State4 = 0;
    State5 = 0;
    State6 = 0;
    State7 = 0;
    State8 = 0;
    RepeatU = UVWrapMode.WRAP;
    RepeatV = UVWrapMode.WRAP;
    RepeatW = UVWrapMode.WRAP;
    BorderColor = new RGBA();
}
export class TRTexture {
    Name = '';
    File = '';
    Slot = 0;
}
export class TRMaterialShader {
    Name = '';
    Values = [];
}
export class TRMaterial {
    Name = '';
    Shader = [];
    Textures = [];
    Samplers = [];
    FloatParams = [];
    Vec2fParams = [];
    Vec3fParams = [];
    Vec4fParams = [];
}
export class TRMTR {
    Field_00 = 0;
    Materials = [];
}
//# sourceMappingURL=TRMTR.js.map