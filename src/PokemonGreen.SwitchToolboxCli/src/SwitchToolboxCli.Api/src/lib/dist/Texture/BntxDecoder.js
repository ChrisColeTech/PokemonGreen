/**
 * BNTX (Binary NX Texture) decoder for Nintendo Switch textures.
 *
 * Decodes BNTX files to RGBA pixel data.
 * Uses TegraSwizzle for GPU deswizzling.
 * Ported from C# BntxDecoder.cs
 */
import * as fs from 'fs';
import { TegraSwizzle } from './TegraSwizzle.js';
/**
 * Decoded texture ready for export.
 */
export class BntxTexture {
    name = '';
    width = 0;
    height = 0;
    format = BntxFormat.Unknown;
    mipmaps = 0;
    rgbaData = Buffer.alloc(0);
    /**
     * Save this texture as a PNG file.
     * Note: This is a stub. Actual PNG encoding requires additional libraries.
     */
    savePng(path) {
        // PNG encoding is complex - this is a placeholder
        // In a real implementation, you would use a library like 'pngjs' or 'sharp'
        console.log(`[BntxTexture] PNG save stub - would save to: ${path}`);
        console.log(`  Name: ${this.name}, Size: ${this.width}x${this.height}`);
        // For now, just save raw RGBA data
        const rawPath = path.replace('.png', '.raw');
        fs.writeFileSync(rawPath, this.rgbaData);
        console.log(`  Saved raw RGBA data to: ${rawPath}`);
    }
    get Name() {
        return this.name;
    }
    get Width() {
        return this.width;
    }
    get Height() {
        return this.height;
    }
    get Format() {
        return this.format;
    }
    get Mipmaps() {
        return this.mipmaps;
    }
    get Data() {
        return this.rgbaData;
    }
}
/**
 * BNTX format enum values
 */
export var BntxFormat;
(function (BntxFormat) {
    BntxFormat["R8_UNORM"] = "R8_UNORM";
    BntxFormat["R8G8_UNORM"] = "R8G8_UNORM";
    BntxFormat["R8G8B8A8_UNORM"] = "R8G8B8A8_UNORM";
    BntxFormat["R8G8B8A8_SRGB"] = "R8G8B8A8_SRGB";
    BntxFormat["B8G8R8A8_UNORM"] = "B8G8R8A8_UNORM";
    BntxFormat["B8G8R8A8_SRGB"] = "B8G8R8A8_SRGB";
    BntxFormat["BC1_UNORM"] = "BC1_UNORM";
    BntxFormat["BC1_SRGB"] = "BC1_SRGB";
    BntxFormat["BC2_UNORM"] = "BC2_UNORM";
    BntxFormat["BC2_SRGB"] = "BC2_SRGB";
    BntxFormat["BC3_UNORM"] = "BC3_UNORM";
    BntxFormat["BC3_SRGB"] = "BC3_SRGB";
    BntxFormat["BC4_UNORM"] = "BC4_UNORM";
    BntxFormat["BC4_SNORM"] = "BC4_SNORM";
    BntxFormat["BC5_UNORM"] = "BC5_UNORM";
    BntxFormat["BC5_SNORM"] = "BC5_SNORM";
    BntxFormat["BC7_UNORM"] = "BC7_UNORM";
    BntxFormat["BC7_SRGB"] = "BC7_SRGB";
    BntxFormat["ASTC_4x4_UNORM"] = "ASTC_4x4_UNORM";
    BntxFormat["ASTC_4x4_SRGB"] = "ASTC_4x4_SRGB";
    BntxFormat["ASTC_5x4_UNORM"] = "ASTC_5x4_UNORM";
    BntxFormat["ASTC_5x4_SRGB"] = "ASTC_5x4_SRGB";
    BntxFormat["ASTC_5x5_UNORM"] = "ASTC_5x5_UNORM";
    BntxFormat["ASTC_5x5_SRGB"] = "ASTC_5x5_SRGB";
    BntxFormat["ASTC_6x5_UNORM"] = "ASTC_6x5_UNORM";
    BntxFormat["ASTC_6x5_SRGB"] = "ASTC_6x5_SRGB";
    BntxFormat["ASTC_6x6_UNORM"] = "ASTC_6x6_UNORM";
    BntxFormat["ASTC_6x6_SRGB"] = "ASTC_6x6_SRGB";
    BntxFormat["ASTC_8x5_UNORM"] = "ASTC_8x5_UNORM";
    BntxFormat["ASTC_8x5_SRGB"] = "ASTC_8x5_SRGB";
    BntxFormat["ASTC_8x6_UNORM"] = "ASTC_8x6_UNORM";
    BntxFormat["ASTC_8x6_SRGB"] = "ASTC_8x6_SRGB";
    BntxFormat["ASTC_8x8_UNORM"] = "ASTC_8x8_UNORM";
    BntxFormat["ASTC_8x8_SRGB"] = "ASTC_8x8_SRGB";
    BntxFormat["ASTC_10x5_UNORM"] = "ASTC_10x5_UNORM";
    BntxFormat["ASTC_10x5_SRGB"] = "ASTC_10x5_SRGB";
    BntxFormat["ASTC_10x6_UNORM"] = "ASTC_10x6_UNORM";
    BntxFormat["ASTC_10x6_SRGB"] = "ASTC_10x6_SRGB";
    BntxFormat["ASTC_10x8_UNORM"] = "ASTC_10x8_UNORM";
    BntxFormat["ASTC_10x8_SRGB"] = "ASTC_10x8_SRGB";
    BntxFormat["ASTC_10x10_UNORM"] = "ASTC_10x10_UNORM";
    BntxFormat["ASTC_10x10_SRGB"] = "ASTC_10x10_SRGB";
    BntxFormat["ASTC_12x10_UNORM"] = "ASTC_12x10_UNORM";
    BntxFormat["ASTC_12x10_SRGB"] = "ASTC_12x10_SRGB";
    BntxFormat["ASTC_12x12_UNORM"] = "ASTC_12x12_UNORM";
    BntxFormat["ASTC_12x12_SRGB"] = "ASTC_12x12_SRGB";
    BntxFormat["Unknown"] = "Unknown";
})(BntxFormat || (BntxFormat = {}));
/**
 * Decodes BNTX (Binary NX Texture) files to RGBA pixel data.
 */
export class BntxDecoder {
    /**
     * Decode all textures from a BNTX file path.
     */
    static decodeFile(path) {
        const data = fs.readFileSync(path);
        return this.decode(data);
    }
    /**
     * Decode all textures from a BNTX buffer.
     */
    static decode(bntxData) {
        let offset = 0;
        // Read BNTX header
        const magic = bntxData.toString('ascii', offset, offset + 4);
        offset += 4;
        if (magic !== 'BNTX') {
            throw new Error(`Invalid BNTX file: expected magic 'BNTX', got '${magic}'`);
        }
        // Read version (skip)
        offset += 4;
        // Read BOM (skip)
        offset += 2;
        // Read header size (skip)
        offset += 2;
        // Read file size (skip)
        offset += 4;
        const dataBlocks = bntxData.readUInt16LE(offset);
        offset += 2;
        // Skip padding
        offset += 2;
        // Read platform target
        const platformTarget = bntxData.toString('ascii', offset, offset + 4);
        offset += 4;
        // Parse texture info blocks
        const textures = [];
        // For this implementation, we'll create a simplified parser
        // In a full implementation, this would parse the entire BNTX structure
        // Read texture dictionary offset
        const texDictOffset = bntxData.readUInt32LE(offset);
        offset += 4;
        // For now, let's try to extract basic texture info
        // This is a simplified parser - a complete implementation would be much more complex
        try {
            const textureDataList = this.parseTextures(bntxData, texDictOffset, dataBlocks);
            for (const texData of textureDataList) {
                try {
                    const decoded = this.decodeTexture(texData, platformTarget);
                    if (decoded) {
                        textures.push(decoded);
                    }
                }
                catch (ex) {
                    console.error(`[BntxDecoder] Failed to decode texture '${texData.name}': ${ex}`);
                }
            }
        }
        catch (ex) {
            console.error(`[BntxDecoder] Failed to parse BNTX file: ${ex}`);
        }
        return textures;
    }
    /**
     * Parse texture data from the BNTX file.
     */
    static parseTextures(data, dictOffset, textureCount) {
        const textures = [];
        // Navigate to texture dictionary
        let offset = dictOffset;
        // Read dictionary header
        const dictMagic = data.toString('ascii', offset, offset + 4);
        if (dictMagic !== '_DIC') {
            console.warn(`[BntxDecoder] Unexpected dictionary magic: ${dictMagic}`);
            return textures;
        }
        offset += 4;
        // Skip dictionary size
        offset += 4;
        const entryCount = data.readUInt32LE(offset);
        offset += 4;
        // Read texture entries
        for (let i = 0; i < Math.min(entryCount, textureCount); i++) {
            const textureOffset = data.readUInt32LE(offset);
            offset += 4;
            if (textureOffset === 0)
                continue;
            const texData = this.parseTextureData(data, textureOffset);
            if (texData) {
                textures.push(texData);
            }
        }
        return textures;
    }
    /**
     * Parse individual texture data.
     */
    static parseTextureData(data, offset) {
        try {
            // Check texture header magic
            const magic = data.toString('ascii', offset, offset + 4);
            if (magic !== 'BRTI') {
                console.warn(`[BntxDecoder] Unexpected texture magic: ${magic}`);
                return null;
            }
            offset += 4;
            // Skip texture info header size
            offset += 4;
            const flags = data.readUInt32LE(offset);
            offset += 4;
            const width = data.readUInt32LE(offset);
            offset += 4;
            const height = data.readUInt32LE(offset);
            offset += 4;
            const depth = data.readUInt32LE(offset);
            offset += 4;
            const arrayLength = data.readUInt32LE(offset);
            offset += 4;
            const mipCount = data.readUInt32LE(offset);
            offset += 4;
            const format = data.readUInt32LE(offset);
            offset += 4;
            // Skip access flags
            offset += 4;
            // Read texture data offsets
            const dataOffset = data.readUInt32LE(offset);
            offset += 4;
            // Read name offset
            const nameOffset = data.readUInt32LE(offset);
            offset += 4;
            // Read texture name
            let name = 'unknown';
            if (nameOffset > 0 && nameOffset < data.length) {
                const nameLen = data.readUInt16LE(nameOffset);
                name = data.toString('utf8', nameOffset + 2, nameOffset + 2 + nameLen);
            }
            // Read texture data
            const textureData = [];
            if (dataOffset > 0 && dataOffset < data.length) {
                // For simplicity, read just the first mip level
                const mipSize = width * height * 4; // Estimate
                const mipData = Buffer.allocUnsafe(Math.min(mipSize, data.length - dataOffset));
                data.copy(mipData, 0, dataOffset, dataOffset + mipData.length);
                textureData.push([mipData]);
            }
            return {
                name,
                width,
                height,
                depth: depth || 1,
                format,
                tileMode: (flags >> 8) & 0xFF,
                blockHeightLog2: (flags >> 16) & 0xF,
                mipCount,
                arrayLength,
                textureData,
            };
        }
        catch (ex) {
            console.error(`[BntxDecoder] Error parsing texture data: ${ex}`);
            return null;
        }
    }
    /**
     * Decode a single texture to RGBA.
     */
    static decodeTexture(tex, platformTarget) {
        const target = platformTarget === 'NX  ' ? 1 : 0;
        const format = this.convertFormat(tex.format);
        const formatInfo = this.getFormatInfo(format);
        const width = tex.width;
        const height = tex.height;
        const depth = Math.max(1, tex.depth);
        if (tex.textureData.length === 0 || tex.textureData[0].length === 0) {
            throw new Error(`Texture '${tex.name}' has no image data`);
        }
        const mipWidth = width;
        const mipHeight = height;
        // Deswizzle
        const swizzledData = tex.textureData[0][0]; // Array 0, Mip 0
        const deswizzled = TegraSwizzle.deswizzle(mipWidth, mipHeight, depth, formatInfo.blkWidth, formatInfo.blkHeight, 1, target, formatInfo.bpp, tex.tileMode, Math.max(0, tex.blockHeightLog2), swizzledData);
        // Trim to exact size needed
        const expectedSize = TegraSwizzle.divRoundUp(mipWidth, formatInfo.blkWidth) *
            TegraSwizzle.divRoundUp(mipHeight, formatInfo.blkHeight) *
            formatInfo.bpp;
        let finalData = deswizzled;
        if (deswizzled.length > expectedSize) {
            finalData = deswizzled.slice(0, expectedSize);
        }
        // Decode to RGBA
        const rgba = this.decodeFormatToRgba(finalData, width, height, format, formatInfo);
        const decoded = new BntxTexture();
        decoded.name = tex.name;
        decoded.width = width;
        decoded.height = height;
        decoded.format = format;
        decoded.mipmaps = tex.mipCount;
        decoded.rgbaData = rgba;
        return decoded;
    }
    /**
     * Decode format-specific data to RGBA.
     */
    static decodeFormatToRgba(data, width, height, format, _info) {
        switch (format) {
            // Uncompressed formats - already linear pixels
            case BntxFormat.R8G8B8A8_UNORM:
            case BntxFormat.R8G8B8A8_SRGB:
                return data;
            case BntxFormat.B8G8R8A8_UNORM:
            case BntxFormat.B8G8R8A8_SRGB:
                return this.convertBgraToRgba(data);
            case BntxFormat.R8_UNORM:
                return this.expandR8(data, width, height);
            case BntxFormat.R8G8_UNORM:
                return this.expandRg8(data, width, height);
            // Block-compressed formats - stub implementations
            case BntxFormat.BC1_UNORM:
            case BntxFormat.BC1_SRGB:
                return this.decodeBcn('BC1', width, height);
            case BntxFormat.BC2_UNORM:
            case BntxFormat.BC2_SRGB:
                return this.decodeBcn('BC2', width, height);
            case BntxFormat.BC3_UNORM:
            case BntxFormat.BC3_SRGB:
                return this.decodeBcn('BC3', width, height);
            case BntxFormat.BC4_UNORM:
                return this.decodeBcn('BC4', width, height);
            case BntxFormat.BC5_UNORM:
                return this.decodeBcn('BC5', width, height);
            case BntxFormat.BC7_UNORM:
            case BntxFormat.BC7_SRGB:
                return this.decodeBcn('BC7', width, height);
            // ASTC formats - stub implementations
            case BntxFormat.ASTC_4x4_UNORM:
            case BntxFormat.ASTC_4x4_SRGB:
                return this.decodeAstc(4, 4, width, height);
            case BntxFormat.ASTC_5x4_UNORM:
            case BntxFormat.ASTC_5x4_SRGB:
                return this.decodeAstc(5, 4, width, height);
            case BntxFormat.ASTC_5x5_UNORM:
            case BntxFormat.ASTC_5x5_SRGB:
                return this.decodeAstc(5, 5, width, height);
            case BntxFormat.ASTC_6x5_UNORM:
            case BntxFormat.ASTC_6x5_SRGB:
                return this.decodeAstc(6, 5, width, height);
            case BntxFormat.ASTC_6x6_UNORM:
            case BntxFormat.ASTC_6x6_SRGB:
                return this.decodeAstc(6, 6, width, height);
            case BntxFormat.ASTC_8x5_UNORM:
            case BntxFormat.ASTC_8x5_SRGB:
                return this.decodeAstc(8, 5, width, height);
            case BntxFormat.ASTC_8x6_UNORM:
            case BntxFormat.ASTC_8x6_SRGB:
                return this.decodeAstc(8, 6, width, height);
            case BntxFormat.ASTC_8x8_UNORM:
            case BntxFormat.ASTC_8x8_SRGB:
                return this.decodeAstc(8, 8, width, height);
            case BntxFormat.ASTC_10x5_UNORM:
            case BntxFormat.ASTC_10x5_SRGB:
                return this.decodeAstc(10, 5, width, height);
            case BntxFormat.ASTC_10x6_UNORM:
            case BntxFormat.ASTC_10x6_SRGB:
                return this.decodeAstc(10, 6, width, height);
            case BntxFormat.ASTC_10x8_UNORM:
            case BntxFormat.ASTC_10x8_SRGB:
                return this.decodeAstc(10, 8, width, height);
            case BntxFormat.ASTC_10x10_UNORM:
            case BntxFormat.ASTC_10x10_SRGB:
                return this.decodeAstc(10, 10, width, height);
            case BntxFormat.ASTC_12x10_UNORM:
            case BntxFormat.ASTC_12x10_SRGB:
                return this.decodeAstc(12, 10, width, height);
            case BntxFormat.ASTC_12x12_UNORM:
            case BntxFormat.ASTC_12x12_SRGB:
                return this.decodeAstc(12, 12, width, height);
            default:
                throw new Error(`Unsupported texture format: ${format}`);
        }
    }
    /**
     * Decode BCn compressed data.
     * Note: This is a stub. Real BCn decoding requires a proper implementation.
     */
    static decodeBcn(format, width, height) {
        // BCn decoding is complex - this is a placeholder
        // In a real implementation, you would decode the BCn blocks
        console.log(`[BntxDecoder] BC${format} decode stub - returning placeholder`);
        return this.createPlaceholder(width, height, 0, 255, 0, 255); // Green placeholder
    }
    /**
     * Decode ASTC compressed data.
     * Note: This is a stub. Real ASTC decoding requires a native library.
     */
    static decodeAstc(blockW, blockH, width, height) {
        // ASTC decode is very complex and typically requires native libraries
        console.error(`[BntxDecoder] ASTC ${blockW}x${blockH} decode not available, using placeholder`);
        return this.createPlaceholder(width, height, 255, 0, 255, 255); // Magenta placeholder
    }
    /**
     * Convert BGRA to RGBA.
     */
    static convertBgraToRgba(data) {
        const rgba = Buffer.alloc(data.length);
        for (let i = 0; i < data.length; i += 4) {
            rgba[i + 0] = data[i + 2]; // R <- B
            rgba[i + 1] = data[i + 1]; // G <- G
            rgba[i + 2] = data[i + 0]; // B <- R
            rgba[i + 3] = data[i + 3]; // A <- A
        }
        return rgba;
    }
    /**
     * Expand R8 to RGBA8.
     */
    static expandR8(data, width, height) {
        const rgba = Buffer.alloc(width * height * 4);
        const pixelCount = Math.min(data.length, width * height);
        for (let i = 0; i < pixelCount; i++) {
            rgba[i * 4 + 0] = data[i];
            rgba[i * 4 + 1] = data[i];
            rgba[i * 4 + 2] = data[i];
            rgba[i * 4 + 3] = 255;
        }
        return rgba;
    }
    /**
     * Expand RG8 to RGBA8.
     */
    static expandRg8(data, width, height) {
        const rgba = Buffer.alloc(width * height * 4);
        const pixels = Math.min(data.length / 2, width * height);
        for (let i = 0; i < pixels; i++) {
            rgba[i * 4 + 0] = data[i * 2 + 0]; // R
            rgba[i * 4 + 1] = data[i * 2 + 1]; // G
            rgba[i * 4 + 2] = 0; // B
            rgba[i * 4 + 3] = 255; // A
        }
        return rgba;
    }
    /**
     * Create a solid color placeholder image.
     */
    static createPlaceholder(width, height, r, g, b, a) {
        const rgba = Buffer.alloc(width * height * 4);
        for (let i = 0; i < rgba.length; i += 4) {
            rgba[i + 0] = r;
            rgba[i + 1] = g;
            rgba[i + 2] = b;
            rgba[i + 3] = a;
        }
        return rgba;
    }
    /**
     * Get format information.
     */
    static getFormatInfo(format) {
        switch (format) {
            case BntxFormat.R8_UNORM:
                return { bpp: 1, blkWidth: 1, blkHeight: 1 };
            case BntxFormat.R8G8_UNORM:
                return { bpp: 2, blkWidth: 1, blkHeight: 1 };
            case BntxFormat.R8G8B8A8_UNORM:
            case BntxFormat.R8G8B8A8_SRGB:
            case BntxFormat.B8G8R8A8_UNORM:
            case BntxFormat.B8G8R8A8_SRGB:
                return { bpp: 4, blkWidth: 1, blkHeight: 1 };
            case BntxFormat.BC1_UNORM:
            case BntxFormat.BC1_SRGB:
            case BntxFormat.BC4_UNORM:
            case BntxFormat.BC4_SNORM:
                return { bpp: 8, blkWidth: 4, blkHeight: 4 };
            case BntxFormat.BC2_UNORM:
            case BntxFormat.BC2_SRGB:
            case BntxFormat.BC3_UNORM:
            case BntxFormat.BC3_SRGB:
            case BntxFormat.BC5_UNORM:
            case BntxFormat.BC5_SNORM:
            case BntxFormat.BC7_UNORM:
            case BntxFormat.BC7_SRGB:
            case BntxFormat.ASTC_4x4_UNORM:
            case BntxFormat.ASTC_4x4_SRGB:
                return { bpp: 16, blkWidth: 4, blkHeight: 4 };
            case BntxFormat.ASTC_5x4_UNORM:
            case BntxFormat.ASTC_5x4_SRGB:
                return { bpp: 16, blkWidth: 5, blkHeight: 4 };
            case BntxFormat.ASTC_5x5_UNORM:
            case BntxFormat.ASTC_5x5_SRGB:
                return { bpp: 16, blkWidth: 5, blkHeight: 5 };
            case BntxFormat.ASTC_6x5_UNORM:
            case BntxFormat.ASTC_6x5_SRGB:
                return { bpp: 16, blkWidth: 6, blkHeight: 5 };
            case BntxFormat.ASTC_6x6_UNORM:
            case BntxFormat.ASTC_6x6_SRGB:
                return { bpp: 16, blkWidth: 6, blkHeight: 6 };
            case BntxFormat.ASTC_8x5_UNORM:
            case BntxFormat.ASTC_8x5_SRGB:
                return { bpp: 16, blkWidth: 8, blkHeight: 5 };
            case BntxFormat.ASTC_8x6_UNORM:
            case BntxFormat.ASTC_8x6_SRGB:
                return { bpp: 16, blkWidth: 8, blkHeight: 6 };
            case BntxFormat.ASTC_8x8_UNORM:
            case BntxFormat.ASTC_8x8_SRGB:
                return { bpp: 16, blkWidth: 8, blkHeight: 8 };
            case BntxFormat.ASTC_10x5_UNORM:
            case BntxFormat.ASTC_10x5_SRGB:
                return { bpp: 16, blkWidth: 10, blkHeight: 5 };
            case BntxFormat.ASTC_10x6_UNORM:
            case BntxFormat.ASTC_10x6_SRGB:
                return { bpp: 16, blkWidth: 10, blkHeight: 6 };
            case BntxFormat.ASTC_10x8_UNORM:
            case BntxFormat.ASTC_10x8_SRGB:
                return { bpp: 16, blkWidth: 10, blkHeight: 8 };
            case BntxFormat.ASTC_10x10_UNORM:
            case BntxFormat.ASTC_10x10_SRGB:
                return { bpp: 16, blkWidth: 10, blkHeight: 10 };
            case BntxFormat.ASTC_12x10_UNORM:
            case BntxFormat.ASTC_12x10_SRGB:
                return { bpp: 16, blkWidth: 12, blkHeight: 10 };
            case BntxFormat.ASTC_12x12_UNORM:
            case BntxFormat.ASTC_12x12_SRGB:
                return { bpp: 16, blkWidth: 12, blkHeight: 12 };
            default:
                return { bpp: 4, blkWidth: 1, blkHeight: 1 }; // Fallback: assume RGBA8
        }
    }
    /**
     * Convert raw format value to BntxFormat enum.
     */
    static convertFormat(surfaceFormat) {
        // Surface format values from Switch SDK
        // These are approximate mappings
        const formatMap = {
            0x0101: BntxFormat.R8_UNORM,
            0x0201: BntxFormat.R8G8_UNORM,
            0x0401: BntxFormat.R8G8B8A8_UNORM,
            0x0402: BntxFormat.R8G8B8A8_SRGB,
            0x040b: BntxFormat.B8G8R8A8_UNORM,
            0x040c: BntxFormat.B8G8R8A8_SRGB,
            0x1a01: BntxFormat.BC1_UNORM,
            0x1a02: BntxFormat.BC1_SRGB,
            0x1b01: BntxFormat.BC2_UNORM,
            0x1b02: BntxFormat.BC2_SRGB,
            0x1c01: BntxFormat.BC3_UNORM,
            0x1c02: BntxFormat.BC3_SRGB,
            0x1d01: BntxFormat.BC4_UNORM,
            0x1d02: BntxFormat.BC4_SNORM,
            0x1e01: BntxFormat.BC5_UNORM,
            0x1e02: BntxFormat.BC5_SNORM,
            0x1f01: BntxFormat.BC7_UNORM,
            0x1f02: BntxFormat.BC7_SRGB,
            0x6001: BntxFormat.ASTC_4x4_UNORM,
            0x6002: BntxFormat.ASTC_4x4_SRGB,
            0x6201: BntxFormat.ASTC_5x4_UNORM,
            0x6202: BntxFormat.ASTC_5x4_SRGB,
            0x6401: BntxFormat.ASTC_5x5_UNORM,
            0x6402: BntxFormat.ASTC_5x5_SRGB,
            0x6601: BntxFormat.ASTC_6x5_UNORM,
            0x6602: BntxFormat.ASTC_6x5_SRGB,
            0x6801: BntxFormat.ASTC_6x6_UNORM,
            0x6802: BntxFormat.ASTC_6x6_SRGB,
            0x6a01: BntxFormat.ASTC_8x5_UNORM,
            0x6a02: BntxFormat.ASTC_8x5_SRGB,
            0x6c01: BntxFormat.ASTC_8x6_UNORM,
            0x6c02: BntxFormat.ASTC_8x6_SRGB,
            0x6e01: BntxFormat.ASTC_8x8_UNORM,
            0x6e02: BntxFormat.ASTC_8x8_SRGB,
            0x7001: BntxFormat.ASTC_10x5_UNORM,
            0x7002: BntxFormat.ASTC_10x5_SRGB,
            0x7201: BntxFormat.ASTC_10x6_UNORM,
            0x7202: BntxFormat.ASTC_10x6_SRGB,
            0x7401: BntxFormat.ASTC_10x8_UNORM,
            0x7402: BntxFormat.ASTC_10x8_SRGB,
            0x7601: BntxFormat.ASTC_10x10_UNORM,
            0x7602: BntxFormat.ASTC_10x10_SRGB,
            0x7801: BntxFormat.ASTC_12x10_UNORM,
            0x7802: BntxFormat.ASTC_12x10_SRGB,
            0x7a01: BntxFormat.ASTC_12x12_UNORM,
            0x7a02: BntxFormat.ASTC_12x12_SRGB,
        };
        return formatMap[surfaceFormat] ?? BntxFormat.Unknown;
    }
}
export default BntxDecoder;
//# sourceMappingURL=BntxDecoder.js.map