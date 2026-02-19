/**
 * Nintendo Tegra X1 swizzle/unswizzle implementation for Switch textures.
 *
 * Handles Tegra X1 GPU block-linear and pitch-linear deswizzling.
 * Ported from C# TegraSwizzle.cs
 */
export declare class TegraSwizzle {
    /**
     * Deswizzle a Tegra X1 block-linear or pitch-linear texture surface.
     * This is the primary entry point.
     */
    static deswizzle(width: number, height: number, depth: number, blkWidth: number, blkHeight: number, blkDepth: number, roundPitch: number, bpp: number, tileMode: number, blockHeightLog2: number, data: Buffer): Buffer;
    /**
     * Deswizzle a block-linear texture surface.
     */
    private static deswizzleBlockLinearSurface;
    /**
     * Pure JavaScript implementation of block-linear deswizzling.
     * This mimics the behavior of the native tegra_swizzle library.
     */
    private static deswizzleBlockLinear;
    /**
     * Calculate the swizzled offset for a given position.
     * Uses Morton order for Tegra X1 swizzling.
     */
    private static getSwizzledOffset;
    /**
     * Interleave bits of x and y to create Morton order index.
     */
    private static interleaveBits;
    /**
     * Deswizzle a pitch-linear texture surface.
     */
    private static deswizzlePitchLinear;
    /**
     * Get the block height for mip level 0.
     */
    static getBlockHeight(heightInBytes: number): bigint;
    /**
     * Get the block height for a specific mip level.
     */
    static getMipBlockHeight(mipHeightInBytes: number, blockHeightMip0: bigint): bigint;
    /**
     * Calculate block height for mip level 0.
     */
    private static blockHeightMip0;
    /**
     * Calculate block height for a specific mip level.
     */
    private static mipBlockHeight;
    /**
     * Divide and round up.
     */
    static divRoundUp(n: number, d: number): number;
    /**
     * Round up to the next power of 2.
     */
    static pow2RoundUp(x: number): number;
    /**
     * Round x up to the next multiple of y.
     */
    private static roundUp;
    /**
     * Calculate swizzled surface size.
     */
    static swizzledSurfaceSize(width: number, height: number, depth: number, blockDim: {
        width: bigint;
        height: bigint;
        depth: bigint;
    }, bytesPerPixel: bigint, mipmapCount: bigint, arrayCount: bigint): bigint;
}
export default TegraSwizzle;
