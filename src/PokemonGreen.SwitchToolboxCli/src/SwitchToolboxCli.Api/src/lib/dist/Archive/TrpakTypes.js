/**
 * TRPAK Archive Type Definitions
 * Converted from C# TrpakTypes.cs
 */
import * as fs from 'fs';
// ============================================================================
// Binary Structs
// ============================================================================
export var TrpakCompressionType;
(function (TrpakCompressionType) {
    TrpakCompressionType[TrpakCompressionType["None"] = 0] = "None";
    TrpakCompressionType[TrpakCompressionType["Zlib"] = 1] = "Zlib";
    TrpakCompressionType[TrpakCompressionType["Lz4"] = 2] = "Lz4";
    TrpakCompressionType[TrpakCompressionType["Oodle"] = 3] = "Oodle";
})(TrpakCompressionType || (TrpakCompressionType = {}));
export var TrpakHeader;
(function (TrpakHeader) {
    TrpakHeader.Magic = 0x4b434150584c4647n; // "GFLXPACK"
    TrpakHeader.Size = 0x18;
})(TrpakHeader || (TrpakHeader = {}));
export var TrpakFolderHeader;
(function (TrpakFolderHeader) {
    TrpakFolderHeader.Size = 0x10;
})(TrpakFolderHeader || (TrpakFolderHeader = {}));
export var TrpakFolderIndex;
(function (TrpakFolderIndex) {
    TrpakFolderIndex.Size = 0x10;
})(TrpakFolderIndex || (TrpakFolderIndex = {}));
export var TrpakFileHeader;
(function (TrpakFileHeader) {
    TrpakFileHeader.Size = 0x18;
})(TrpakFileHeader || (TrpakFileHeader = {}));
// ============================================================================
// FNV Hash
// ============================================================================
/**
 * FNV-1a 64-bit hash used by TRPAK for file/folder name lookup.
 * Ported from gftool GFFNV.cs.
 */
export class FnvHash {
    static FnvPrime = 0x00000100000001b3n;
    static FnvBasis = 0xcbf29ce484222645n;
    static Hash(str) {
        const buf = Buffer.from(str, 'utf8');
        let result = this.FnvBasis;
        for (const b of buf) {
            result ^= BigInt(b);
            result *= this.FnvPrime;
        }
        return result;
    }
}
// ============================================================================
// Hash Cache
// ============================================================================
/**
 * Maps FNV hashes back to human-readable file paths.
 * Ported from gftool GFPakHashCache.cs.
 */
export class TrpakHashCache {
    _cache = new Map();
    get count() {
        return this._cache.size;
    }
    /**
     * Load a binary hash cache file (GFPAKHashCache.bin format).
     */
    LoadBinaryCache(path) {
        if (!fs.existsSync(path))
            return;
        const data = fs.readFileSync(path);
        let offset = 0;
        const count = data.readBigUInt64LE(offset);
        offset += 8;
        for (let i = 0n; i < count; i++) {
            const hash = data.readBigUInt64LE(offset);
            offset += 8;
            // Read .NET BinaryReader string format (7-bit encoded length prefix)
            let length = 0;
            let shift = 0;
            while (true) {
                const b = data[offset++];
                length |= (b & 0x7F) << shift;
                if ((b & 0x80) === 0)
                    break;
                shift += 7;
            }
            const name = data.toString('utf8', offset, offset + length);
            offset += length;
            this._cache.set(hash, name);
        }
    }
    /**
     * Load a text hash list (one "hash path" per line).
     */
    LoadHashList(lines) {
        for (const line of lines) {
            if (!line || line.trim().length === 0)
                continue;
            const parts = line.trim().split(/\s+/).filter(p => p.length > 0);
            if (parts.length < 2)
                continue;
            const parseResult = this.TryParseHex(parts[0]);
            if (parseResult.success) {
                this._cache.set(parseResult.value, parts[1]);
            }
            else {
                this._cache.set(FnvHash.Hash(parts[1]), parts[1]);
            }
        }
    }
    Add(hash, name) {
        this._cache.set(hash, name);
    }
    GetName(hash) {
        return this._cache.get(hash) ?? null;
    }
    TryParseHex(text) {
        if (!text || text.trim().length === 0) {
            return { success: false, value: 0n };
        }
        let trimmed = text.trim();
        if (trimmed.toLowerCase().startsWith('0x')) {
            trimmed = trimmed.slice(2);
        }
        try {
            const value = BigInt('0x' + trimmed);
            return { success: true, value };
        }
        catch {
            return { success: false, value: 0n };
        }
    }
}
// ============================================================================
// Output Models
// ============================================================================
export class TrpakArchive {
    Folders = [];
    /**
     * Find a file by its full path (case-insensitive).
     */
    FindFile(fullPath) {
        for (const folder of this.Folders) {
            for (const file of folder.Files) {
                if (file.FullName.toLowerCase() === fullPath.toLowerCase()) {
                    return file;
                }
            }
        }
        return null;
    }
    /**
     * Find all files matching a predicate.
     */
    FindFiles(predicate) {
        const results = [];
        for (const folder of this.Folders) {
            for (const file of folder.Files) {
                if (predicate(file)) {
                    results.push(file);
                }
            }
        }
        return results;
    }
    /**
     * Find all files with a specific extension.
     */
    FindFilesByExtension(extension) {
        return this.FindFiles(f => f.FullName.toLowerCase().endsWith(extension.toLowerCase()));
    }
}
export class TrpakFolder {
    Path = '';
    Files = [];
}
export class TrpakFile {
    Name = '';
    FullName = '';
    Data = Buffer.alloc(0);
}
//# sourceMappingURL=TrpakTypes.js.map