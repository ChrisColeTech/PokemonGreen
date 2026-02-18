using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Formats.Common;
using System.Buffers.Binary;

namespace PokemonGreen.SwitchToolboxCli.Formats.Archives.Rom3DS;

// Adapted from:
// D:\Projects\Switch-Toolbox\File_Format_Library\FileFormats\Rom\3DS\NCCH.cs
public sealed class NcchFormat : IFileFormat
{
    private const int MediaUnitSize = 0x200;

    public string FormatName => "NCCH";
    public IReadOnlyList<string> Extensions => new[] { ".cxi" };

    public bool Identify(Stream stream, string? fileName)
    {
        return BinaryUtil.MatchAscii(stream, 0x100, "NCCH");
    }

    public object Load(Stream stream, string? fileName)
    {
        var image = ReadRemaining(stream);
        var files = new List<ArchiveEntry>();

        if (image.Length >= 0x1BC &&
            TryReadUInt32(image, 0x1B4, out var romfsOffsetUnits) &&
            TryReadUInt32(image, 0x1B8, out var romfsSizeUnits) &&
            romfsOffsetUnits != 0 &&
            romfsSizeUnits != 0 &&
            TryMapMediaUnits(romfsOffsetUnits, romfsSizeUnits, image.Length, out var romfsOffset, out var romfsSize))
        {
            var romfsData = new byte[romfsSize];
            Buffer.BlockCopy(image, romfsOffset, romfsData, 0, romfsSize);
            files.Add(new ArchiveEntry
            {
                FileName = "RomFS.bin",
                OpenRead = () => new MemoryStream(romfsData, writable: false),
            });
        }

        return new NcchArchive(files);
    }

    private static byte[] ReadRemaining(Stream source)
    {
        using var copy = new MemoryStream();
        source.CopyTo(copy);
        return copy.ToArray();
    }

    private static bool TryReadUInt32(byte[] data, int offset, out uint value)
    {
        value = 0;
        if (offset < 0 || offset > data.Length - sizeof(uint))
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, sizeof(uint)));
        return true;
    }

    private static bool TryMapMediaUnits(uint offsetUnits, uint sizeUnits, int imageLength, out int offset, out int size)
    {
        offset = 0;
        size = 0;

        var mappedOffset = (ulong)offsetUnits * MediaUnitSize;
        var mappedSize = (ulong)sizeUnits * MediaUnitSize;
        if (mappedOffset > int.MaxValue || mappedSize > int.MaxValue)
        {
            return false;
        }

        offset = (int)mappedOffset;
        size = (int)mappedSize;
        if (offset < 0 || size <= 0)
        {
            return false;
        }

        return offset <= imageLength - size;
    }
}

public sealed class NcchArchive : IArchiveFile
{
    private readonly List<ArchiveEntry> _files;

    public NcchArchive(IEnumerable<ArchiveEntry> files)
    {
        _files = files.ToList();
    }

    public IEnumerable<ArchiveEntry> Files => _files;
}
