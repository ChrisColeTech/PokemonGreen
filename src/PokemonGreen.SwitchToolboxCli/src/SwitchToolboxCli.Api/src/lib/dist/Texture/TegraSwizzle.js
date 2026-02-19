/**
 * Nintendo Tegra X1 swizzle/unswizzle implementation for Switch textures.
 *
 * Handles Tegra X1 GPU block-linear and pitch-linear deswizzling.
 * Ported from C# TegraSwizzle.cs
 */
export class TegraSwizzle {
    /**
     * Deswizzle a Tegra X1 block-linear or pitch-linear texture surface.
     * This is the primary entry point.
     */
    static deswizzle(width, height, depth, blkWidth, blkHeight, blkDepth, roundPitch, bpp, tileMode, blockHeightLog2, data) {
        if (tileMode === 1) {
            return this.deswizzlePitchLinear(width, height, depth, blkWidth, blkHeight, blkDepth, roundPitch, bpp, data);
        }
        else {
            return this.deswizzleBlockLinearSurface(width, height, depth, blkWidth, blkHeight, blkDepth, bpp, blockHeightLog2, data);
        }
    }
    /**
     * Deswizzle a block-linear texture surface.
     */
    static deswizzleBlockLinearSurface(width, height, depth, blkWidth, blkHeight, blkDepth, bpp, blockHeightLog2, data) {
        // Tegra only allows block heights supported by the TRM (1, 2, 4, 8, 16, 32)
        const blockHeightMip0 = 1 << Math.max(Math.min(blockHeightLog2, 5), 0);
        // Convert to block dimensions for block compressed formats
        const w = this.divRoundUp(width, blkWidth);
        const h = this.divRoundUp(height, blkHeight);
        const d = this.divRoundUp(depth, blkDepth);
        const output = Buffer.alloc(w * h * d * bpp);
        this.deswizzleBlockLinear(w, h, d, data, output, BigInt(blockHeightMip0), bpp);
        return output;
    }
    /**
     * Pure JavaScript implementation of block-linear deswizzling.
     * This mimics the behavior of the native tegra_swizzle library.
     */
    static deswizzleBlockLinear(width, height, depth, source, destination, blockHeight, bytesPerPixel) {
        const blockHeightNum = Number(blockHeight);
        const gobWidth = 64; // GOB width in bytes
        const gobHeight = 8 * blockHeightNum; // GOB height depends on block height
        for (let z = 0; z < depth; z++) {
            for (let y = 0; y < height; y++) {
                for (let x = 0; x < width; x++) {
                    // Calculate the destination position
                    const dstPos = (z * height * width + y * width + x) * bytesPerPixel;
                    // Calculate the swizzled source position using Morton order
                    const srcPos = this.getSwizzledOffset(x, y, gobWidth, gobHeight, bytesPerPixel);
                    if (srcPos + bytesPerPixel <= source.length && dstPos + bytesPerPixel <= destination.length) {
                        source.copy(destination, dstPos, srcPos, srcPos + bytesPerPixel);
                    }
                }
            }
        }
    }
    /**
     * Calculate the swizzled offset for a given position.
     * Uses Morton order for Tegra X1 swizzling.
     */
    static getSwizzledOffset(x, y, gobWidth, gobHeight, bytesPerPixel) {
        // Calculate which GOB this pixel belongs to
        const gobX = Math.floor((x * bytesPerPixel) / gobWidth);
        const gobY = Math.floor(y / gobHeight);
        // Position within the GOB
        const xInGob = (x * bytesPerPixel) % gobWidth;
        const yInGob = y % gobHeight;
        // Calculate GOB index using Morton order
        const gobIndex = this.interleaveBits(gobX, gobY);
        // Calculate offset within GOB using Morton order
        const inGobOffset = this.interleaveBits(xInGob / 4, yInGob) * 4 + (xInGob % 4);
        return gobIndex * gobWidth * gobHeight + inGobOffset;
    }
    /**
     * Interleave bits of x and y to create Morton order index.
     */
    static interleaveBits(x, y) {
        let result = 0;
        for (let i = 0; i < 16; i++) {
            result |= ((x >> i) & 1) << (2 * i);
            result |= ((y >> i) & 1) << (2 * i + 1);
        }
        return result;
    }
    /**
     * Deswizzle a pitch-linear texture surface.
     */
    static deswizzlePitchLinear(width, height, depth, blkWidth, blkHeight, blkDepth, roundPitch, bpp, data) {
        const w = this.divRoundUp(width, blkWidth);
        const h = this.divRoundUp(height, blkHeight);
        const d = this.divRoundUp(depth, blkDepth);
        let pitch = w * bpp;
        if (roundPitch === 1) {
            pitch = this.roundUp(pitch, 32);
        }
        const surfSize = pitch * h;
        const result = Buffer.alloc(surfSize);
        for (let z = 0; z < d; z++) {
            for (let y = 0; y < h; y++) {
                for (let x = 0; x < w; x++) {
                    const pos = y * pitch + x * bpp;
                    const pos_ = (y * w + x) * bpp;
                    if (pos + bpp <= surfSize) {
                        data.copy(result, pos, pos_, pos_ + bpp);
                    }
                }
            }
        }
        return result;
    }
    /**
     * Get the block height for mip level 0.
     */
    static getBlockHeight(heightInBytes) {
        return this.blockHeightMip0(BigInt(heightInBytes));
    }
    /**
     * Get the block height for a specific mip level.
     */
    static getMipBlockHeight(mipHeightInBytes, blockHeightMip0) {
        return this.mipBlockHeight(BigInt(mipHeightInBytes), blockHeightMip0);
    }
    /**
     * Calculate block height for mip level 0.
     */
    static blockHeightMip0(heightInBytes) {
        const height = Number(heightInBytes);
        // Block height is determined by texture height
        if (height <= 16)
            return BigInt(1);
        if (height <= 32)
            return BigInt(2);
        if (height <= 64)
            return BigInt(4);
        if (height <= 128)
            return BigInt(8);
        if (height <= 256)
            return BigInt(16);
        return BigInt(32);
    }
    /**
     * Calculate block height for a specific mip level.
     */
    static mipBlockHeight(mipHeightInBytes, blockHeightMip0) {
        const height = Number(mipHeightInBytes);
        const mip0 = Number(blockHeightMip0);
        // Calculate appropriate block height based on mip level size
        if (height <= 16)
            return BigInt(1);
        if (height <= 32 && mip0 >= 2)
            return BigInt(2);
        if (height <= 64 && mip0 >= 4)
            return BigInt(4);
        if (height <= 128 && mip0 >= 8)
            return BigInt(8);
        if (height <= 256 && mip0 >= 16)
            return BigInt(16);
        // Clamp to the mip0 block height
        const maxBlockHeight = BigInt(32);
        return blockHeightMip0 < maxBlockHeight ? blockHeightMip0 : maxBlockHeight;
    }
    /**
     * Divide and round up.
     */
    static divRoundUp(n, d) {
        return Math.floor((n + d - 1) / d);
    }
    /**
     * Round up to the next power of 2.
     */
    static pow2RoundUp(x) {
        x -= 1;
        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;
        return x + 1;
    }
    /**
     * Round x up to the next multiple of y.
     */
    static roundUp(x, y) {
        return ((x - 1) | (y - 1)) + 1;
    }
    /**
     * Calculate swizzled surface size.
     */
    static swizzledSurfaceSize(width, height, depth, blockDim, bytesPerPixel, mipmapCount, arrayCount) {
        // Simplified calculation - full implementation would need more detail
        const w = BigInt(Math.ceil(width / Number(blockDim.width)));
        const h = BigInt(Math.ceil(height / Number(blockDim.height)));
        const d = BigInt(Math.ceil(depth / Number(blockDim.depth)));
        const surfaceSize = w * h * d * bytesPerPixel * mipmapCount * arrayCount;
        return surfaceSize;
    }
}
export default TegraSwizzle;
//# sourceMappingURL=TegraSwizzle.js.map