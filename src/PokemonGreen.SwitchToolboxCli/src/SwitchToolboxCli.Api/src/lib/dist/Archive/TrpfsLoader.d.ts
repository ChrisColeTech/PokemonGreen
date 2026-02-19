/**
 * TRPFS/TRPFD Archive Loader
 * Ported from C# TrpfsLoader.cs
 *
 * Loads files from a TRPFD/TRPFS archive pair (Pokémon Scarlet/Violet, Legends Arceus).
 * data.trpfd = FlatBuffer file descriptor (hashes → pack names/indices).
 * data.trpfs = ONEFILE header + FlatBuffer FileSystem (hashes → offsets) + packed data.
 */
import { TrpakHashCache } from './TrpakTypes.js';
export declare class TrpfsLoader {
    private _fd;
    private _fs;
    private _trpfsPath;
    private _hashCache;
    private _packCache;
    constructor(arcDirectory: string, hashCache?: TrpakHashCache);
    /** Number of files in the descriptor. */
    get FileCount(): number;
    /** All pack names registered in the descriptor. */
    get PackNames(): readonly string[];
    /**
     * Extract a file by its romfs-relative path (e.g. "pokemon/pokemon_model/pm0025/pm0025_00/mdl/pm0025_00.trmdl").
     */
    ExtractFile(romfsRelativePath: string): Buffer | null;
    /**
     * Extract a file by its FNV hash.
     */
    ExtractFileByHash(fileHash: bigint): Buffer | null;
    /**
     * Find all file hashes whose human-readable names match a predicate.
     * Requires a populated hash cache.
     */
    FindFiles(namePredicate: (name: string) => boolean): IterableIterator<[bigint, string]>;
    /**
     * Find all files with a given extension (e.g. ".trmdl").
     */
    FindFilesByExtension(extension: string): IterableIterator<[bigint, string]>;
    private NormalizePath;
    private ReadFileSystem;
    private TryResolvePackInfo;
    private TryGetPack;
    private FindEntryIndex;
}
//# sourceMappingURL=TrpfsLoader.d.ts.map