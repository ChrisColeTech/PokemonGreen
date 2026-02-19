/**
 * Lightweight texture reference (no GL texture loading).
 */
export class TextureRef {
    Name = '';
    FilePath = '';
    Slot = 0;
}
/**
 * Data-only material decoded from TRMTR / Gfx2 material files.
 * Ported from gftool Material.cs — all GL rendering code removed.
 */
export class TrinityMaterial {
    Name = '';
    ShaderName = '';
    Textures = [];
    ShaderParams = [];
    FloatParams = [];
    Vec2Params = [];
    Vec3Params = [];
    Vec4Params = [];
    Samplers = [];
    constructor(modelPath, trmat) {
        this.Name = trmat.Name ?? '';
        this.ShaderName = TrinityMaterial.ResolveShaderName(trmat.Shader?.length > 0 ? trmat.Shader[0].Name : '');
        this.FloatParams = trmat.FloatParams ?? [];
        this.Vec2Params = trmat.Vec2fParams ?? [];
        this.Vec3Params = trmat.Vec3fParams ?? [];
        this.Vec4Params = trmat.Vec4fParams ?? [];
        this.Samplers = trmat.Samplers ?? [];
        if (trmat.Shader != null && trmat.Shader.length > 0 && trmat.Shader[0].Values != null) {
            for (const param of trmat.Shader[0].Values) {
                this.ShaderParams.push({ Name: param.Name, Value: param.Value });
            }
        }
        for (const tex of trmat.Textures ?? []) {
            this.Textures.push({
                Name: tex.Name,
                FilePath: modelPath.combine(tex.File),
                Slot: tex.Slot
            });
        }
    }
    static ResolveShaderName(name) {
        if (!name || name.length === 0) {
            return 'Standard';
        }
        switch (name) {
            case 'Opaque': return 'Standard';
            case 'Transparent': return 'Transparent';
            case 'Hair': return 'Hair';
            case 'SSS': return 'SSS';
            case 'EyeClearCoat': return 'EyeClearCoat';
            case 'Unlit': return 'Unlit';
            default: return name;
        }
    }
}
//# sourceMappingURL=TrinityMaterial.js.map