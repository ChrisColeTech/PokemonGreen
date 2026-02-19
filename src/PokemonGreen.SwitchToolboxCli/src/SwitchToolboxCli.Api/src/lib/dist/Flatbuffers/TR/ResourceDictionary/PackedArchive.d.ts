export declare class PackedFile {
    Field_00: number;
    EncryptionType: number;
    Level: number;
    FileSize: bigint;
    FileBuffer: Buffer;
}
export declare class PackedArchive {
    FileHashes: bigint[];
    FileEntry: PackedFile[];
}
//# sourceMappingURL=PackedArchive.d.ts.map