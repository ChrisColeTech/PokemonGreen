export class FileInfo {
    PackIndex = 0n;
    UnusedTable = 0;
}
export class PackInfo {
    FileSize = 0n;
    FileCount = 0n;
}
export class FileDescriptor {
    FileHashes = [];
    PackNames = [];
    FileInfo = [];
    PackInfo = [];
}
export class CustomFileDescriptor {
    FileHashes = [];
    PackNames = [];
    FileInfo = [];
    PackInfo = [];
    UnusedHashes = [];
    UnusedFileInfo = [];
}
//# sourceMappingURL=FileDescriptor.js.map