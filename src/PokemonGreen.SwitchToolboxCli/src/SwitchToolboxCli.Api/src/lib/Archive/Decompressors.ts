/**
 * Decompression algorithms for TRPAK archives.
 * Ported from C# Decompressors.cs
 */

/**
 * Oodle decompression via native library.
 * Stub implementation - real Oodle requires native library.
 */
export class OodleDecompressor {
    static Decompress(input: Buffer, decompressedLength: number): Buffer | null {
        // TODO: Real Oodle decompression requires the oo2core_8_win64.dll native library
        // This is a stub that returns null
        console.warn('Oodle decompression requires native library - not implemented');
        return null;
    }
}

/**
 * LZ4 decompression.
 * Ported from gftool LZ4.cs.
 */
export class Lz4Decompressor {
    static Decompress(compressed: Buffer, decompressedLength: number): Buffer {
        const dec = Buffer.alloc(decompressedLength);
        let cmpPos = 0;
        let decPos = 0;

        const getLength = (length: number): number => {
            if (length === 0xF) {
                let sum: number;
                do {
                    length += (sum = compressed[cmpPos++]);
                } while (sum === 0xFF);
            }
            return length;
        };

        do {
            const token = compressed[cmpPos++];
            let encCount = token & 0xF;
            const litCount = getLength((token >> 4) & 0xF);

            // Copy literal data
            dec.set(compressed.subarray(cmpPos, cmpPos + litCount), decPos);
            cmpPos += litCount;
            decPos += litCount;

            if (cmpPos >= compressed.length) break;

            // Read back-reference offset
            const back = compressed[cmpPos++] | (compressed[cmpPos++] << 8);
            encCount = getLength(encCount) + 4;
            const encPos = decPos - back;

            if (encCount <= back) {
                // Fast path: no overlap, use buffer copy
                dec.set(dec.subarray(encPos, encPos + encCount), decPos);
                decPos += encCount;
            } else {
                // Slow path: overlapping copy, byte-by-byte
                for (let i = 0; i < encCount; i++) {
                    dec[decPos++] = dec[encPos + i];
                }
            }
        } while (cmpPos < compressed.length && decPos < dec.length);

        return dec;
    }
}
