using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Formats.Common;
using System.Buffers.Binary;

namespace PokemonGreen.SwitchToolboxCli.Formats.Archives.Rom3DS;

// Adapted from:
// D:\Projects\Switch-Toolbox\File_Format_Library\FileFormats\Rom\3DS\NCSD.cs
public sealed class NcsdFormat : IFileFormat
{
    private const int MediaUnitSize = 0x200;
    private static readonly IReadOnlyDictionary<int, string> PartNames = new Dictionary<int, string>
    {
        [0] = "GameData.cxi",
        [1] = "EManual.cfa",
        [2] = "DLP.cfa",
        [6] = "FirmwareUpdate.cfa",
        [7] = "UpdateData.cfa",
    };

    public string FormatName => "NCSD";
    public IReadOnlyList<string> Extensions => new[] { ".3ds", ".cci" };

    public bool Identify(Stream stream, string? fileName)
    {
        return BinaryUtil.MatchAscii(stream, 0x100, "NCSD");
    }

    public object Load(Stream stream, string? fileName)
    {
        var image = ReadRemaining(stream);
        var files = new List<ArchiveEntry>();

        const int partitionTableOffset = 0x120;
        for (var i = 0; i < 8; i++)
        {
            var partOffset = partitionTableOffset + i * 8;
            if (!TryReadUInt32(image, partOffset, out var offsetUnits) ||
                !TryReadUInt32(image, partOffset + 4, out var sizeUnits) ||
                offsetUnits == 0 ||
                sizeUnits == 0 ||
                !TryMapMediaUnits(offsetUnits, sizeUnits, image.Length, out var mappedOffset, out var mappedSize))
            {
                continue;
            }

            var entryData = new byte[mappedSize];
            Buffer.BlockCopy(image, mappedOffset, entryData, 0, mappedSize);

            var fileNamePrefix = PartNames.TryGetValue(i, out var knownName) ? knownName : $"Partition{i}.cfa";
            files.Add(new ArchiveEntry
            {
                FileName = fileNamePrefix,
                OpenRead = () => new MemoryStream(entryData, writable: false),
            });
        }

        return new NcsdArchive(files);
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

public sealed class NcsdArchive : IArchiveFile
{
    private readonly List<ArchiveEntry> _files;

    public NcsdArchive(IEnumerable<ArchiveEntry> files)
    {
        _files = files.ToList();
    }

    public IEnumerable<ArchiveEntry> Files => _files;
}
