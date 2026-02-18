using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Formats.Common;
using System.Buffers.Binary;
using System.Text;

namespace PokemonGreen.SwitchToolboxCli.Formats.Archives.Rom3DS;

// Adapted from:
// D:\Projects\Switch-Toolbox\File_Format_Library\FileFormats\Rom\3DS\RomFS.cs
public sealed class RomFsFormat : IFileFormat
{
    public string FormatName => "RomFS";
    public IReadOnlyList<string> Extensions => new[] { ".bin" };

    public bool Identify(Stream stream, string? fileName)
    {
        return BinaryUtil.MatchAscii(stream, 0, "IVFC");
    }

    public object Load(Stream stream, string? fileName)
    {
        return RomFsArchive.Parse(stream);
    }
}

public sealed class RomFsArchive : IArchiveFile
{
    private const uint NoOffset = 0xFFFFFFFF;
    private readonly List<ArchiveEntry> _files;

    private RomFsArchive(List<ArchiveEntry> files)
    {
        _files = files;
    }

    public IEnumerable<ArchiveEntry> Files => _files;

    public static RomFsArchive Parse(Stream source)
    {
        var data = ReadRemaining(source);
        var files = new List<ArchiveEntry>();

        if (data.Length < 0x88 || !IsAscii(data, 0, "IVFC"))
        {
            return new RomFsArchive(files);
        }

        if (!TryReadUInt32(data, 0x08, out var masterHashSize) ||
            !TryReadUInt32(data, 0x1C, out var level1BlockSizeLog2) ||
            level1BlockSizeLog2 > 31)
        {
            return new RomFsArchive(files);
        }

        var hashAlignment = 1u << (int)level1BlockSizeLog2;
        var level3Position = Align(Align(0x5C + masterHashSize, 0x10), hashAlignment);
        if (!Fits(data, level3Position, 0x28))
        {
            return new RomFsArchive(files);
        }

        if (!TryReadUInt32(data, (int)level3Position + 0x0C, out var dirMetaOffset) ||
            !TryReadUInt32(data, (int)level3Position + 0x1C, out var fileMetaOffset) ||
            !TryReadUInt32(data, (int)level3Position + 0x24, out var fileDataOffset))
        {
            return new RomFsArchive(files);
        }

        var directoryTableBase = level3Position + dirMetaOffset;
        var fileMetaTableBase = level3Position + fileMetaOffset;
        var fileDataBase = level3Position + fileDataOffset;
        if (!Fits(data, directoryTableBase, 0x18) || !Fits(data, fileMetaTableBase, 0x20) || !Fits(data, fileDataBase, 0))
        {
            return new RomFsArchive(files);
        }

        var visitedDirectories = new HashSet<uint>();
        ReadDirectory(data, files, directoryTableBase, fileMetaTableBase, fileDataBase, 0, string.Empty, visitedDirectories, 0);
        return new RomFsArchive(files);
    }

    private static void ReadDirectory(
        byte[] data,
        List<ArchiveEntry> files,
        long directoryTableBase,
        long fileMetaTableBase,
        long fileDataBase,
        uint relativeOffset,
        string parentPath,
        HashSet<uint> visitedDirectories,
        int depth)
    {
        if (depth > 128 || !visitedDirectories.Add(relativeOffset))
        {
            return;
        }

        var directoryOffset = directoryTableBase + relativeOffset;
        if (!Fits(data, directoryOffset, 0x18))
        {
            return;
        }

        if (!TryReadUInt32(data, (int)directoryOffset + 0x04, out var nextSiblingOffset) ||
            !TryReadUInt32(data, (int)directoryOffset + 0x08, out var firstChildOffset) ||
            !TryReadUInt32(data, (int)directoryOffset + 0x0C, out var firstFileOffset) ||
            !TryReadUInt32(data, (int)directoryOffset + 0x14, out var nameLength))
        {
            return;
        }

        var name = ReadUtf16Name(data, directoryOffset + 0x18, nameLength);
        var currentPath = string.IsNullOrEmpty(name)
            ? parentPath
            : parentPath + name.Replace('\\', '/') + '/';

        if (firstChildOffset != NoOffset)
        {
            ReadDirectory(data, files, directoryTableBase, fileMetaTableBase, fileDataBase, firstChildOffset, currentPath, visitedDirectories, depth + 1);
        }

        if (firstFileOffset != NoOffset)
        {
            ReadFileList(data, files, fileMetaTableBase, fileDataBase, firstFileOffset, currentPath);
        }

        if (nextSiblingOffset != NoOffset)
        {
            ReadDirectory(data, files, directoryTableBase, fileMetaTableBase, fileDataBase, nextSiblingOffset, parentPath, visitedDirectories, depth + 1);
        }
    }

    private static void ReadFileList(
        byte[] data,
        List<ArchiveEntry> files,
        long fileMetaTableBase,
        long fileDataBase,
        uint firstFileOffset,
        string currentPath)
    {
        var seenOffsets = new HashSet<uint>();
        var currentOffset = firstFileOffset;
        var safety = 0;

        while (currentOffset != NoOffset && safety++ < 4096)
        {
            if (!seenOffsets.Add(currentOffset))
            {
                break;
            }

            var fileMetaOffset = fileMetaTableBase + currentOffset;
            if (!Fits(data, fileMetaOffset, 0x20))
            {
                break;
            }

            if (!TryReadUInt32(data, (int)fileMetaOffset + 0x04, out var nextSiblingOffset) ||
                !TryReadUInt64(data, (int)fileMetaOffset + 0x08, out var fileDataOffset) ||
                !TryReadUInt64(data, (int)fileMetaOffset + 0x10, out var fileDataSize) ||
                !TryReadUInt32(data, (int)fileMetaOffset + 0x1C, out var nameLength))
            {
                break;
            }

            var fileName = ReadUtf16Name(data, fileMetaOffset + 0x20, nameLength);
            var fullName = string.IsNullOrWhiteSpace(fileName)
                ? currentPath.TrimEnd('/')
                : currentPath + fileName.Replace('\\', '/');

            if (TrySlice(data, fileDataBase + (long)fileDataOffset, fileDataSize, out var fileBytes) &&
                !string.IsNullOrWhiteSpace(fullName))
            {
                files.Add(new ArchiveEntry
                {
                    FileName = fullName,
                    OpenRead = () => new MemoryStream(fileBytes, writable: false),
                });
            }

            currentOffset = nextSiblingOffset;
        }
    }

    private static byte[] ReadRemaining(Stream source)
    {
        using var copy = new MemoryStream();
        source.CopyTo(copy);
        return copy.ToArray();
    }

    private static bool TrySlice(byte[] data, long offset, ulong length, out byte[] slice)
    {
        slice = Array.Empty<byte>();
        if (offset < 0 || length > int.MaxValue)
        {
            return false;
        }

        var intLength = (int)length;
        if (!Fits(data, offset, intLength))
        {
            return false;
        }

        slice = new byte[intLength];
        Buffer.BlockCopy(data, (int)offset, slice, 0, intLength);
        return true;
    }

    private static bool TryReadUInt32(byte[] data, int offset, out uint value)
    {
        value = 0;
        if (!Fits(data, offset, sizeof(uint)))
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, sizeof(uint)));
        return true;
    }

    private static bool TryReadUInt64(byte[] data, int offset, out ulong value)
    {
        value = 0;
        if (!Fits(data, offset, sizeof(ulong)))
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset, sizeof(ulong)));
        return true;
    }

    private static string ReadUtf16Name(byte[] data, long offset, uint byteLength)
    {
        if (byteLength == 0 || byteLength > int.MaxValue || !Fits(data, offset, (int)byteLength))
        {
            return string.Empty;
        }

        return Encoding.Unicode.GetString(data, (int)offset, (int)byteLength).TrimEnd('\0');
    }

    private static bool IsAscii(byte[] data, int offset, string signature)
    {
        if (!Fits(data, offset, signature.Length))
        {
            return false;
        }

        for (var i = 0; i < signature.Length; i++)
        {
            if (data[offset + i] != signature[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool Fits(byte[] data, long offset, long length)
    {
        if (offset < 0 || length < 0)
        {
            return false;
        }

        return offset <= data.Length - length;
    }

    private static long Align(long value, uint alignment)
    {
        if (alignment <= 1)
        {
            return value;
        }

        var remainder = value % alignment;
        return remainder == 0 ? value : value + alignment - remainder;
    }
}
