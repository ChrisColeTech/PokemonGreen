using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using PokemonGreen.SwitchToolboxCli.App.Commands;
using PokemonGreen.SwitchToolboxCli.App.Services;
using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Core.Extraction;
using PokemonGreen.SwitchToolboxCli.Core.Loading;
using PokemonGreen.SwitchToolboxCli.Core.Models;
using PokemonGreen.SwitchToolboxCli.Formats.Archives.Pokemon;
using PokemonGreen.SwitchToolboxCli.Formats.Archives.TRPAK;
using PokemonGreen.SwitchToolboxCli.Formats.Archives.TRPFS;
using PokemonGreen.SwitchToolboxCli.Formats.Common;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Animations;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Models;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Textures;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class TrinityPipelineIntegrationTests
{
    [Fact]
    public void SyntheticTrpfsNestedTrpak_ExtractAndConvert_AreDeterministic()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"trinity-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var inputPath = Path.Combine(tempDir, "sample.trpfs");
        var trpfdPath = Path.Combine(tempDir, "sample.trpfd");
        var extractOutputDir = Path.Combine(tempDir, "extract-out");
        var convertOutputDir = Path.Combine(tempDir, "convert-out");
        var nestedTrpakPath = "packs/character_bundle.trpak";

        var nestedTrpakPayload = SyntheticArchiveBuilder.BuildTrpak(
        [
            (SyntheticArchiveBuilder.BuildJsonModel("NestedModel"), 255),
            ([(byte)'C', (byte)'H', (byte)'R', (byte)'0', 0x01], 255),
        ]);

        var directObjPayload = Encoding.UTF8.GetBytes("o DirectPass\nv 0 0 0\n");
        var directClipPayload = new byte[] { (byte)'C', (byte)'H', (byte)'R', (byte)'0', 0x02 };

        File.WriteAllBytes(
            inputPath,
            SyntheticArchiveBuilder.BuildTrpfs(
            [
                ("models/direct_pass.obj", directObjPayload),
                ("models/direct_pass_anim_idle.tranm", directClipPayload),
                (nestedTrpakPath, nestedTrpakPayload),
            ]));
        File.WriteAllBytes(trpfdPath, SyntheticArchiveBuilder.BuildTrpfd([
            "models/direct_pass.obj",
            "models/direct_pass_anim_idle.tranm",
            nestedTrpakPath,
        ]));

        try
        {
            var discovery = CreateDiscoveryService();
            var extractor = new ArchiveExtractionService();
            var discovered = discovery.Discover(inputPath)
                .Select(item => (item.Path, item.FormatName))
                .OrderBy(item => item.Path, StringComparer.Ordinal)
                .ThenBy(item => item.FormatName, StringComparer.Ordinal)
                .ToList();

            Assert.Contains(discovered, item => item.Path == "sample.trpfs" && item.FormatName == "TRPFS");
            Assert.Contains(discovered, item => item.Path == "sample.trpfs/packs/character_bundle.trpak" && item.FormatName == "TRPAK");

            var extractExitCode = ExtractBinsCommand.Run(discovery, extractor, inputPath, extractOutputDir, TextWriter.Null, TextWriter.Null);
            Assert.Equal(0, extractExitCode);

            var extractedFiles = Directory.EnumerateFiles(extractOutputDir, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(extractOutputDir, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(
            [
                "sample.trpfs/models/direct_pass.obj",
                "sample.trpfs/models/direct_pass_anim_idle.tranm",
                "sample.trpfs/packs/character_bundle.trpak",
                "sample.trpfs/packs/character_bundle.trpak_001/file_000.bin",
                "sample.trpfs/packs/character_bundle.trpak_001/file_001.tranm",
            ],
                extractedFiles);

            var convertExitCode = ConvertCommand.Run(
                discovery,
                extractor,
                new ManifestFileWriter(),
                new ModelArchiveExportService(),
                new TrinityModelAssemblyService(),
                new TextureArchiveExportService(),
                new AnimationClipArchiveExportService(),
                inputPath,
                convertOutputDir,
                "obj",
                extractArchives: false,
                TextWriter.Null,
                TextWriter.Null);

            Assert.Equal(0, convertExitCode);

            var manifestPath = Path.Combine(convertOutputDir, "manifest.json");
            Assert.True(File.Exists(manifestPath));

            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            Assert.Equal("completed", manifest.RootElement.GetProperty("ModelConversionStatus").GetString());

            var modelRelativePath = "sample.trpfs/models/direct_pass/direct_pass.obj";
            var modelPath = Path.Combine(convertOutputDir, "models", modelRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(modelPath));

            var clipFiles = Directory.EnumerateFiles(Path.Combine(convertOutputDir, "clips"), "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Path.Combine(convertOutputDir, "clips"), path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            Assert.NotEmpty(clipFiles);
            Assert.All(clipFiles, path => Assert.StartsWith("sample.trpfs/", path));
            Assert.Contains("sample.trpfs/models/direct_pass/clips/direct_pass_anim_idle.tranm", clipFiles);
            Assert.False(Directory.Exists(Path.Combine(convertOutputDir, "models", "clips")));
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
    public void SyntheticTrpfsTrinityBinOnlyBundle_ProducesPendingConversionRecord()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"trinity-pending-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var inputPath = Path.Combine(tempDir, "pending.trpfs");
        var trpfdPath = Path.Combine(tempDir, "pending.trpfd");
        var convertOutputDir = Path.Combine(tempDir, "convert-out");

        var paths = new[]
        {
            "characters/pikachu/pikachu_00.trmdl",
            "characters/pikachu/pikachu_00_mesh.trmsh",
            "characters/pikachu/pikachu_00_skel.trskl",
            "characters/pikachu/pikachu_00_anim_idle.tranm",
        };

        File.WriteAllBytes(
            inputPath,
            SyntheticArchiveBuilder.BuildTrpfs(
            [
                (paths[0], [0x01, 0x02]),
                (paths[1], [0x03, 0x04]),
                (paths[2], [0x05, 0x06]),
                (paths[3], [(byte)'C', (byte)'H', (byte)'R', (byte)'0', 0x01]),
            ]));
        File.WriteAllBytes(trpfdPath, SyntheticArchiveBuilder.BuildTrpfd(paths));

        try
        {
            var discovery = CreateDiscoveryService();
            var extractor = new ArchiveExtractionService();
            var stdout = new StringWriter();

            var convertExitCode = ConvertCommand.Run(
                discovery,
                extractor,
                new ManifestFileWriter(),
                new ModelArchiveExportService(),
                new TrinityModelAssemblyService(),
                new TextureArchiveExportService(),
                new AnimationClipArchiveExportService(),
                inputPath,
                convertOutputDir,
                "obj",
                extractArchives: false,
                stdout,
                TextWriter.Null);

            Assert.Equal(0, convertExitCode);
            Assert.False(File.Exists(Path.Combine(convertOutputDir, "manifest.json")));
            Assert.Contains("pending: 1", stdout.ToString(), StringComparison.Ordinal);

            var discovered = discovery.Discover(inputPath);
            var trinitySources = discovered
                .Where(item => item.Value is IArchiveFile &&
                               (item.FormatName.Equals("TRPAK", StringComparison.OrdinalIgnoreCase) ||
                                item.FormatName.Equals("TRPFS", StringComparison.OrdinalIgnoreCase)))
                .Select(item => new TrinityArchiveSource(item.Path, (IArchiveFile)item.Value))
                .ToList();

            var bundles = new TrinityBinIndexerService().IndexArchives(trinitySources);
            var assembly = new TrinityModelAssemblyService().AssembleBundles(
                bundles,
                trinitySources,
                Path.Combine(tempDir, "assembly-models"),
                Path.Combine(tempDir, "assembly-textures"),
                Path.Combine(tempDir, "assembly-clips"),
                "obj");

            var pending = Assert.Single(assembly.Models);
            Assert.Equal("pending_conversion", pending.Status);
            Assert.Equal("characters/pikachu/pikachu_00", pending.BundleKey);

            var clipPath = Path.Combine(convertOutputDir, "clips", "pending.trpfs", "characters", "pikachu", "pikachu_00", "clips", "pikachu_00_anim_idle.tranm");
            Assert.True(File.Exists(clipPath));
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
    public void SyntheticTrpfsTrinityModernBundle_ProducesCompletedDaeWithController()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"trinity-modern-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var inputPath = Path.Combine(tempDir, "modern.trpfs");
        var trpfdPath = Path.Combine(tempDir, "modern.trpfd");
        var convertOutputDir = Path.Combine(tempDir, "convert-out");

        var paths = new[]
        {
            "characters/pikachu/pikachu_00.trmdl",
            "characters/pikachu/pikachu_00.trmsh",
            "characters/pikachu/pikachu_00.trmbf",
            "characters/pikachu/pikachu_00.trskl",
        };

        File.WriteAllBytes(
            inputPath,
            SyntheticArchiveBuilder.BuildTrpfs(
            [
                (paths[0], SyntheticArchiveBuilder.BuildTrmdl("Pikachu", paths[1], paths[2], paths[3])),
                (paths[1], SyntheticArchiveBuilder.BuildLegacyTrmsh(vertexCount: 3, indexCount: 3, vertexStride: 32, positionOffset: 0, normalOffset: 12, uvOffset: 24, indexFormat: 2)),
                (paths[2], SyntheticArchiveBuilder.BuildLegacyTrmbf()),
                (paths[3], SyntheticArchiveBuilder.BuildMinimalTrskl("PikachuRoot")),
            ]));
        File.WriteAllBytes(trpfdPath, SyntheticArchiveBuilder.BuildTrpfd(paths));

        try
        {
            var discovery = CreateDiscoveryService();
            var extractor = new ArchiveExtractionService();
            var convertExitCode = ConvertCommand.Run(
                discovery,
                extractor,
                new ManifestFileWriter(),
                new ModelArchiveExportService(),
                new TrinityModelAssemblyService(),
                new TextureArchiveExportService(),
                new AnimationClipArchiveExportService(),
                inputPath,
                convertOutputDir,
                "dae",
                extractArchives: false,
                TextWriter.Null,
                TextWriter.Null);

            Assert.Equal(0, convertExitCode);

            var trinitySources = discovery.Discover(inputPath)
                .Where(item => item.Value is IArchiveFile &&
                               (item.FormatName.Equals("TRPAK", StringComparison.OrdinalIgnoreCase) ||
                                item.FormatName.Equals("TRPFS", StringComparison.OrdinalIgnoreCase)))
                .Select(item => new TrinityArchiveSource(item.Path, (IArchiveFile)item.Value))
                .ToList();

            var bundles = new TrinityBinIndexerService().IndexArchives(trinitySources);
            var assemblyModelsRoot = Path.Combine(tempDir, "assembly-models");
            var assembly = new TrinityModelAssemblyService().AssembleBundles(
                bundles,
                trinitySources,
                assemblyModelsRoot,
                Path.Combine(tempDir, "assembly-textures"),
                Path.Combine(tempDir, "assembly-clips"),
                "dae");

            var completed = Assert.Single(assembly.Models);
            Assert.True(string.Equals("completed", completed.Status, StringComparison.Ordinal), completed.DetailMessage ?? "missing detail");
            Assert.EndsWith(".dae", completed.OutputRelativePath, StringComparison.Ordinal);

            var daePath = Path.Combine(assemblyModelsRoot, completed.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(daePath));

            var daeContent = File.ReadAllText(daePath);
            Assert.Contains("<library_controllers>", daeContent, StringComparison.Ordinal);
            Assert.Contains("<instance_controller", daeContent, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [EnvironmentVariableFact("PG_TRINITY_CORPUS")]
    public void CorpusSmoke_ConvertRunsWhenCorpusEnvironmentIsConfigured()
    {
        var corpusPath = Environment.GetEnvironmentVariable("PG_TRINITY_CORPUS")!;
        var inputPath = ResolveCorpusInput(corpusPath);
        var outputDir = Path.Combine(Path.GetTempPath(), $"trinity-corpus-smoke-{Guid.NewGuid():N}");

        try
        {
            var discovery = CreateDiscoveryService();
            var extractor = new ArchiveExtractionService();

            var exitCode = ConvertCommand.Run(
                discovery,
                extractor,
                new ManifestFileWriter(),
                new ModelArchiveExportService(),
                new TrinityModelAssemblyService(),
                new TextureArchiveExportService(),
                new AnimationClipArchiveExportService(),
                inputPath,
                outputDir,
                "obj",
                extractArchives: false,
                TextWriter.Null,
                TextWriter.Null);

            Assert.Equal(0, exitCode);
            Assert.True(Directory.Exists(outputDir));
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    private static DiscoveryService CreateDiscoveryService()
    {
        var registry = new FormatRegistry();
        registry.Register(new TrpfsFormat());
        registry.Register(new GarcFormat());
        registry.Register(new TrpakFormat());

        var loader = new FileLoader(registry);
        return new DiscoveryService(new ArchiveWalker(loader));
    }

    private static string ResolveCorpusInput(string corpusPath)
    {
        if (File.Exists(corpusPath))
        {
            return Path.GetFullPath(corpusPath);
        }

        if (!Directory.Exists(corpusPath))
        {
            throw new DirectoryNotFoundException($"PG_TRINITY_CORPUS path was not found: {corpusPath}");
        }

        var candidates = Directory.EnumerateFiles(corpusPath, "*.trpfs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(corpusPath, "*.trpak", SearchOption.AllDirectories))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"No .trpfs or .trpak files found under PG_TRINITY_CORPUS: {corpusPath}");
        }

        return candidates[0];
    }
}

public sealed class EnvironmentVariableFactAttribute : FactAttribute
{
    public EnvironmentVariableFactAttribute(string variableName)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variableName)))
        {
            Skip = $"Set {variableName} to enable this corpus smoke test.";
        }
    }
}

internal static class SyntheticArchiveBuilder
{
    public static byte[] BuildJsonModel(string modelName)
    {
        return Encoding.UTF8.GetBytes(
            $"{{\"modelName\":\"{modelName}\",\"meshes\":[{{\"meshName\":\"Main\",\"vertices\":[[0,0,0],[1,0,0],[0,1,0]],\"normals\":[[0,0,1],[0,0,1],[0,0,1]],\"uvs\":[[0,0],[1,0],[0,1]],\"indices\":[0,1,2]}}]}}");
    }

    public static byte[] BuildTrpak(IReadOnlyList<(byte[] Data, byte Compression)> files)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(0);

        var rootVtablePos = (int)stream.Position;
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)4);
        writer.Write((ushort)8);

        var rootTablePos = (int)stream.Position;
        writer.Write(rootTablePos - rootVtablePos);
        var hashesRelPos = (int)stream.Position;
        writer.Write(0);
        var filesRelPos = (int)stream.Position;
        writer.Write(0);

        var hashesVectorPos = (int)stream.Position;
        writer.Write(files.Count);
        for (var i = 0; i < files.Count; i++)
        {
            writer.Write((ulong)(0x2000 + i));
        }

        PatchInt32(stream, hashesRelPos, hashesVectorPos - hashesRelPos);

        var filesVectorPos = (int)stream.Position;
        writer.Write(files.Count);
        var fileSlotPositions = new int[files.Count];
        for (var i = 0; i < files.Count; i++)
        {
            fileSlotPositions[i] = (int)stream.Position;
            writer.Write(0);
        }

        PatchInt32(stream, filesRelPos, filesVectorPos - filesRelPos);

        for (var i = 0; i < files.Count; i++)
        {
            var fileVtablePos = (int)stream.Position;
            writer.Write((ushort)14);
            writer.Write((ushort)12);
            writer.Write((ushort)0);
            writer.Write((ushort)4);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)8);

            var fileTablePos = (int)stream.Position;
            writer.Write(fileTablePos - fileVtablePos);
            writer.Write(files[i].Compression);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
            var dataRelPos = (int)stream.Position;
            writer.Write(0);

            var dataVectorPos = (int)stream.Position;
            writer.Write(files[i].Data.Length);
            writer.Write(files[i].Data);

            PatchInt32(stream, dataRelPos, dataVectorPos - dataRelPos);
            PatchInt32(stream, fileSlotPositions[i], fileTablePos - fileSlotPositions[i]);
        }

        PatchInt32(stream, 0, rootTablePos);
        return stream.ToArray();
    }

    public static byte[] BuildTrpfd(IReadOnlyList<string> paths)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(0);

        var rootVtablePos = (int)stream.Position;
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)0);
        writer.Write((ushort)8);

        var rootTablePos = (int)stream.Position;
        writer.Write(rootTablePos - rootVtablePos);
        writer.Write(0);
        var stringVectorRelPos = (int)stream.Position;
        writer.Write(0);

        var vectorPos = (int)stream.Position;
        writer.Write(paths.Count);

        var stringRelPositions = new int[paths.Count];
        for (var i = 0; i < paths.Count; i++)
        {
            stringRelPositions[i] = (int)stream.Position;
            writer.Write(0);
        }

        for (var i = 0; i < paths.Count; i++)
        {
            var stringPos = (int)stream.Position;
            var encoded = Encoding.UTF8.GetBytes(paths[i]);
            writer.Write(encoded.Length);
            writer.Write(encoded);

            PatchInt32(stream, stringRelPositions[i], stringPos - stringRelPositions[i]);
        }

        PatchInt32(stream, stringVectorRelPos, vectorPos - stringVectorRelPos);
        PatchInt32(stream, 0, rootTablePos);

        return stream.ToArray();
    }

    public static byte[] BuildTrpfs(IReadOnlyList<(string Path, byte[] Payload)> entries)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(Encoding.ASCII.GetBytes("ONEPACK\0"));
        writer.Write((long)0);

        var offsets = new List<ulong>(entries.Count);
        foreach (var entry in entries)
        {
            offsets.Add((ulong)stream.Position);
            writer.Write(entry.Payload);
        }

        var metadataOffset = stream.Position;
        var metadata = BuildTrpfsMetadata(entries.Select(entry => Fnv1a64.Compute(entry.Path)).ToList(), offsets);
        writer.Write(metadata);

        stream.Position = 8;
        writer.Write(metadataOffset);

        return stream.ToArray();
    }

    private static byte[] BuildTrpfsMetadata(IReadOnlyList<ulong> hashes, IReadOnlyList<ulong> offsets)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(0);

        var rootVtablePos = (int)stream.Position;
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)4);
        writer.Write((ushort)8);

        var rootTablePos = (int)stream.Position;
        writer.Write(rootTablePos - rootVtablePos);
        var hashesRelPos = (int)stream.Position;
        writer.Write(0);
        var offsetsRelPos = (int)stream.Position;
        writer.Write(0);

        var hashesVectorPos = (int)stream.Position;
        writer.Write(hashes.Count);
        foreach (var hash in hashes)
        {
            writer.Write(hash);
        }

        PatchInt32(stream, hashesRelPos, hashesVectorPos - hashesRelPos);

        var offsetsVectorPos = (int)stream.Position;
        writer.Write(offsets.Count);
        foreach (var offset in offsets)
        {
            writer.Write(offset);
        }

        PatchInt32(stream, offsetsRelPos, offsetsVectorPos - offsetsRelPos);
        PatchInt32(stream, 0, rootTablePos);

        return stream.ToArray();
    }

    public static byte[] BuildTrmdl(string modelName, string meshPath, string bufferPath, string skeletonPath)
    {
        _ = skeletonPath;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(0);

        var rootVtablePos = (int)stream.Position;
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)4);
        writer.Write((ushort)8);

        var rootTablePos = (int)stream.Position;
        writer.Write(rootTablePos - rootVtablePos);
        var modelNameRelPos = (int)stream.Position;
        writer.Write(0);
        var meshRefsRelPos = (int)stream.Position;
        writer.Write(0);

        var meshRefVectorPos = (int)stream.Position;
        writer.Write(1);
        var meshRefSlotPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, meshRefsRelPos, meshRefVectorPos - meshRefsRelPos);

        var meshRefVtablePos = (int)stream.Position;
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)4);
        writer.Write((ushort)8);

        var meshRefTablePos = (int)stream.Position;
        writer.Write(meshRefTablePos - meshRefVtablePos);
        var meshPathRelPos = (int)stream.Position;
        writer.Write(0);
        var bufferPathRelPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, meshRefSlotPos, meshRefTablePos - meshRefSlotPos);

        var modelNamePos = WriteString(writer, modelName);
        var meshPathPos = WriteString(writer, meshPath);
        var bufferPathPos = WriteString(writer, bufferPath);
        PatchInt32(stream, modelNameRelPos, modelNamePos - modelNameRelPos);
        PatchInt32(stream, meshPathRelPos, meshPathPos - meshPathRelPos);
        PatchInt32(stream, bufferPathRelPos, bufferPathPos - bufferPathRelPos);
        PatchInt32(stream, 0, rootTablePos);
        return stream.ToArray();
    }

    public static byte[] BuildLegacyTrmsh(int vertexCount, int indexCount, int vertexStride, int positionOffset, int normalOffset, int uvOffset, int indexFormat)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(0);

        var rootVtablePos = (int)stream.Position;
        writer.Write((ushort)6);
        writer.Write((ushort)8);
        writer.Write((ushort)4);

        var rootTablePos = (int)stream.Position;
        writer.Write(rootTablePos - rootVtablePos);
        var meshesRelPos = (int)stream.Position;
        writer.Write(0);

        var meshesVectorPos = (int)stream.Position;
        writer.Write(1);
        var meshSlotPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, meshesRelPos, meshesVectorPos - meshesRelPos);

        var meshVtablePos = (int)stream.Position;
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)4);
        writer.Write((ushort)8);

        var meshTablePos = (int)stream.Position;
        writer.Write(meshTablePos - meshVtablePos);
        var meshNameRelPos = (int)stream.Position;
        writer.Write(0);
        var partsRelPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, meshSlotPos, meshTablePos - meshSlotPos);

        var partsVectorPos = (int)stream.Position;
        writer.Write(1);
        var partSlotPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, partsRelPos, partsVectorPos - partsRelPos);

        var partVtablePos = (int)stream.Position;
        writer.Write((ushort)22);
        writer.Write((ushort)40);
        writer.Write((ushort)4);
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)16);
        writer.Write((ushort)20);
        writer.Write((ushort)24);
        writer.Write((ushort)28);
        writer.Write((ushort)32);
        writer.Write((ushort)36);

        var partTablePos = (int)stream.Position;
        writer.Write(partTablePos - partVtablePos);
        writer.Write(0);
        writer.Write(vertexCount);
        writer.Write(indexCount);
        writer.Write(positionOffset);
        writer.Write(normalOffset);
        writer.Write(uvOffset);
        writer.Write(vertexStride);
        writer.Write(indexFormat);
        writer.Write(0);
        PatchInt32(stream, partSlotPos, partTablePos - partSlotPos);

        var meshNamePos = WriteString(writer, "Mesh0");
        PatchInt32(stream, meshNameRelPos, meshNamePos - meshNameRelPos);

        PatchInt32(stream, 0, rootTablePos);
        return stream.ToArray();
    }

    public static byte[] BuildLegacyTrmbf()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(0);

        var rootVtablePos = (int)stream.Position;
        writer.Write((ushort)6);
        writer.Write((ushort)8);
        writer.Write((ushort)4);

        var rootTablePos = (int)stream.Position;
        writer.Write(rootTablePos - rootVtablePos);
        var setsRelPos = (int)stream.Position;
        writer.Write(0);

        var setsVectorPos = (int)stream.Position;
        writer.Write(1);
        var setSlotPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, setsRelPos, setsVectorPos - setsRelPos);

        var setVtablePos = (int)stream.Position;
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)4);
        writer.Write((ushort)8);

        var setTablePos = (int)stream.Position;
        writer.Write(setTablePos - setVtablePos);
        var vertexRelPos = (int)stream.Position;
        writer.Write(0);
        var indexRelPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, setSlotPos, setTablePos - setSlotPos);

        var vertexVectorPos = (int)stream.Position;
        var vertexPayload = BuildVertexBuffer();
        writer.Write(vertexPayload.Length);
        writer.Write(vertexPayload);

        var indexVectorPos = (int)stream.Position;
        var indexPayload = BuildIndexBuffer([0, 1, 2]);
        writer.Write(indexPayload.Length);
        writer.Write(indexPayload);

        PatchInt32(stream, vertexRelPos, vertexVectorPos - vertexRelPos);
        PatchInt32(stream, indexRelPos, indexVectorPos - indexRelPos);

        PatchInt32(stream, 0, rootTablePos);
        return stream.ToArray();
    }

    public static byte[] BuildModernTrmsh(int indexCount, int indexOffset)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(0);

        var rootVtablePos = (int)stream.Position;
        writer.Write((ushort)10);
        writer.Write((ushort)16);
        writer.Write((ushort)4);
        writer.Write((ushort)8);
        writer.Write((ushort)12);

        var rootTablePos = (int)stream.Position;
        writer.Write(rootTablePos - rootVtablePos);
        writer.Write(1);
        var meshesRelPos = (int)stream.Position;
        writer.Write(0);
        writer.Write(0);

        var meshesVectorPos = (int)stream.Position;
        writer.Write(1);
        var meshSlotPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, meshesRelPos, meshesVectorPos - meshesRelPos);

        var meshVtablePos = (int)stream.Position;
        writer.Write((ushort)14);
        writer.Write((ushort)20);
        writer.Write((ushort)4);
        writer.Write((ushort)0);
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)16);

        var meshTablePos = (int)stream.Position;
        writer.Write(meshTablePos - meshVtablePos);
        var meshNameRelPos = (int)stream.Position;
        writer.Write(0);
        writer.Write(1);
        var declarationsRelPos = (int)stream.Position;
        writer.Write(0);
        var partsRelPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, meshSlotPos, meshTablePos - meshSlotPos);

        var declarationVectorPos = (int)stream.Position;
        writer.Write(1);
        var declarationSlotPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, declarationsRelPos, declarationVectorPos - declarationsRelPos);

        var declarationVtablePos = (int)stream.Position;
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)4);
        writer.Write((ushort)8);

        var declarationTablePos = (int)stream.Position;
        writer.Write(declarationTablePos - declarationVtablePos);
        var elementsRelPos = (int)stream.Position;
        writer.Write(0);
        var elementSizesRelPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, declarationSlotPos, declarationTablePos - declarationSlotPos);

        var elementsVectorPos = (int)stream.Position;
        writer.Write(3);
        var elementSlot0 = (int)stream.Position;
        writer.Write(0);
        var elementSlot1 = (int)stream.Position;
        writer.Write(0);
        var elementSlot2 = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, elementsRelPos, elementsVectorPos - elementsRelPos);

        var element0 = WriteVertexElement(writer, sizeIndex: 0, usage: 1, layer: 0, format: 51, offset: 0);
        var element1 = WriteVertexElement(writer, sizeIndex: 0, usage: 2, layer: 0, format: 51, offset: 12);
        var element2 = WriteVertexElement(writer, sizeIndex: 0, usage: 6, layer: 0, format: 48, offset: 24);
        PatchInt32(stream, elementSlot0, element0 - elementSlot0);
        PatchInt32(stream, elementSlot1, element1 - elementSlot1);
        PatchInt32(stream, elementSlot2, element2 - elementSlot2);

        var sizesVectorPos = (int)stream.Position;
        writer.Write(1);
        var sizeSlot0 = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, elementSizesRelPos, sizesVectorPos - elementSizesRelPos);

        var sizeEntry = WriteVertexElementSize(writer, 32);
        PatchInt32(stream, sizeSlot0, sizeEntry - sizeSlot0);

        var partsVectorPos = (int)stream.Position;
        writer.Write(1);
        var partSlotPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, partsRelPos, partsVectorPos - partsRelPos);

        var partVtablePos = (int)stream.Position;
        writer.Write((ushort)12);
        writer.Write((ushort)24);
        writer.Write((ushort)4);
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)16);
        writer.Write((ushort)20);

        var partTablePos = (int)stream.Position;
        writer.Write(partTablePos - partVtablePos);
        writer.Write(indexCount);
        writer.Write(indexOffset);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        PatchInt32(stream, partSlotPos, partTablePos - partSlotPos);

        var meshNamePos = WriteString(writer, "Mesh0");
        PatchInt32(stream, meshNameRelPos, meshNamePos - meshNameRelPos);

        PatchInt32(stream, 0, rootTablePos);
        return stream.ToArray();
    }

    public static byte[] BuildModernTrmbf()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(0);

        var rootVtablePos = (int)stream.Position;
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)0);
        writer.Write((ushort)8);

        var rootTablePos = (int)stream.Position;
        writer.Write(rootTablePos - rootVtablePos);
        writer.Write(0);
        var meshBuffersRelPos = (int)stream.Position;
        writer.Write(0);

        var meshBuffersVectorPos = (int)stream.Position;
        writer.Write(1);
        var meshBufferSlotPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, meshBuffersRelPos, meshBuffersVectorPos - meshBuffersRelPos);

        var meshBufferVtablePos = (int)stream.Position;
        writer.Write((ushort)10);
        writer.Write((ushort)12);
        writer.Write((ushort)4);
        writer.Write((ushort)8);
        writer.Write((ushort)0);

        var meshBufferTablePos = (int)stream.Position;
        writer.Write(meshBufferTablePos - meshBufferVtablePos);
        var indexBuffersRelPos = (int)stream.Position;
        writer.Write(0);
        var vertexBuffersRelPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, meshBufferSlotPos, meshBufferTablePos - meshBufferSlotPos);

        var indexVectorPos = (int)stream.Position;
        writer.Write(1);
        var indexSlotPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, indexBuffersRelPos, indexVectorPos - indexBuffersRelPos);

        var indexBufferTablePos = WriteBufferTable(writer, BuildIndexBuffer([0, 1, 2]));
        PatchInt32(stream, indexSlotPos, indexBufferTablePos - indexSlotPos);

        var vertexVectorPos = (int)stream.Position;
        writer.Write(1);
        var vertexSlotPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, vertexBuffersRelPos, vertexVectorPos - vertexBuffersRelPos);

        var vertexBufferTablePos = WriteBufferTable(writer, BuildVertexBuffer());
        PatchInt32(stream, vertexSlotPos, vertexBufferTablePos - vertexSlotPos);

        PatchInt32(stream, 0, rootTablePos);
        return stream.ToArray();
    }

    public static byte[] BuildMinimalTrskl(string rootBoneName)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(0);

        var rootVtablePos = (int)stream.Position;
        writer.Write((ushort)10);
        writer.Write((ushort)16);
        writer.Write((ushort)4);
        writer.Write((ushort)8);
        writer.Write((ushort)12);

        var rootTablePos = (int)stream.Position;
        writer.Write(rootTablePos - rootVtablePos);
        writer.Write(2u);
        var nodesRelPos = (int)stream.Position;
        writer.Write(0);
        var jointsRelPos = (int)stream.Position;
        writer.Write(0);

        var nodesVectorPos = (int)stream.Position;
        writer.Write(1);
        var nodeSlotPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, nodesRelPos, nodesVectorPos - nodesRelPos);

        var nodeVtablePos = (int)stream.Position;
        writer.Write((ushort)16);
        writer.Write((ushort)24);
        writer.Write((ushort)4);
        writer.Write((ushort)8);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)12);
        writer.Write((ushort)16);

        var nodeTablePos = (int)stream.Position;
        writer.Write(nodeTablePos - nodeVtablePos);
        var nodeNameRelPos = (int)stream.Position;
        writer.Write(0);
        var srtRelPos = (int)stream.Position;
        writer.Write(0);
        writer.Write(-1);
        writer.Write(0);
        PatchInt32(stream, nodeSlotPos, nodeTablePos - nodeSlotPos);

        var srtTablePos = WriteSrtTable(writer);
        PatchInt32(stream, srtRelPos, srtTablePos - srtRelPos);

        var jointsVectorPos = (int)stream.Position;
        writer.Write(1);
        var jointSlotPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, jointsRelPos, jointsVectorPos - jointsRelPos);

        var jointTablePos = WriteJointInfoTable(writer);
        PatchInt32(stream, jointSlotPos, jointTablePos - jointSlotPos);

        var nodeNamePos = WriteString(writer, rootBoneName);
        PatchInt32(stream, nodeNameRelPos, nodeNamePos - nodeNameRelPos);

        PatchInt32(stream, 0, rootTablePos);
        return stream.ToArray();
    }

    private static int WriteVertexElement(BinaryWriter writer, int sizeIndex, int usage, int layer, int format, int offset)
    {
        var tablePos = (int)writer.BaseStream.Position;
        writer.Write((ushort)14);
        writer.Write((ushort)24);
        writer.Write((ushort)4);
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)16);
        writer.Write((ushort)20);

        var valuePos = (int)writer.BaseStream.Position;
        writer.Write(valuePos - tablePos);
        writer.Write(sizeIndex);
        writer.Write(usage);
        writer.Write(layer);
        writer.Write(format);
        writer.Write(offset);
        return valuePos;
    }

    private static int WriteVertexElementSize(BinaryWriter writer, int size)
    {
        var tablePos = (int)writer.BaseStream.Position;
        writer.Write((ushort)6);
        writer.Write((ushort)8);
        writer.Write((ushort)4);

        var valuePos = (int)writer.BaseStream.Position;
        writer.Write(valuePos - tablePos);
        writer.Write(size);
        return valuePos;
    }

    private static int WriteBufferTable(BinaryWriter writer, byte[] payload)
    {
        var tablePos = (int)writer.BaseStream.Position;
        writer.Write((ushort)6);
        writer.Write((ushort)8);
        writer.Write((ushort)4);

        var valuePos = (int)writer.BaseStream.Position;
        writer.Write(valuePos - tablePos);
        var vectorRelPos = (int)writer.BaseStream.Position;
        writer.Write(0);

        var vectorPos = (int)writer.BaseStream.Position;
        writer.Write(payload.Length);
        writer.Write(payload);
        PatchInt32(writer.BaseStream, vectorRelPos, vectorPos - vectorRelPos);
        return valuePos;
    }

    private static int WriteSrtTable(BinaryWriter writer)
    {
        var tablePos = (int)writer.BaseStream.Position;
        writer.Write((ushort)10);
        writer.Write((ushort)16);
        writer.Write((ushort)4);
        writer.Write((ushort)8);
        writer.Write((ushort)12);

        var valuePos = (int)writer.BaseStream.Position;
        writer.Write(valuePos - tablePos);
        var scaleRelPos = (int)writer.BaseStream.Position;
        writer.Write(0);
        var rotateRelPos = (int)writer.BaseStream.Position;
        writer.Write(0);
        var translateRelPos = (int)writer.BaseStream.Position;
        writer.Write(0);

        var scalePos = WriteVector3Table(writer, 1f, 1f, 1f);
        var rotatePos = WriteVector3Table(writer, 0f, 0f, 0f);
        var translatePos = WriteVector3Table(writer, 0f, 0f, 0f);
        PatchInt32(writer.BaseStream, scaleRelPos, scalePos - scaleRelPos);
        PatchInt32(writer.BaseStream, rotateRelPos, rotatePos - rotateRelPos);
        PatchInt32(writer.BaseStream, translateRelPos, translatePos - translateRelPos);
        return valuePos;
    }

    private static int WriteJointInfoTable(BinaryWriter writer)
    {
        var tablePos = (int)writer.BaseStream.Position;
        writer.Write((ushort)10);
        writer.Write((ushort)12);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)4);

        var valuePos = (int)writer.BaseStream.Position;
        writer.Write(valuePos - tablePos);
        var matrixRelPos = (int)writer.BaseStream.Position;
        writer.Write(0);

        var matrixPos = WriteMatrix4x3Table(writer);
        PatchInt32(writer.BaseStream, matrixRelPos, matrixPos - matrixRelPos);
        return valuePos;
    }

    private static int WriteMatrix4x3Table(BinaryWriter writer)
    {
        var tablePos = (int)writer.BaseStream.Position;
        writer.Write((ushort)12);
        writer.Write((ushort)20);
        writer.Write((ushort)4);
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)16);

        var valuePos = (int)writer.BaseStream.Position;
        writer.Write(valuePos - tablePos);
        var axisXRelPos = (int)writer.BaseStream.Position;
        writer.Write(0);
        var axisYRelPos = (int)writer.BaseStream.Position;
        writer.Write(0);
        var axisZRelPos = (int)writer.BaseStream.Position;
        writer.Write(0);
        var axisWRelPos = (int)writer.BaseStream.Position;
        writer.Write(0);

        var axisX = WriteVector3Table(writer, 1f, 0f, 0f);
        var axisY = WriteVector3Table(writer, 0f, 1f, 0f);
        var axisZ = WriteVector3Table(writer, 0f, 0f, 1f);
        var axisW = WriteVector3Table(writer, 0f, 0f, 0f);
        PatchInt32(writer.BaseStream, axisXRelPos, axisX - axisXRelPos);
        PatchInt32(writer.BaseStream, axisYRelPos, axisY - axisYRelPos);
        PatchInt32(writer.BaseStream, axisZRelPos, axisZ - axisZRelPos);
        PatchInt32(writer.BaseStream, axisWRelPos, axisW - axisWRelPos);
        return valuePos;
    }

    private static int WriteVector3Table(BinaryWriter writer, float x, float y, float z)
    {
        var tablePos = (int)writer.BaseStream.Position;
        writer.Write((ushort)10);
        writer.Write((ushort)16);
        writer.Write((ushort)4);
        writer.Write((ushort)8);
        writer.Write((ushort)12);

        var valuePos = (int)writer.BaseStream.Position;
        writer.Write(valuePos - tablePos);
        writer.Write(x);
        writer.Write(y);
        writer.Write(z);
        return valuePos;
    }

    private static int WriteString(BinaryWriter writer, string text)
    {
        var position = (int)writer.BaseStream.Position;
        var bytes = Encoding.UTF8.GetBytes(text);
        writer.Write(bytes.Length);
        writer.Write(bytes);
        writer.Write((byte)0);
        return position;
    }

    private static int WriteStringVector(BinaryWriter writer, string value)
    {
        var vectorPos = (int)writer.BaseStream.Position;
        writer.Write(1);
        var slotPos = (int)writer.BaseStream.Position;
        writer.Write(0);
        var stringPos = WriteString(writer, value);
        PatchInt32(writer.BaseStream, slotPos, stringPos - slotPos);
        return vectorPos;
    }

    private static byte[] BuildVertexBuffer()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        WriteVertex(writer, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f);
        WriteVertex(writer, 1f, 0f, 0f, 0f, 0f, 1f, 1f, 0f);
        WriteVertex(writer, 0f, 1f, 0f, 0f, 0f, 1f, 0f, 1f);
        return stream.ToArray();
    }

    private static void WriteVertex(BinaryWriter writer, float x, float y, float z, float nx, float ny, float nz, float u, float v)
    {
        writer.Write(x);
        writer.Write(y);
        writer.Write(z);
        writer.Write(nx);
        writer.Write(ny);
        writer.Write(nz);
        writer.Write(u);
        writer.Write(v);
    }

    private static byte[] BuildIndexBuffer(ushort[] indices)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        foreach (var index in indices)
        {
            writer.Write(index);
        }

        return stream.ToArray();
    }

    private static void PatchInt32(Stream stream, int position, int value)
    {
        var end = stream.Position;
        stream.Position = position;
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        stream.Write(bytes);
        stream.Position = end;
    }

}
