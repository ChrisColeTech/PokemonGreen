/**
 * Bakes eye albedo textures from EyeClearCoat material parameters.
 * In-game, the EyeClearCoat shader uses BaseColorLayer1-4 + LayerMaskMap
 * to composite the eye color at runtime. Since DAE/Blender don't support
 * this shader, we bake a flat albedo texture from the material data.
 */
import type { TrinityMaterial } from '../Decoders/TrinityMaterial.js';
export declare class EyeTextureBaker {
    /**
     * Check if a material uses the EyeClearCoat shader and can be baked.
     */
    static IsEyeMaterial(material: TrinityMaterial): boolean;
    /**
     * Bake an eye albedo texture and save it, replacing the blank placeholder.
     * Returns the path to the baked texture, or null if baking fails.
     */
    static BakeEyeTexture(material: TrinityMaterial, tempRoot: string, texOutDir: string): string | null;
    private static ExtractBaseColors;
    private static ExtractEmissionColors;
    private static ExtractEmissionIntensities;
    private static GetAlbedoFileName;
    private static FindBntxFile;
    private static DecodeBntxToImage;
    /**
     * Convert linear-space value to sRGB gamma.
     * The standard sRGB transfer function.
     */
    private static LinearToSrgb;
    private static SaveAsPng;
}
