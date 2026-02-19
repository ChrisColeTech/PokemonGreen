import { Vector2f, Vector3f, Vector4f } from '../Common/Math.js';
export class IntParam {
    Name = '';
    Value = 0;
}
export class FloatParam {
    Name = '';
    Value = 0.0;
}
export class Vector2fParam {
    Name = '';
    Value = new Vector2f();
}
export class Vector3fParam {
    Name = '';
    Value = new Vector3f();
}
export class Vector4fParam {
    Name = '';
    Value = new Vector4f();
}
export class TextureParam {
    Name = '';
    FilePath = '';
    SamplerId = 0;
}
export class ShaderOption {
    Name = '';
    Choice = '';
}
export class Technique {
    Name = '';
    ShaderOptions = [];
}
export class MaterialItem {
    Name = '';
    TechniqueList = [];
    TextureParamList = [];
    FloatParamList = [];
    Vector2fParamList = [];
    Vector3fParamList = [];
    Vector4fParamList = [];
    IntParamList = [];
}
export class Material {
    Version = 0;
    ItemList = [];
}
//# sourceMappingURL=Material.js.map