using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Formats.Common;
using System.Buffers.Binary;

namespace PokemonGreen.SwitchToolboxCli.Formats.Archives.Pokemon;

public sealed class GarcFormat : IFileFormat
{
    private static readonly string[] GarcMagics = ["CRAG", "GARC"];

    public string FormatName => "GARC";
    public IReadOnlyList<string> Extensions => new[] { ".garc", string.Empty };

    public bool Identify(Stream stream, string? fileName)
    {
        foreach (var magic in GarcMagics)
        {
            if (BinaryUtil.MatchAscii(stream, 0, magic))
            {
                return true;
            }
        }

        return false;
    }

    public object Load(Stream stream, string? fileName)
    {
        return GarcArchive.Parse(stream);
    }
}

public sealed class GarcArchive : IArchiveFile
{
    private const int SectionHeaderSize = 0x0C;

    private static readonly string[] GarcMagics = ["CRAG", "GARC"];
    private static readonly string[] FatoMagics = ["OTAF", "FATO"];
    private static readonly string[] FatbMagics = ["BTAF", "FATB"];
    private static readonly string[] FimbMagics = ["BMIF", "FIMB"];

    private readonly List<ArchiveEntry> _files;

    private GarcArchive(List<ArchiveEntry> files)
    {
        _files = files;
    }

    public IEnumerable<ArchiveEntry> Files => _files;

    public static GarcArchive Parse(Stream source)
    {
        var image = ReadRemaining(source);
        var files = new List<ArchiveEntry>();
        if (!HasMagic(image, 0, GarcMagics) || image.Length < SectionHeaderSize)
        {
            return new GarcArchive(files);
        }

        var sectionSearchStart = 0;
        if (TryReadUInt32(image, 0x04, out var headerSize) &&
            headerSize >= SectionHeaderSize &&
            headerSize <= (uint)(image.Length - SectionHeaderSize))
        {
            sectionSearchStart = (int)headerSize;
        }

        if (!TryFindSection(image, sectionSearchStart, FatoMagics, out var fatoOffset, out var fatoSize) ||
            !TryFindSection(image, Align4(fatoOffset + (int)fatoSize), FatbMagics, out var fatbOffset, out var fatbSize) ||
            !TryFindSection(image, Align4(fatbOffset + (int)fatbSize), FimbMagics, out var fimbOffset, out var fimbSize))
        {
            return new GarcArchive(files);
        }

        if (!TryReadFatoOffsets(image, fatoOffset, fatoSize, out var fatbEntryOffsets))
        {
            return new GarcArchive(files);
        }

        if (fimbSize < SectionHeaderSize)
        {
            return new GarcArchive(files);
        }

        var dataStart = fimbOffset + SectionHeaderSize;
        var dataSize = (int)fimbSize - SectionHeaderSize;
        if (!Fits(image, dataStart, dataSize))
        {
            return new GarcArchive(files);
        }

        var ranges = ReadFatbRanges(image, fatbOffset, fatbSize, fatbEntryOffsets);
        foreach (var range in ranges)
        {
            var length = range.End >= range.Start ? range.End - range.Start : 0u;
            if (range.Length != 0 && range.Length <= length)
            {
                length = range.Length;
            }

            if (length == 0 || !TrySlice(image, dataStart + (int)range.Start, (int)length, out var payload))
            {
                continue;
            }

            var generatedName = $"file_{files.Count:000}.bin";
            files.Add(new ArchiveEntry
            {
                FileName = generatedName,
                OpenRead = () => new MemoryStream(payload, writable: false),
            });
        }

        return new GarcArchive(files);
    }

    private static List<FatbRange> ReadFatbRanges(byte[] data, int fatbOffset, uint fatbSize, IReadOnlyList<uint> fatbEntryOffsets)
    {
        var ranges = new List<FatbRange>();
        var fatbDataOffset = fatbOffset + SectionHeaderSize;
        var fatbEnd = fatbOffset + (int)fatbSize;

        foreach (var entryRelativeOffset in fatbEntryOffsets)
        {
            if (entryRelativeOffset > int.MaxValue)
            {
                continue;
            }

            var cursor = fatbDataOffset + (int)entryRelativeOffset;
            if (!Fits(data, cursor, sizeof(uint)))
            {
                continue;
            }

            var vector = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(cursor, sizeof(uint)));
            cursor += sizeof(uint);
            for (var bit = 0; bit < 32; bit++)
            {
                if ((vector & (1u << bit)) == 0)
                {
                    continue;
                }

                if (cursor > fatbEnd - (sizeof(uint) * 3))
                {
                    break;
                }

                var start = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(cursor, sizeof(uint)));
                var end = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(cursor + sizeof(uint), sizeof(uint)));
                var length = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(cursor + (sizeof(uint) * 2), sizeof(uint)));
                cursor += sizeof(uint) * 3;

                if (end < start)
                {
                    continue;
                }

                ranges.Add(new FatbRange(start, end, length));
            }
        }

        return ranges;
    }

    private static bool TryReadFatoOffsets(byte[] data, int fatoOffset, uint fatoSize, out List<uint> offsets)
    {
        offsets = new List<uint>();
        if (!Fits(data, fatoOffset, (int)fatoSize) || fatoSize < SectionHeaderSize || !TryReadUInt16(data, fatoOffset + 0x08, out var rawEntryCount))
        {
            return false;
        }

        var availableCount = ((int)fatoSize - SectionHeaderSize) / sizeof(uint);
        if (availableCount <= 0)
        {
            return false;
        }

        var entryCount = Math.Min(rawEntryCount, availableCount);
        if (entryCount <= 0)
        {
            return false;
        }

        var offsetBase = fatoOffset + SectionHeaderSize;
        for (var i = 0; i < entryCount; i++)
        {
            offsets.Add(BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offsetBase + (i * sizeof(uint)), sizeof(uint))));
        }

        return true;
    }

    private static byte[] ReadRemaining(Stream source)
    {
        using var copy = new MemoryStream();
        source.CopyTo(copy);
        return copy.ToArray();
    }

    private static bool TryFindSection(byte[] data, int startOffset, IReadOnlyList<string> magics, out int sectionOffset, out uint sectionSize)
    {
        sectionOffset = 0;
        sectionSize = 0;

        var start = Math.Max(0, startOffset);
        for (var offset = start; offset <= data.Length - SectionHeaderSize; offset += 4)
        {
            if (!HasMagic(data, offset, magics) || !TryReadUInt32(data, offset + 0x04, out var size) || size < SectionHeaderSize || size > int.MaxValue)
            {
                continue;
            }

            if (!Fits(data, offset, (int)size))
            {
                continue;
            }

            sectionOffset = offset;
            sectionSize = size;
            return true;
        }

        for (var offset = start; offset <= data.Length - SectionHeaderSize; offset++)
        {
            if (!HasMagic(data, offset, magics) || !TryReadUInt32(data, offset + 0x04, out var size) || size < SectionHeaderSize || size > int.MaxValue)
            {
                continue;
            }

            if (!Fits(data, offset, (int)size))
            {
                continue;
            }

            sectionOffset = offset;
            sectionSize = size;
            return true;
        }

        return false;
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

    private static bool TryReadUInt16(byte[] data, int offset, out ushort value)
    {
        value = 0;
        if (!Fits(data, offset, sizeof(ushort)))
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, sizeof(ushort)));
        return true;
    }

    private static bool TrySlice(byte[] data, int offset, int length, out byte[] slice)
    {
        slice = Array.Empty<byte>();
        if (!Fits(data, offset, length) || length <= 0)
        {
            return false;
        }

        slice = new byte[length];
        Buffer.BlockCopy(data, offset, slice, 0, length);
        return true;
    }

    private static bool Fits(byte[] data, int offset, int length)
    {
        if (offset < 0 || length < 0)
        {
            return false;
        }

        return offset <= data.Length - length;
    }

    private static bool HasMagic(byte[] data, int offset, IReadOnlyList<string> magics)
    {
        foreach (var magic in magics)
        {
            if (HasMagic(data, offset, magic))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMagic(byte[] data, int offset, string magic)
    {
        if (!Fits(data, offset, magic.Length))
        {
            return false;
        }

        for (var i = 0; i < magic.Length; i++)
        {
            if (data[offset + i] != magic[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int Align4(int value)
    {
        var remainder = value & 3;
        return remainder == 0 ? value : value + (4 - remainder);
    }

    private readonly record struct FatbRange(uint Start, uint End, uint Length);
}
