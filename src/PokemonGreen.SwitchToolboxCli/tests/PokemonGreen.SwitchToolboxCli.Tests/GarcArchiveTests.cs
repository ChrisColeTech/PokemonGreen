using PokemonGreen.SwitchToolboxCli.Core.Extraction;
using PokemonGreen.SwitchToolboxCli.Core.Loading;
using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Formats.Archives.Pokemon;
using PokemonGreen.SwitchToolboxCli.Formats.Archives.Rom3DS;
using System.Buffers.Binary;
using System.Text;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class GarcArchiveTests
{
    [Theory]
    [InlineData("CRAG")]
    [InlineData("GARC")]
    public void IdentifiesGarcBySignature(string magic)
    {
        var data = new byte[0x20];
        WriteAscii(data, 0, magic);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x04, sizeof(uint)), 0x1C);

        using var stream = new MemoryStream(data);

        var registry = new FormatRegistry();
        registry.Register(new GarcFormat());

        var loader = new FileLoader(registry);
        var opened = loader.Open(stream, "sample.bin");

        Assert.NotNull(opened);
        Assert.Equal("GARC", opened?.Format.FormatName);
    }

    [Fact]
    public void ExtractsMultipleFilesFromGarc()
    {
        var garcData = BuildGarc(
        [
            new byte[] { 0x01, 0x02, 0x03, 0x04 },
            new byte[] { 0x11, 0x22, 0x33 },
        ]);

        using var stream = new MemoryStream(garcData);

        var archive = GarcArchive.Parse(stream);
        var files = archive.Files.ToList();

        Assert.Equal(2, files.Count);
        Assert.Equal("file_000.bin", files[0].FileName);
        Assert.Equal("file_001.bin", files[1].FileName);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, ReadAll(files[0]));
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33 }, ReadAll(files[1]));
    }

    [Fact]
    public void WalksRootGarcAndParsesNestedRomFsEntries()
    {
        var romFsData = BuildMinimalRomFs("inside.bin", new byte[] { 9, 8, 7, 6 });
        var garcData = BuildGarc([
            romFsData,
            new byte[] { 0xCC, 0xDD },
        ]);

        var rootPath = Path.Combine(Path.GetTempPath(), $"garc-{Guid.NewGuid():N}.garc");
        File.WriteAllBytes(rootPath, garcData);

        try
        {
            var registry = new FormatRegistry();
            registry.Register(new GarcFormat());
            registry.Register(new RomFsFormat());

            var walker = new ArchiveWalker(new FileLoader(registry));
            var walked = walker.Walk(rootPath).ToList();

            Assert.Contains(walked, item => item.Format.FormatName == "GARC");
            Assert.Contains(walked, item => item.Format.FormatName == "RomFS");
            Assert.Contains(walked, item => item.Path.EndsWith("/file_000.bin", StringComparison.Ordinal));

            var romFsNode = Assert.Single(walked.Where(item => item.Format.FormatName == "RomFS"));
            var romFsArchive = Assert.IsType<RomFsArchive>(romFsNode.Value);
            var entry = Assert.Single(romFsArchive.Files);
            Assert.Equal("inside.bin", entry.FileName);
            Assert.Equal(new byte[] { 9, 8, 7, 6 }, ReadAll(entry));
        }
        finally
        {
            File.Delete(rootPath);
        }
    }

    private static byte[] BuildGarc(IReadOnlyList<byte[]> files)
    {
        var fatoOffsets = new List<uint>(files.Count);
        var fatbData = new List<byte>();
        var fimbData = new List<byte>();

        foreach (var file in files)
        {
            var dataStart = (uint)fimbData.Count;
            fimbData.AddRange(file);
            var dataEnd = (uint)fimbData.Count;
            var dataLength = (uint)file.Length;

            while ((fimbData.Count & 3) != 0)
            {
                fimbData.Add(0);
            }

            fatoOffsets.Add((uint)fatbData.Count);
            WriteUInt32(fatbData, 1u);
            WriteUInt32(fatbData, dataStart);
            WriteUInt32(fatbData, dataEnd);
            WriteUInt32(fatbData, dataLength);
        }

        var header = new byte[0x1C];
        WriteAscii(header, 0x00, "CRAG");
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x04, sizeof(uint)), 0x1C);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x08, sizeof(ushort)), 0xFEFF);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x0A, sizeof(ushort)), 0x0400);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x10, sizeof(ushort)), 4);

        var fatoChunk = BuildChunk("OTAF", files.Count, fatoOffsets);
        var fatbChunk = BuildChunk("BTAF", files.Count, fatbData);
        var fimbChunk = BuildChunk("BMIF", 0, fimbData);

        var fullData = new byte[header.Length + fatoChunk.Length + fatbChunk.Length + fimbChunk.Length];
        var cursor = 0;
        Buffer.BlockCopy(header, 0, fullData, cursor, header.Length);
        cursor += header.Length;

        Buffer.BlockCopy(fatoChunk, 0, fullData, cursor, fatoChunk.Length);
        cursor += fatoChunk.Length;

        Buffer.BlockCopy(fatbChunk, 0, fullData, cursor, fatbChunk.Length);
        cursor += fatbChunk.Length;

        Buffer.BlockCopy(fimbChunk, 0, fullData, cursor, fimbChunk.Length);

        BinaryPrimitives.WriteUInt32LittleEndian(fullData.AsSpan(0x0C, sizeof(uint)), (uint)fullData.Length);
        var fimbDataOffset = header.Length + fatoChunk.Length + fatbChunk.Length + 0x0C;
        BinaryPrimitives.WriteUInt32LittleEndian(fullData.AsSpan(0x14, sizeof(uint)), (uint)fimbDataOffset);

        return fullData;
    }

    private static byte[] BuildChunk(string magic, int entryCount, IReadOnlyList<uint> bodyData)
    {
        var size = 0x0C + (bodyData.Count * sizeof(uint));
        var chunk = new byte[size];
        WriteAscii(chunk, 0x00, magic);
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(0x04, sizeof(uint)), (uint)size);
        BinaryPrimitives.WriteUInt16LittleEndian(chunk.AsSpan(0x08, sizeof(ushort)), (ushort)entryCount);

        var cursor = 0x0C;
        foreach (var value in bodyData)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(cursor, sizeof(uint)), value);
            cursor += sizeof(uint);
        }

        return chunk;
    }

    private static byte[] BuildChunk(string magic, int entryCount, IReadOnlyList<byte> bodyData)
    {
        var size = 0x0C + bodyData.Count;
        var chunk = new byte[size];
        WriteAscii(chunk, 0x00, magic);
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(0x04, sizeof(uint)), (uint)size);
        BinaryPrimitives.WriteUInt16LittleEndian(chunk.AsSpan(0x08, sizeof(ushort)), (ushort)entryCount);

        for (var i = 0; i < bodyData.Count; i++)
        {
            chunk[0x0C + i] = bodyData[i];
        }

        return chunk;
    }

    private static byte[] ReadAll(ArchiveEntry entry)
    {
        using var stream = entry.OpenRead();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
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

    private static void WriteUInt32(List<byte> data, uint value)
    {
        var bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        data.AddRange(bytes);
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
