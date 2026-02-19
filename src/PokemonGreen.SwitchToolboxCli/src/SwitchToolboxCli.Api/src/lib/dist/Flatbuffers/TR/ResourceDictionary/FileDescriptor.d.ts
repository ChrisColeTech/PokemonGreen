export declare class FileInfo {
    PackIndex: bigint;
    UnusedTable: number;
}
export declare class PackInfo {
    FileSize: bigint;
    FileCount: bigint;
}
export declare class FileDescriptor {
    FileHashes: bigint[];
    PackNames: string[];
    FileInfo: FileInfo[];
    PackInfo: PackInfo[];
}
export declare class CustomFileDescriptor {
    FileHashes: bigint[];
    PackNames: string[];
    FileInfo: FileInfo[];
    PackInfo: PackInfo[];
    UnusedHashes: bigint[];
    UnusedFileInfo: FileInfo[];
}
//# sourceMappingURL=FileDescriptor.d.ts.map