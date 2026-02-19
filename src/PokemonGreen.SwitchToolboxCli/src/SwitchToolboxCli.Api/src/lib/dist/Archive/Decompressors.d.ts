/**
 * Decompression algorithms for TRPAK archives.
 * Ported from C# Decompressors.cs
 */
/**
 * Oodle decompression via native library.
 * Stub implementation - real Oodle requires native library.
 */
export declare class OodleDecompressor {
    static Decompress(input: Buffer, decompressedLength: number): Buffer | null;
}
/**
 * LZ4 decompression.
 * Ported from gftool LZ4.cs.
 */
export declare class Lz4Decompressor {
    static Decompress(compressed: Buffer, decompressedLength: number): Buffer;
}
//# sourceMappingURL=Decompressors.d.ts.map