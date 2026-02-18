using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using PokemonGreen.SwitchToolboxCli.App.Commands;
using PokemonGreen.SwitchToolboxCli.App.Services;
using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Core.Extraction;
using PokemonGreen.SwitchToolboxCli.Core.Loading;
using PokemonGreen.SwitchToolboxCli.Formats.Archives.Pokemon;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Animations;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Models;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Textures;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class CliCommandFoundationTests
{
    [Fact]
    public void ScanCommand_WritesJsonReportWithDetectedFormats()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"scan-{Guid.NewGuid():N}");
        var inputPath = Path.Combine(tempDir, "sample.garc");
        var reportPath = Path.Combine(tempDir, "report.json");

        Directory.CreateDirectory(tempDir);
        File.WriteAllBytes(inputPath, BuildGarc([new byte[] { 1, 2, 3 }]));

        try
        {
            var discovery = CreateDiscoveryService();
            var exitCode = ScanCommand.Run(discovery, inputPath, reportPath, TextWriter.Null, TextWriter.Null);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(reportPath));

            using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
            var items = doc.RootElement.GetProperty("Items");
            Assert.True(items.GetArrayLength() >= 1);

            var firstItem = items[0];
            Assert.Equal("GARC", firstItem.GetProperty("FormatName").GetString());
            Assert.Equal("sample.garc", firstItem.GetProperty("Path").GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ExtractGarcCommand_RequiresDetectedGarcAndExtractsEntries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"extract-{Guid.NewGuid():N}");
        var garcPath = Path.Combine(tempDir, "input.garc");
        var invalidPath = Path.Combine(tempDir, "not-garc.bin");
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);
        File.WriteAllBytes(garcPath, BuildGarc([new byte[] { 0xCA, 0xFE, 0xBA, 0xBE }]));
        File.WriteAllBytes(invalidPath, [0x00, 0x11, 0x22, 0x33]);

        try
        {
            var registry = new FormatRegistry();
            registry.Register(new GarcFormat());

            var loader = new FileLoader(registry);
            var extractor = new ArchiveExtractionService();

            var invalidExitCode = ExtractGarcCommand.Run(loader, extractor, invalidPath, outputDir, TextWriter.Null, TextWriter.Null);
            Assert.Equal(2, invalidExitCode);

            var validExitCode = ExtractGarcCommand.Run(loader, extractor, garcPath, outputDir, TextWriter.Null, TextWriter.Null);
            Assert.Equal(0, validExitCode);

            var extractedFilePath = Path.Combine(outputDir, "file_000.bin");
            Assert.True(File.Exists(extractedFilePath));
            Assert.Equal(new byte[] { 0xCA, 0xFE, 0xBA, 0xBE }, File.ReadAllBytes(extractedFilePath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ExtractBinsCommand_CreatesExtractedFilesOnly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"extract-bins-{Guid.NewGuid():N}");
        var inputPath = Path.Combine(tempDir, "sample.garc");
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);
        File.WriteAllBytes(inputPath, BuildGarc([
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            [(byte)'C', (byte)'H', (byte)'R', (byte)'0', 0x01, 0x02],
        ]));

        try
        {
            var discovery = CreateDiscoveryService();
            var extractor = new ArchiveExtractionService();
            var exitCode = ExtractBinsCommand.Run(
                discovery,
                extractor,
                inputPath,
                outputDir,
                TextWriter.Null,
                TextWriter.Null);

            Assert.Equal(0, exitCode);

            var extractedFiles = Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(outputDir, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            Assert.Equal([
                "sample.garc/file_000.bin",
                "sample.garc/file_001.bin",
            ], extractedFiles);

            Assert.False(Directory.Exists(Path.Combine(outputDir, "models")));
            Assert.False(Directory.Exists(Path.Combine(outputDir, "textures")));
            Assert.False(Directory.Exists(Path.Combine(outputDir, "clips")));
            Assert.False(File.Exists(Path.Combine(outputDir, "manifest.json")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ConvertCommand_UpdatesManifestStatusToCompleted_WhenModelExported()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"convert-model-{Guid.NewGuid():N}");
        var inputPath = Path.Combine(tempDir, "sample.garc");
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);
        File.WriteAllBytes(inputPath, BuildGarc([
            Encoding.UTF8.GetBytes("{\"modelName\":\"JsonModel\",\"meshes\":[{\"meshName\":\"Main\",\"vertices\":[[0,0,0],[1,0,0],[0,1,0]],\"normals\":[[0,0,1],[0,0,1],[0,0,1]],\"uvs\":[[0,0],[1,0],[0,1]],\"indices\":[0,1,2]}]}")
        ]));

        try
        {
            var discovery = CreateDiscoveryService();
            var extractor = new ArchiveExtractionService();
            var manifestWriter = new ManifestFileWriter();
            var textureExporter = new TextureArchiveExportService();
            var clipExporter = new AnimationClipArchiveExportService();
            var modelExporter = new ModelArchiveExportService();
            var trinityAssemblyService = new TrinityModelAssemblyService();

            var exitCode = ConvertCommand.Run(
                discovery,
                extractor,
                manifestWriter,
                modelExporter,
                trinityAssemblyService,
                textureExporter,
                clipExporter,
                inputPath,
                outputDir,
                "obj",
                true,
                TextWriter.Null,
                TextWriter.Null);

            Assert.Equal(0, exitCode);

            var modelsDir = Path.Combine(outputDir, "models");
            var modelFiles = Directory.EnumerateFiles(modelsDir, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(modelsDir, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            Assert.Contains("sample.garc/file_000.obj", modelFiles);

            var manifestPath = Path.Combine(outputDir, "manifest.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            Assert.Equal("completed", doc.RootElement.GetProperty("ModelConversionStatus").GetString());

            var models = doc.RootElement.GetProperty("Models");
            Assert.Equal(1, models.GetArrayLength());
            Assert.Equal("sample.garc/file_000.obj", models[0].GetProperty("OutputRelativePath").GetString());
            Assert.Equal("converted", models[0].GetProperty("ExportKind").GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ConvertCommand_WithExtractFalse_DoesNotCreateExtractedFolder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"convert-no-extract-{Guid.NewGuid():N}");
        var inputPath = Path.Combine(tempDir, "sample.garc");
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);
        File.WriteAllBytes(inputPath, BuildGarc([
            Encoding.UTF8.GetBytes("{\"modelName\":\"JsonModel\",\"meshes\":[{\"meshName\":\"Main\",\"vertices\":[[0,0,0],[1,0,0],[0,1,0]],\"normals\":[[0,0,1],[0,0,1],[0,0,1]],\"uvs\":[[0,0],[1,0],[0,1]],\"indices\":[0,1,2]}]}")
        ]));

        try
        {
            var discovery = CreateDiscoveryService();
            var extractor = new ArchiveExtractionService();
            var manifestWriter = new ManifestFileWriter();
            var textureExporter = new TextureArchiveExportService();
            var clipExporter = new AnimationClipArchiveExportService();
            var modelExporter = new ModelArchiveExportService();
            var trinityAssemblyService = new TrinityModelAssemblyService();

            var exitCode = ConvertCommand.Run(
                discovery,
                extractor,
                manifestWriter,
                modelExporter,
                trinityAssemblyService,
                textureExporter,
                clipExporter,
                inputPath,
                outputDir,
                "obj",
                false,
                TextWriter.Null,
                TextWriter.Null);

            Assert.Equal(0, exitCode);
            Assert.False(Directory.Exists(Path.Combine(outputDir, "extracted")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ConvertCommand_DoesNotWriteManifest_WhenNoModelOutputsExist()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"convert-no-manifest-{Guid.NewGuid():N}");
        var inputPath = Path.Combine(tempDir, "sample.garc");
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);
        File.WriteAllBytes(inputPath, BuildGarc([
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            [(byte)'C', (byte)'H', (byte)'R', (byte)'0', 0x01, 0x02],
            [0x10, 0x20, 0x30],
        ]));

        try
        {
            var discovery = CreateDiscoveryService();
            var extractor = new ArchiveExtractionService();
            var manifestWriter = new ManifestFileWriter();
            var textureExporter = new TextureArchiveExportService();
            var clipExporter = new AnimationClipArchiveExportService();
            var modelExporter = new ModelArchiveExportService();
            var trinityAssemblyService = new TrinityModelAssemblyService();

            var exitCode = ConvertCommand.Run(
                discovery,
                extractor,
                manifestWriter,
                modelExporter,
                trinityAssemblyService,
                textureExporter,
                clipExporter,
                inputPath,
                outputDir,
                "obj",
                false,
                TextWriter.Null,
                TextWriter.Null);

            Assert.Equal(0, exitCode);
            Assert.True(Directory.Exists(Path.Combine(outputDir, "textures")));
            Assert.True(Directory.Exists(Path.Combine(outputDir, "clips")));
            Assert.False(Directory.Exists(Path.Combine(outputDir, "models")));
            Assert.False(File.Exists(Path.Combine(outputDir, "manifest.json")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ConvertCommand_WithTrinityBinOnlyBundle_CompletesWithoutCrash()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"convert-trinity-pending-{Guid.NewGuid():N}");
        var inputPath = Path.Combine(tempDir, "sample.trpak");
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);
        File.WriteAllBytes(inputPath, [0x54, 0x52, 0x50, 0x41]);

        try
        {
            var trpakArchive = new InMemoryArchiveFile(
            [
                CreateArchiveEntry("characters/pikachu/pikachu_00.trmdl", [0x01, 0x02]),
                CreateArchiveEntry("characters/pikachu/pikachu_00_mesh.trmsh", [0x03, 0x04]),
                CreateArchiveEntry("characters/pikachu/pikachu_00_skel.trskl", [0x05, 0x06]),
                CreateArchiveEntry("characters/pikachu/pikachu_00_anim_idle.tranm", [(byte)'C', (byte)'H', (byte)'R', (byte)'0', 0x01]),
            ]);

            var discovery = CreateDiscoveryServiceForFormats(
            [
                new FixedArchiveFormat("TRPAK", ".trpak", trpakArchive),
            ]);

            var extractor = new ArchiveExtractionService();
            var manifestWriter = new ManifestFileWriter();
            var textureExporter = new TextureArchiveExportService();
            var clipExporter = new AnimationClipArchiveExportService();
            var modelExporter = new ModelArchiveExportService();
            var trinityAssemblyService = new TrinityModelAssemblyService();

            var exitCode = ConvertCommand.Run(
                discovery,
                extractor,
                manifestWriter,
                modelExporter,
                trinityAssemblyService,
                textureExporter,
                clipExporter,
                inputPath,
                outputDir,
                "obj",
                false,
                TextWriter.Null,
                TextWriter.Null);

            Assert.Equal(0, exitCode);
            Assert.False(File.Exists(Path.Combine(outputDir, "manifest.json")));

            var clipOutput = Path.Combine(outputDir, "clips", "sample.trpak", "characters", "pikachu", "pikachu_00", "clips", "pikachu_00_anim_idle.tranm");
            Assert.True(File.Exists(clipOutput));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ConvertCommand_WritesExpectedFolders_AndCoreManifestShape()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"convert-structure-{Guid.NewGuid():N}");
        var inputPath = Path.Combine(tempDir, "sample.garc");
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);
        File.WriteAllBytes(inputPath, BuildGarc([
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            [(byte)'C', (byte)'H', (byte)'R', (byte)'0', 0x01, 0x02],
            Encoding.UTF8.GetBytes("{\"modelName\":\"JsonModel\",\"meshes\":[{\"meshName\":\"Main\",\"vertices\":[[0,0,0],[1,0,0],[0,1,0]],\"normals\":[[0,0,1],[0,0,1],[0,0,1]],\"uvs\":[[0,0],[1,0],[0,1]],\"indices\":[0,1,2]}]}")
        ]));

        try
        {
            var discovery = CreateDiscoveryService();
            var extractor = new ArchiveExtractionService();
            var manifestWriter = new ManifestFileWriter();
            var textureExporter = new TextureArchiveExportService();
            var clipExporter = new AnimationClipArchiveExportService();
            var modelExporter = new ModelArchiveExportService();
            var trinityAssemblyService = new TrinityModelAssemblyService();

            var exitCode = ConvertCommand.Run(
                discovery,
                extractor,
                manifestWriter,
                modelExporter,
                trinityAssemblyService,
                textureExporter,
                clipExporter,
                inputPath,
                outputDir,
                "obj",
                true,
                TextWriter.Null,
                TextWriter.Null);

            Assert.Equal(0, exitCode);
            Assert.True(Directory.Exists(Path.Combine(outputDir, "extracted")));
            Assert.True(Directory.Exists(Path.Combine(outputDir, "models")));
            Assert.True(Directory.Exists(Path.Combine(outputDir, "textures")));
            Assert.True(Directory.Exists(Path.Combine(outputDir, "clips")));

            var manifestPath = Path.Combine(outputDir, "manifest.json");
            Assert.True(File.Exists(manifestPath));

            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = doc.RootElement;
            Assert.Equal("obj", root.GetProperty("ModelFormat").GetString());
            Assert.Equal("completed", root.GetProperty("ModelConversionStatus").GetString());
            Assert.True(root.GetProperty("DetectedItems").GetArrayLength() >= 1);
            Assert.True(root.GetProperty("Archives").GetArrayLength() >= 1);
            Assert.True(root.GetProperty("Models").GetArrayLength() >= 1);
            Assert.True(root.GetProperty("Textures").GetArrayLength() >= 1);
            Assert.True(root.GetProperty("Clips").GetArrayLength() >= 1);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void BatchCommand_WritesExpectedFolders_AndCoreManifestShape()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"batch-structure-{Guid.NewGuid():N}");
        var inputPath = Path.Combine(tempDir, "sample.garc");
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(tempDir);
        File.WriteAllBytes(inputPath, BuildGarc([
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            [(byte)'C', (byte)'H', (byte)'R', (byte)'0', 0x01, 0x02],
            Encoding.UTF8.GetBytes("{\"modelName\":\"JsonModel\",\"meshes\":[{\"meshName\":\"Main\",\"vertices\":[[0,0,0],[1,0,0],[0,1,0]],\"normals\":[[0,0,1],[0,0,1],[0,0,1]],\"uvs\":[[0,0],[1,0],[0,1]],\"indices\":[0,1,2]}]}")
        ]));

        try
        {
            var discovery = CreateDiscoveryService();
            var extractor = new ArchiveExtractionService();
            var manifestWriter = new ManifestFileWriter();
            var textureExporter = new TextureArchiveExportService();
            var clipExporter = new AnimationClipArchiveExportService();
            var modelExporter = new ModelArchiveExportService();

            var exitCode = BatchCommand.Run(
                discovery,
                extractor,
                manifestWriter,
                modelExporter,
                textureExporter,
                clipExporter,
                inputPath,
                outputDir,
                "dae",
                TextWriter.Null,
                TextWriter.Null);

            Assert.Equal(0, exitCode);
            Assert.True(Directory.Exists(Path.Combine(outputDir, "extracted")));
            Assert.True(Directory.Exists(Path.Combine(outputDir, "models")));
            Assert.True(Directory.Exists(Path.Combine(outputDir, "textures")));
            Assert.True(Directory.Exists(Path.Combine(outputDir, "clips")));

            var manifestPath = Path.Combine(outputDir, "batch-manifest.json");
            Assert.True(File.Exists(manifestPath));

            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = doc.RootElement;
            Assert.Equal("dae", root.GetProperty("ModelFormat").GetString());
            Assert.Equal("completed", root.GetProperty("ModelConversionStatus").GetString());
            Assert.True(root.GetProperty("DetectedItems").GetArrayLength() >= 1);
            Assert.True(root.GetProperty("Archives").GetArrayLength() >= 1);
            Assert.True(root.GetProperty("Models").GetArrayLength() >= 1);
            Assert.True(root.GetProperty("Textures").GetArrayLength() >= 1);
            Assert.True(root.GetProperty("Clips").GetArrayLength() >= 1);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static DiscoveryService CreateDiscoveryService()
    {
        return CreateDiscoveryServiceForFormats([new GarcFormat()]);
    }

    private static DiscoveryService CreateDiscoveryServiceForFormats(IReadOnlyList<IFileFormat> formats)
    {
        var registry = new FormatRegistry();
        foreach (var format in formats)
        {
            registry.Register(format);
        }

        var loader = new FileLoader(registry);
        return new DiscoveryService(new ArchiveWalker(loader));
    }

    private static ArchiveEntry CreateArchiveEntry(string fileName, byte[] payload)
    {
        return new ArchiveEntry
        {
            FileName = fileName,
            OpenRead = () => new MemoryStream(payload, writable: false),
        };
    }

    private static byte[] BuildGarc(IReadOnlyList<byte[]> files)
    {
        var fatoOffsets = new List<uint>(files.Count);
        var fatbData = new List<byte>();
        var fimbData = new List<byte>();

        foreach (var file in files)
        {
            var start = (uint)fimbData.Count;
            fimbData.AddRange(file);
            var end = (uint)fimbData.Count;

            while ((fimbData.Count & 3) != 0)
            {
                fimbData.Add(0);
            }

            fatoOffsets.Add((uint)fatbData.Count);
            WriteUInt32(fatbData, 1u);
            WriteUInt32(fatbData, start);
            WriteUInt32(fatbData, end);
            WriteUInt32(fatbData, (uint)file.Length);
        }

        var header = new byte[0x1C];
        WriteAscii(header, 0x00, "CRAG");
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x04, sizeof(uint)), 0x1C);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x08, sizeof(ushort)), 0xFEFF);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x0A, sizeof(ushort)), 0x0400);

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

    private static void WriteAscii(byte[] buffer, int offset, string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            buffer[offset + i] = (byte)value[i];
        }
    }

    private static void WriteUInt32(List<byte> data, uint value)
    {
        var bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        data.AddRange(bytes);
    }

    private sealed class FixedArchiveFormat : IFileFormat
    {
        private readonly string _extension;
        private readonly IArchiveFile _archive;

        public FixedArchiveFormat(string formatName, string extension, IArchiveFile archive)
        {
            FormatName = formatName;
            _extension = extension;
            _archive = archive;
            Extensions = [extension];
        }

        public string FormatName { get; }

        public IReadOnlyList<string> Extensions { get; }

        public bool Identify(Stream stream, string? fileName)
        {
            return fileName is not null &&
                   fileName.EndsWith(_extension, StringComparison.OrdinalIgnoreCase);
        }

        public object Load(Stream stream, string? fileName)
        {
            return _archive;
        }
    }

    private sealed class InMemoryArchiveFile : IArchiveFile
    {
        private readonly IReadOnlyList<ArchiveEntry> _files;

        public InMemoryArchiveFile(IReadOnlyList<ArchiveEntry> files)
        {
            _files = files;
        }

        public IEnumerable<ArchiveEntry> Files => _files;
    }
}
