/**
 * TRPFS/TRPFD Archive Loader
 * Ported from C# TrpfsLoader.cs
 *
 * Loads files from a TRPFD/TRPFS archive pair (Pokémon Scarlet/Violet, Legends Arceus).
 * data.trpfd = FlatBuffer file descriptor (hashes → pack names/indices).
 * data.trpfs = ONEFILE header + FlatBuffer FileSystem (hashes → offsets) + packed data.
 */
import * as fs from 'fs';
import * as path from 'path';
import { FlatBufferConverter } from '../Utils/FlatBufferConverter.js';
import { TrpakHashCache, FnvHash } from './TrpakTypes.js';
import { OodleDecompressor } from './Decompressors.js';
export class TrpfsLoader {
    _fd;
    _fs;
    _trpfsPath;
    _hashCache;
    // Pack cache: packHash → deserialized PackedArchive
    _packCache = new Map();
    constructor(arcDirectory, hashCache) {
        const trpfdPath = path.join(arcDirectory, 'data.trpfd');
        this._trpfsPath = path.join(arcDirectory, 'data.trpfs');
        if (!fs.existsSync(trpfdPath)) {
            throw new Error(`data.trpfd not found: ${trpfdPath}`);
        }
        if (!fs.existsSync(this._trpfsPath)) {
            throw new Error(`data.trpfs not found: ${this._trpfsPath}`);
        }
        this._fd = FlatBufferConverter.DeserializeFrom(trpfdPath);
        this._fs = this.ReadFileSystem(this._trpfsPath);
        this._hashCache = hashCache ?? new TrpakHashCache();
    }
    /** Number of files in the descriptor. */
    get FileCount() {
        return this._fd.FileHashes?.length ?? 0;
    }
    /** All pack names registered in the descriptor. */
    get PackNames() {
        return this._fd.PackNames;
    }
    /**
     * Extract a file by its romfs-relative path (e.g. "pokemon/pokemon_model/pm0025/pm0025_00/mdl/pm0025_00.trmdl").
     */
    ExtractFile(romfsRelativePath) {
        const normalized = this.NormalizePath(romfsRelativePath);
        const hash = FnvHash.Hash(normalized);
        return this.ExtractFileByHash(hash);
    }
    /**
     * Extract a file by its FNV hash.
     */
    ExtractFileByHash(fileHash) {
        const packInfo = this.TryResolvePackInfo(fileHash);
        if (!packInfo)
            return null;
        const { packName, packSize } = packInfo;
        const packHash = FnvHash.Hash(packName);
        const pack = this.TryGetPack(packHash, packSize);
        if (!pack)
            return null;
        const entryIndex = this.FindEntryIndex(pack, fileHash);
        if (entryIndex < 0)
            return null;
        const entry = pack.FileEntry[entryIndex];
        let buffer = entry.FileBuffer;
        // EncryptionType != -1 means Oodle-compressed
        if (entry.EncryptionType !== -1) {
            const decompressed = OodleDecompressor.Decompress(buffer, Number(entry.FileSize));
            if (decompressed === null) {
                throw new Error(`Oodle decompression failed for hash 0x${fileHash.toString(16).padStart(16, '0')}`);
            }
            buffer = decompressed;
        }
        return buffer;
    }
    /**
     * Find all file hashes whose human-readable names match a predicate.
     * Requires a populated hash cache.
     */
    *FindFiles(namePredicate) {
        if (!this._fd.FileHashes)
            return;
        for (const hash of this._fd.FileHashes) {
            const name = this._hashCache.GetName(hash);
            if (name !== null && namePredicate(name)) {
                yield [hash, name];
            }
        }
    }
    /**
     * Find all files with a given extension (e.g. ".trmdl").
     */
    *FindFilesByExtension(extension) {
        const ext = extension.toLowerCase();
        yield* this.FindFiles(name => name.toLowerCase().endsWith(ext));
    }
    NormalizePath(inputPath) {
        let p = (inputPath ?? '').replace(/\\/g, '/').trim();
        if (p.toLowerCase().startsWith('romfs://')) {
            p = p.slice('romfs://'.length);
        }
        if (p.toLowerCase().startsWith('trpfs://')) {
            p = p.slice('trpfs://'.length);
        }
        return p.replace(/^\/+/, '');
    }
    ReadFileSystem(trpfsPath) {
        const data = fs.readFileSync(trpfsPath);
        // OneFileHeader: 8 bytes magic + 8 bytes offset
        // Skip magic (8 bytes)
        let offset = 8;
        const fsOffset = Number(data.readBigInt64LE(offset));
        offset += 8;
        const fsData = data.subarray(fsOffset);
        return FlatBufferConverter.DeserializeFrom(fsData);
    }
    TryResolvePackInfo(fileHash) {
        if (!this._fd.FileHashes || !this._fd.FileInfo || !this._fd.PackNames || !this._fd.PackInfo) {
            return null;
        }
        let idx = this._fd.FileHashes.indexOf(fileHash);
        if (idx >= 0) {
            const packIndex = this._fd.FileInfo[idx].PackIndex;
            if (packIndex < BigInt(this._fd.PackNames.length) && packIndex < BigInt(this._fd.PackInfo.length)) {
                const packName = this._fd.PackNames[Number(packIndex)];
                const packSize = this._fd.PackInfo[Number(packIndex)].FileSize;
                if (packName && packName.trim().length > 0) {
                    return { packName, packSize };
                }
            }
            return null;
        }
        // Check unused files
        if (this._fd.UnusedHashes && this._fd.UnusedFileInfo) {
            idx = this._fd.UnusedHashes.indexOf(fileHash);
            if (idx >= 0 && idx < this._fd.UnusedFileInfo.length) {
                const packIndex = this._fd.UnusedFileInfo[idx].PackIndex;
                if (packIndex < BigInt(this._fd.PackNames.length) && packIndex < BigInt(this._fd.PackInfo.length)) {
                    const packName = this._fd.PackNames[Number(packIndex)];
                    const packSize = this._fd.PackInfo[Number(packIndex)].FileSize;
                    if (packName && packName.trim().length > 0) {
                        return { packName, packSize };
                    }
                }
            }
        }
        return null;
    }
    TryGetPack(packHash, packSize) {
        if (this._packCache.has(packHash)) {
            return this._packCache.get(packHash);
        }
        const fileIndex = this._fs.FileHashes.indexOf(packHash);
        if (fileIndex < 0) {
            return null;
        }
        // Read raw pack bytes from TRPFS
        const data = fs.readFileSync(this._trpfsPath);
        const packOffset = Number(this._fs.FileOffsets[fileIndex]);
        const packBytes = data.subarray(packOffset, packOffset + Number(packSize));
        const pack = FlatBufferConverter.DeserializeFrom(packBytes);
        this._packCache.set(packHash, pack);
        return pack;
    }
    FindEntryIndex(pack, fileHash) {
        if (!pack.FileHashes)
            return -1;
        return pack.FileHashes.indexOf(fileHash);
    }
}
//# sourceMappingURL=TrpfsLoader.js.map