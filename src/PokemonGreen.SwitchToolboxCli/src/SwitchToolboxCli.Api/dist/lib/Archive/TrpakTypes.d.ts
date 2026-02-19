/**
 * TRPAK Archive Type Definitions
 * Converted from C# TrpakTypes.cs
 */
export declare enum TrpakCompressionType {
    None = 0,
    Zlib = 1,
    Lz4 = 2,
    Oodle = 3
}
export interface TrpakHeader {
    magic: bigint;
    Version: number;
    Relocated: number;
    FileNumber: number;
    FolderNumber: number;
}
export declare namespace TrpakHeader {
    const Magic = 5423250189618792007n;
    const Size = 24;
}
export interface TrpakFolderHeader {
    Hash: bigint;
    ContentNumber: number;
    Reserved: number;
}
export declare namespace TrpakFolderHeader {
    const Size = 16;
}
export interface TrpakFolderIndex {
    Hash: bigint;
    Index: number;
    Reserved: number;
}
export declare namespace TrpakFolderIndex {
    const Size = 16;
}
export interface TrpakFileHeader {
    Level: number;
    CompressionType: TrpakCompressionType;
    BufferSize: number;
    FileSize: number;
    Reserved: number;
    FilePointer: bigint;
}
export declare namespace TrpakFileHeader {
    const Size = 24;
}
/**
 * FNV-1a 64-bit hash used by TRPAK for file/folder name lookup.
 * Ported from gftool GFFNV.cs.
 */
export declare class FnvHash {
    private static readonly FnvPrime;
    private static readonly FnvBasis;
    static Hash(str: string): bigint;
}
/**
 * Maps FNV hashes back to human-readable file paths.
 * Ported from gftool GFPakHashCache.cs.
 */
export declare class TrpakHashCache {
    private _cache;
    get count(): number;
    /**
     * Load a binary hash cache file (GFPAKHashCache.bin format).
     */
    LoadBinaryCache(path: string): void;
    /**
     * Load a text hash list (one "hash path" per line).
     */
    LoadHashList(lines: string[]): void;
    Add(hash: bigint, name: string): void;
    GetName(hash: bigint): string | null;
    private TryParseHex;
}
export declare class TrpakArchive {
    Folders: TrpakFolder[];
    /**
     * Find a file by its full path (case-insensitive).
     */
    FindFile(fullPath: string): TrpakFile | null;
    /**
     * Find all files matching a predicate.
     */
    FindFiles(predicate: (file: TrpakFile) => boolean): TrpakFile[];
    /**
     * Find all files with a specific extension.
     */
    FindFilesByExtension(extension: string): TrpakFile[];
}
export declare class TrpakFolder {
    Path: string;
    Files: TrpakFile[];
}
export declare class TrpakFile {
    Name: string;
    FullName: string;
    Data: Buffer;
}
