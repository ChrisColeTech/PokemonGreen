/**
 * BNTX (Binary NX Texture) decoder for Nintendo Switch textures.
 *
 * Decodes BNTX files to RGBA pixel data.
 * Uses TegraSwizzle for GPU deswizzling.
 * Ported from C# BntxDecoder.cs
 */
/**
 * Decoded texture ready for export.
 */
export declare class BntxTexture {
    name: string;
    width: number;
    height: number;
    format: BntxFormat;
    mipmaps: number;
    rgbaData: Buffer;
    /**
     * Save this texture as a PNG file.
     * Note: This is a stub. Actual PNG encoding requires additional libraries.
     */
    savePng(path: string): void;
    get Name(): string;
    get Width(): number;
    get Height(): number;
    get Format(): BntxFormat;
    get Mipmaps(): number;
    get Data(): Buffer;
}
/**
 * BNTX format enum values
 */
export declare enum BntxFormat {
    R8_UNORM = "R8_UNORM",
    R8G8_UNORM = "R8G8_UNORM",
    R8G8B8A8_UNORM = "R8G8B8A8_UNORM",
    R8G8B8A8_SRGB = "R8G8B8A8_SRGB",
    B8G8R8A8_UNORM = "B8G8R8A8_UNORM",
    B8G8R8A8_SRGB = "B8G8R8A8_SRGB",
    BC1_UNORM = "BC1_UNORM",
    BC1_SRGB = "BC1_SRGB",
    BC2_UNORM = "BC2_UNORM",
    BC2_SRGB = "BC2_SRGB",
    BC3_UNORM = "BC3_UNORM",
    BC3_SRGB = "BC3_SRGB",
    BC4_UNORM = "BC4_UNORM",
    BC4_SNORM = "BC4_SNORM",
    BC5_UNORM = "BC5_UNORM",
    BC5_SNORM = "BC5_SNORM",
    BC7_UNORM = "BC7_UNORM",
    BC7_SRGB = "BC7_SRGB",
    ASTC_4x4_UNORM = "ASTC_4x4_UNORM",
    ASTC_4x4_SRGB = "ASTC_4x4_SRGB",
    ASTC_5x4_UNORM = "ASTC_5x4_UNORM",
    ASTC_5x4_SRGB = "ASTC_5x4_SRGB",
    ASTC_5x5_UNORM = "ASTC_5x5_UNORM",
    ASTC_5x5_SRGB = "ASTC_5x5_SRGB",
    ASTC_6x5_UNORM = "ASTC_6x5_UNORM",
    ASTC_6x5_SRGB = "ASTC_6x5_SRGB",
    ASTC_6x6_UNORM = "ASTC_6x6_UNORM",
    ASTC_6x6_SRGB = "ASTC_6x6_SRGB",
    ASTC_8x5_UNORM = "ASTC_8x5_UNORM",
    ASTC_8x5_SRGB = "ASTC_8x5_SRGB",
    ASTC_8x6_UNORM = "ASTC_8x6_UNORM",
    ASTC_8x6_SRGB = "ASTC_8x6_SRGB",
    ASTC_8x8_UNORM = "ASTC_8x8_UNORM",
    ASTC_8x8_SRGB = "ASTC_8x8_SRGB",
    ASTC_10x5_UNORM = "ASTC_10x5_UNORM",
    ASTC_10x5_SRGB = "ASTC_10x5_SRGB",
    ASTC_10x6_UNORM = "ASTC_10x6_UNORM",
    ASTC_10x6_SRGB = "ASTC_10x6_SRGB",
    ASTC_10x8_UNORM = "ASTC_10x8_UNORM",
    ASTC_10x8_SRGB = "ASTC_10x8_SRGB",
    ASTC_10x10_UNORM = "ASTC_10x10_UNORM",
    ASTC_10x10_SRGB = "ASTC_10x10_SRGB",
    ASTC_12x10_UNORM = "ASTC_12x10_UNORM",
    ASTC_12x10_SRGB = "ASTC_12x10_SRGB",
    ASTC_12x12_UNORM = "ASTC_12x12_UNORM",
    ASTC_12x12_SRGB = "ASTC_12x12_SRGB",
    Unknown = "Unknown"
}
/**
 * Decodes BNTX (Binary NX Texture) files to RGBA pixel data.
 */
export declare class BntxDecoder {
    /**
     * Decode all textures from a BNTX file path.
     */
    static decodeFile(path: string): BntxTexture[];
    /**
     * Decode all textures from a BNTX buffer.
     */
    static decode(bntxData: Buffer): BntxTexture[];
    /**
     * Parse texture data from the BNTX file.
     */
    private static parseTextures;
    /**
     * Parse individual texture data.
     */
    private static parseTextureData;
    /**
     * Decode a single texture to RGBA.
     */
    private static decodeTexture;
    /**
     * Decode format-specific data to RGBA.
     */
    private static decodeFormatToRgba;
    /**
     * Decode BCn compressed data.
     * Note: This is a stub. Real BCn decoding requires a proper implementation.
     */
    private static decodeBcn;
    /**
     * Decode ASTC compressed data.
     * Note: This is a stub. Real ASTC decoding requires a native library.
     */
    private static decodeAstc;
    /**
     * Convert BGRA to RGBA.
     */
    private static convertBgraToRgba;
    /**
     * Expand R8 to RGBA8.
     */
    private static expandR8;
    /**
     * Expand RG8 to RGBA8.
     */
    private static expandRg8;
    /**
     * Create a solid color placeholder image.
     */
    private static createPlaceholder;
    /**
     * Get format information.
     */
    private static getFormatInfo;
    /**
     * Convert raw format value to BntxFormat enum.
     */
    private static convertFormat;
}
export default BntxDecoder;
