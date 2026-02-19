/**
 * TRPAK Archive Loader
 * Ported from C# TrpakLoader.cs
 *
 * Usage:
 *   const cache = new TrpakHashCache();
 *   cache.LoadHashList(fs.readFileSync('hashlist.txt', 'utf8').split('\n'));
 *   const archive = TrpakLoader.Load('model.trpak', cache);
 *   const trmdl = archive.FindFilesByExtension('.trmdl')[0];
 */
import { TrpakArchive, TrpakHashCache } from './TrpakTypes.js';
export declare class TrpakLoader {
    static Load(path: string, hashCache?: TrpakHashCache): TrpakArchive;
    static LoadFromBuffer(data: Buffer, hashCache?: TrpakHashCache): TrpakArchive;
    private static ReadStructTrpakHeader;
    private static ReadStructTrpakFolderHeader;
    private static ReadStructTrpakFolderIndex;
    private static ReadStructTrpakFileHeader;
}
//# sourceMappingURL=TrpakLoader.d.ts.map