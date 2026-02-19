import type { PathString } from '../Utils/PathString.js';
import type { TRMaterial, TRFloatParameter, TRVec2fParameter, TRVec3fParameter, TRVec4fParameter, TRSampler } from '../Flatbuffers/TR/Model/index.js';
/**
 * Lightweight texture reference (no GL texture loading).
 */
export declare class TextureRef {
    Name: string;
    FilePath: string;
    Slot: number;
}
/**
 * Data-only material decoded from TRMTR / Gfx2 material files.
 * Ported from gftool Material.cs — all GL rendering code removed.
 */
export declare class TrinityMaterial {
    Name: string;
    ShaderName: string;
    Textures: TextureRef[];
    ShaderParams: Array<{
        Name: string;
        Value: string;
    }>;
    FloatParams: TRFloatParameter[];
    Vec2Params: TRVec2fParameter[];
    Vec3Params: TRVec3fParameter[];
    Vec4Params: TRVec4fParameter[];
    Samplers: TRSampler[];
    constructor(modelPath: PathString, trmat: TRMaterial);
    private static ResolveShaderName;
}
