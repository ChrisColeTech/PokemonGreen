import { Vector2f, Vector3f, Vector4f, RGBA } from '../../Common/Math.js';
export declare enum UVWrapMode {
    WRAP = 0,
    CLAMP = 1,
    MIRROR = 6,
    MIRROR_ONCE = 7
}
export declare class TRFloatParameter {
    Name: string;
    Value: number;
}
export declare class TRVec2fParameter {
    Name: string;
    Value: Vector2f;
}
export declare class TRVec3fParameter {
    Name: string;
    Value: Vector3f;
}
export declare class TRVec4fParameter {
    Name: string;
    Value: Vector4f;
}
export declare class TRStringParameter {
    Name: string;
    Value: string;
}
export declare class TRSampler {
    State0: number;
    State1: number;
    State2: number;
    State3: number;
    State4: number;
    State5: number;
    State6: number;
    State7: number;
    State8: number;
    RepeatU: UVWrapMode;
    RepeatV: UVWrapMode;
    RepeatW: UVWrapMode;
    BorderColor: RGBA;
}
export declare class TRTexture {
    Name: string;
    File: string;
    Slot: number;
}
export declare class TRMaterialShader {
    Name: string;
    Values: TRStringParameter[];
}
export declare class TRMaterial {
    Name: string;
    Shader: TRMaterialShader[];
    Textures: TRTexture[];
    Samplers: TRSampler[];
    FloatParams: TRFloatParameter[];
    Vec2fParams: TRVec2fParameter[];
    Vec3fParams: TRVec3fParameter[];
    Vec4fParams: TRVec4fParameter[];
}
export declare class TRMTR {
    Field_00: number;
    Materials: TRMaterial[];
}
