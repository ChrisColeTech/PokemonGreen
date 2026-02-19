import { Vector2f, Vector3f, Vector4f } from '../Common/Math.js';
export declare class IntParam {
    Name: string;
    Value: number;
}
export declare class FloatParam {
    Name: string;
    Value: number;
}
export declare class Vector2fParam {
    Name: string;
    Value: Vector2f;
}
export declare class Vector3fParam {
    Name: string;
    Value: Vector3f;
}
export declare class Vector4fParam {
    Name: string;
    Value: Vector4f;
}
export declare class TextureParam {
    Name: string;
    FilePath: string;
    SamplerId: number;
}
export declare class ShaderOption {
    Name: string;
    Choice: string;
}
export declare class Technique {
    Name: string;
    ShaderOptions: ShaderOption[];
}
export declare class MaterialItem {
    Name: string;
    TechniqueList: Technique[];
    TextureParamList: TextureParam[];
    FloatParamList: FloatParam[];
    Vector2fParamList: Vector2fParam[];
    Vector3fParamList: Vector3fParam[];
    Vector4fParamList: Vector4fParam[];
    IntParamList: IntParam[];
}
export declare class Material {
    Version: number;
    ItemList: MaterialItem[];
}
