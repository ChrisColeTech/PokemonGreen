using PokemonGreen.SwitchToolboxCli.Core.Extraction;
using PokemonGreen.SwitchToolboxCli.Core.Loading;
using PokemonGreen.SwitchToolboxCli.Formats.Archives.Rom3DS;
using System.Buffers.Binary;
using System.Text;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class Rom3DsArchiveTraversalTests
{
    [Fact]
    public void WalksNcsdToNcchToRomFsAndParsesRomFsFiles()
    {
        var romFsData = BuildMinimalRomFs("hello.bin", new byte[] { 1, 2, 3, 4 });
        var ncchData = BuildNcchWithRomFs(romFsData);
        var ncsdData = BuildNcsdWithPartition0(ncchData);

        var rootPath = Path.Combine(Path.GetTempPath(), $"rom3ds-{Guid.NewGuid():N}.3ds");
        File.WriteAllBytes(rootPath, ncsdData);

        try
        {
            var registry = new FormatRegistry();
            registry.Register(new NcsdFormat());
            registry.Register(new NcchFormat());
            registry.Register(new RomFsFormat());

            var loader = new FileLoader(registry);
            var walker = new ArchiveWalker(loader);

            var walked = walker.Walk(rootPath).ToList();

            Assert.Contains(walked, item => item.Format.FormatName == "NCSD");
            Assert.Contains(walked, item => item.Format.FormatName == "NCCH");
            var romfsItem = Assert.Single(walked.Where(item => item.Format.FormatName == "RomFS"));

            var archive = Assert.IsType<RomFsArchive>(romfsItem.Value);
            var file = Assert.Single(archive.Files);
            Assert.Equal("hello.bin", file.FileName);

            using var fileStream = file.OpenRead();
            using var memory = new MemoryStream();
            fileStream.CopyTo(memory);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, memory.ToArray());
        }
        finally
        {
            File.Delete(rootPath);
        }
    }

    private static byte[] BuildNcsdWithPartition0(byte[] ncchData)
    {
        const int mediaUnitSize = 0x200;
        var ncchUnits = (ncchData.Length + mediaUnitSize - 1) / mediaUnitSize;
        var totalLength = mediaUnitSize + (ncchUnits * mediaUnitSize);
        var image = new byte[totalLength];

        WriteAscii(image, 0x100, "NCSD");
        WriteUInt32(image, 0x120, 1);
        WriteUInt32(image, 0x124, (uint)ncchUnits);
        Buffer.BlockCopy(ncchData, 0, image, mediaUnitSize, ncchData.Length);

        return image;
    }

    private static byte[] BuildNcchWithRomFs(byte[] romFsData)
    {
        const int mediaUnitSize = 0x200;
        var romFsUnits = (romFsData.Length + mediaUnitSize - 1) / mediaUnitSize;
        var totalLength = mediaUnitSize + (romFsUnits * mediaUnitSize);
        var image = new byte[totalLength];

        WriteAscii(image, 0x100, "NCCH");
        WriteUInt32(image, 0x104, (uint)(totalLength / mediaUnitSize));
        WriteUInt32(image, 0x1B4, 1);
        WriteUInt32(image, 0x1B8, (uint)romFsUnits);
        Buffer.BlockCopy(romFsData, 0, image, mediaUnitSize, romFsData.Length);

        return image;
    }

    private static byte[] BuildMinimalRomFs(string fileName, byte[] fileData)
    {
        var fileNameBytes = Encoding.Unicode.GetBytes(fileName);
        const int level3Position = 0x60;
        const int level3HeaderOffset = level3Position;
        const int dirMetaOffset = 0x28;
        const int fileMetaOffset = 0x40;
        const int fileDataOffset = 0x80;

        var fileDataAbsolute = level3Position + fileDataOffset;
        var totalLength = fileDataAbsolute + fileData.Length;
        var image = new byte[totalLength];

        WriteAscii(image, 0x00, "IVFC");
        WriteUInt32(image, 0x04, 0x00010000);
        WriteUInt32(image, 0x08, 0);
        WriteUInt32(image, 0x1C, 4);

        WriteUInt32(image, level3HeaderOffset + 0x00, 0x28);
        WriteUInt32(image, level3HeaderOffset + 0x0C, dirMetaOffset);
        WriteUInt32(image, level3HeaderOffset + 0x1C, fileMetaOffset);
        WriteUInt32(image, level3HeaderOffset + 0x24, fileDataOffset);

        var dirOffset = level3Position + dirMetaOffset;
        WriteUInt32(image, dirOffset + 0x00, 0xFFFFFFFF);
        WriteUInt32(image, dirOffset + 0x04, 0xFFFFFFFF);
        WriteUInt32(image, dirOffset + 0x08, 0xFFFFFFFF);
        WriteUInt32(image, dirOffset + 0x0C, 0);
        WriteUInt32(image, dirOffset + 0x10, 0xFFFFFFFF);
        WriteUInt32(image, dirOffset + 0x14, 0);

        var fileOffset = level3Position + fileMetaOffset;
        WriteUInt32(image, fileOffset + 0x00, 0);
        WriteUInt32(image, fileOffset + 0x04, 0xFFFFFFFF);
        WriteUInt64(image, fileOffset + 0x08, 0);
        WriteUInt64(image, fileOffset + 0x10, (ulong)fileData.Length);
        WriteUInt32(image, fileOffset + 0x18, 0xFFFFFFFF);
        WriteUInt32(image, fileOffset + 0x1C, (uint)fileNameBytes.Length);
        Buffer.BlockCopy(fileNameBytes, 0, image, fileOffset + 0x20, fileNameBytes.Length);

        Buffer.BlockCopy(fileData, 0, image, fileDataAbsolute, fileData.Length);
        return image;
    }

    private static void WriteAscii(byte[] buffer, int offset, string value)
    {
        Encoding.ASCII.GetBytes(value, buffer.AsSpan(offset, value.Length));
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset, sizeof(uint)), value);
    }

    private static void WriteUInt64(byte[] buffer, int offset, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(offset, sizeof(ulong)), value);
    }
}
