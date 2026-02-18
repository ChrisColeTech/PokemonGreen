using System.Text;
using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Core.Extraction;
using PokemonGreen.SwitchToolboxCli.Core.Models;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Models;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class TrinityModelAssemblyServiceTests
{
    [Fact]
    public void AssembleBundles_WritesPassThroughModelIntoBundleFolder()
    {
        var archive = new TestArchive(
        [
            CreateEntry("characters/pikachu/pikachu_00.obj", Encoding.UTF8.GetBytes("o Pikachu\nv 0 0 0\n")),
            CreateEntry("characters/pikachu/pikachu_00_anim_idle.tranm", [(byte)'C', (byte)'H', (byte)'R', (byte)'0', 0x01]),
        ]);

        var source = new TrinityArchiveSource("pkm_model.trpak", archive);
        var indexer = new TrinityBinIndexerService();
        var bundles = indexer.IndexArchives([source]);

        var outputDir = Path.Combine(Path.GetTempPath(), $"trinity-assemble-pass-{Guid.NewGuid():N}");
        var modelsRoot = Path.Combine(outputDir, "models");
        var texturesRoot = Path.Combine(outputDir, "textures");
        var clipsRoot = Path.Combine(outputDir, "clips");

        try
        {
            var service = new TrinityModelAssemblyService();
            var report = service.AssembleBundles(bundles, [source], modelsRoot, texturesRoot, clipsRoot, "obj");

            var model = Assert.Single(report.Models);
            Assert.Equal("completed", model.Status);
            Assert.Equal("characters/pikachu/pikachu_00", model.BundleKey);
            Assert.Equal("pkm_model.trpak/characters/pikachu/pikachu_00/pikachu_00.obj", model.OutputRelativePath);

            var modelPath = Path.Combine(modelsRoot, model.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(modelPath));

            var clip = Assert.Single(report.Clips);
            Assert.Equal("pkm_model.trpak/characters/pikachu/pikachu_00/clips/pikachu_00_anim_idle.tranm", clip.OutputRelativePath);
            var clipPath = Path.Combine(clipsRoot, clip.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(clipPath));
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void AssembleBundles_DecodesNativeTrinityBins_AndExportsObj()
    {
        var archive = new TestArchive(
        [
            CreateEntry("characters/pikachu/pikachu_00.trmdl", BuildTrmdl("Pikachu")),
            CreateEntry("characters/pikachu/pikachu_00.trmsh", BuildTrmsh(vertexCount: 3, indexCount: 3, vertexStride: 32, positionOffset: 0, normalOffset: 12, uvOffset: 24, indexFormat: 2)),
            CreateEntry("characters/pikachu/pikachu_00_bf.trmbf", BuildTrmbf(BuildVertexBuffer(), BuildIndexBuffer([0, 1, 2]))),
        ]);

        var source = new TrinityArchiveSource("pkm_model.trpak", archive);
        var indexer = new TrinityBinIndexerService();
        var bundles = indexer.IndexArchives([source]);

        var outputDir = Path.Combine(Path.GetTempPath(), $"trinity-assemble-native-{Guid.NewGuid():N}");
        var modelsRoot = Path.Combine(outputDir, "models");
        var texturesRoot = Path.Combine(outputDir, "textures");
        var clipsRoot = Path.Combine(outputDir, "clips");

        try
        {
            var service = new TrinityModelAssemblyService();
            var report = service.AssembleBundles(bundles, [source], modelsRoot, texturesRoot, clipsRoot, "obj");

            var model = Assert.Single(report.Models);
            Assert.Equal("completed", model.Status);
            Assert.Equal("characters/pikachu/pikachu_00", model.BundleKey);
            Assert.Equal("obj", model.OutputFormat);
            Assert.Equal("trinity-native-static-mesh", model.ExportKind);
            Assert.Null(model.DetailMessage);
            Assert.Empty(report.Clips);

            var modelPath = Path.Combine(modelsRoot, model.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(modelPath));
            var content = File.ReadAllText(modelPath);
            Assert.Contains("v 0 0 0", content, StringComparison.Ordinal);
            Assert.Contains("f 1/1/1 2/2/2 3/3/3", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void AssembleBundles_WhenNativeDecodeIsMalformed_ReportsPendingWithoutCrash()
    {
        var archive = new TestArchive(
        [
            CreateEntry("characters/pikachu/pikachu_00.trmdl", BuildTrmdl("Pikachu")),
            CreateEntry("characters/pikachu/pikachu_00.trmsh", BuildTrmsh(vertexCount: 3, indexCount: 3, vertexStride: 32, positionOffset: 0, normalOffset: 12, uvOffset: 24, indexFormat: 2)),
            CreateEntry("characters/pikachu/pikachu_00_bf.trmbf", BuildTrmbf(BuildVertexBuffer(), BuildIndexBuffer([0, 1, 99]))),
        ]);

        var source = new TrinityArchiveSource("pkm_model.trpak", archive);
        var indexer = new TrinityBinIndexerService();
        var bundles = indexer.IndexArchives([source]);

        var outputDir = Path.Combine(Path.GetTempPath(), $"trinity-assemble-malformed-{Guid.NewGuid():N}");
        var modelsRoot = Path.Combine(outputDir, "models");
        var texturesRoot = Path.Combine(outputDir, "textures");
        var clipsRoot = Path.Combine(outputDir, "clips");

        try
        {
            var service = new TrinityModelAssemblyService();
            var report = service.AssembleBundles(bundles, [source], modelsRoot, texturesRoot, clipsRoot, "obj");

            var model = Assert.Single(report.Models);
            Assert.Equal("pending_conversion", model.Status);
            Assert.Equal("characters/pikachu/pikachu_00", model.BundleKey);
            Assert.Equal("trinity-native-static-mesh", model.ExportKind);
            Assert.Contains("outside vertex range", model.DetailMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(report.Clips);
            Assert.False(Directory.EnumerateFiles(modelsRoot, "*", SearchOption.AllDirectories).Any());
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void AssembleBundles_TrinityContainerReferences_ResolveModelTextureAndClipOutputs()
    {
        var archive = new TestArchive(
        [
            CreateEntry("characters/pikachu/pikachu_00_mmt.trmmt", BuildContainerPayload(
                "shared/assets/pikachu_body.trmdl",
                "shared/assets/pikachu_body.trmsh",
                "shared/assets/pikachu_body.trmbf",
                "shared/assets/pikachu_diffuse.bntx",
                "shared/assets/pikachu_idle.tranm")),
            CreateEntry("characters/pikachu/pikachu_00_mdt.trmdt", BuildContainerPayload(
                "shared/assets/pikachu_skel.trskl",
                "shared/assets/pikachu_mat.trmtr")),
            CreateEntry("shared/assets/pikachu_body.trmdl", BuildTrmdl("Pikachu", "shared/assets/pikachu_body.trmsh", "shared/assets/pikachu_body.trmbf")),
            CreateEntry("shared/assets/pikachu_body.trmsh", BuildTrmsh(vertexCount: 3, indexCount: 3, vertexStride: 32, positionOffset: 0, normalOffset: 12, uvOffset: 24, indexFormat: 2)),
            CreateEntry("shared/assets/pikachu_body.trmbf", BuildTrmbf(BuildVertexBuffer(), BuildIndexBuffer([0, 1, 2]))),
            CreateEntry("shared/assets/pikachu_diffuse.bntx", [0x42, 0x4E, 0x54, 0x58, 0x01]),
            CreateEntry("shared/assets/pikachu_idle.tranm", [(byte)'C', (byte)'H', (byte)'R', (byte)'0', 0x01]),
        ]);

        var source = new TrinityArchiveSource("container_model.trpak", archive);
        var indexer = new TrinityBinIndexerService();
        var bundles = indexer.IndexArchives([source]);

        var outputDir = Path.Combine(Path.GetTempPath(), $"trinity-assemble-container-{Guid.NewGuid():N}");
        var modelsRoot = Path.Combine(outputDir, "models");
        var texturesRoot = Path.Combine(outputDir, "textures");
        var clipsRoot = Path.Combine(outputDir, "clips");

        try
        {
            var service = new TrinityModelAssemblyService();
            var report = service.AssembleBundles(bundles, [source], modelsRoot, texturesRoot, clipsRoot, "dae");

            var model = Assert.Single(report.Models.Where(item => item.BundleKey == "characters/pikachu/pikachu_00"));
            Assert.Equal("completed", model.Status);
            Assert.Equal("dae", model.OutputFormat);
            Assert.Contains("/characters/pikachu/pikachu_00/", model.OutputRelativePath, StringComparison.Ordinal);

            var texture = Assert.Single(report.Textures.Where(item => item.BundleKey == "characters/pikachu/pikachu_00"));
            Assert.Contains("/characters/pikachu/pikachu_00/textures/", texture.OutputRelativePath, StringComparison.Ordinal);

            var clip = Assert.Single(report.Clips.Where(item => item.BundleKey == "characters/pikachu/pikachu_00"));
            Assert.Contains("/characters/pikachu/pikachu_00/clips/", clip.OutputRelativePath, StringComparison.Ordinal);

            var modelPath = Path.Combine(modelsRoot, model.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var texturePath = Path.Combine(texturesRoot, texture.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var clipPath = Path.Combine(clipsRoot, clip.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(modelPath));
            Assert.True(File.Exists(texturePath));
            Assert.True(File.Exists(clipPath));
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void AssembleBundles_TrinityContainerReferences_ResolveTrinityControlClipFamilies()
    {
        var archive = new TestArchive(
        [
            CreateEntry("characters/pikachu/pikachu_00_mmt.trmmt", BuildContainerPayload(
                "shared/assets/pikachu_body.trmdl",
                "shared/assets/pikachu_body.trmsh",
                "shared/assets/pikachu_body.trmbf",
                "shared/assets/pikachu_ctrl_map.tracm",
                "shared/assets/pikachu_ctrl_state.tracs",
                "shared/assets/pikachu_ctrl_layer.tracl",
                "shared/assets/pikachu_ctrl_route.tracr",
                "shared/assets/pikachu_ctrl_param.tracp",
                "shared/assets/pikachu_fx.traef",
                "shared/assets/pikachu_idle.tranm")),
            CreateEntry("shared/assets/pikachu_body.trmdl", BuildTrmdl("Pikachu", "shared/assets/pikachu_body.trmsh", "shared/assets/pikachu_body.trmbf")),
            CreateEntry("shared/assets/pikachu_body.trmsh", BuildTrmsh(vertexCount: 3, indexCount: 3, vertexStride: 32, positionOffset: 0, normalOffset: 12, uvOffset: 24, indexFormat: 2)),
            CreateEntry("shared/assets/pikachu_body.trmbf", BuildTrmbf(BuildVertexBuffer(), BuildIndexBuffer([0, 1, 2]))),
            CreateEntry("shared/assets/pikachu_ctrl_map.tracm", [0x01, 0x02, 0x03]),
            CreateEntry("shared/assets/pikachu_ctrl_state.tracs", [0x04, 0x05, 0x06]),
            CreateEntry("shared/assets/pikachu_ctrl_layer.tracl", [0x07, 0x08, 0x09]),
            CreateEntry("shared/assets/pikachu_ctrl_route.tracr", [0x0A, 0x0B, 0x0C]),
            CreateEntry("shared/assets/pikachu_ctrl_param.tracp", [0x0D, 0x0E, 0x0F]),
            CreateEntry("shared/assets/pikachu_fx.traef", [0x10, 0x11, 0x12]),
            CreateEntry("shared/assets/pikachu_idle.tranm", [(byte)'C', (byte)'H', (byte)'R', (byte)'0', 0x01]),
        ]);

        var source = new TrinityArchiveSource("container_model.trpak", archive);
        var indexer = new TrinityBinIndexerService();
        var bundles = indexer.IndexArchives([source]);

        var outputDir = Path.Combine(Path.GetTempPath(), $"trinity-assemble-container-control-clips-{Guid.NewGuid():N}");
        var modelsRoot = Path.Combine(outputDir, "models");
        var texturesRoot = Path.Combine(outputDir, "textures");
        var clipsRoot = Path.Combine(outputDir, "clips");

        try
        {
            var service = new TrinityModelAssemblyService();
            var report = service.AssembleBundles(bundles, [source], modelsRoot, texturesRoot, clipsRoot, "obj");

            var model = Assert.Single(report.Models.Where(item => item.BundleKey == "characters/pikachu/pikachu_00"));
            Assert.Equal("completed", model.Status);

            var clips = report.Clips
                .Where(item => item.BundleKey == "characters/pikachu/pikachu_00")
                .OrderBy(item => item.OutputRelativePath, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(7, clips.Count);
            Assert.Contains(clips, item => item.OutputRelativePath.EndsWith("/pikachu_idle.tranm", StringComparison.Ordinal));
            Assert.Contains(clips, item => item.OutputRelativePath.EndsWith("/pikachu_fx.traef", StringComparison.Ordinal));
            Assert.Contains(clips, item => item.OutputRelativePath.EndsWith("/pikachu_ctrl_map.tracm", StringComparison.Ordinal));
            Assert.Contains(clips, item => item.OutputRelativePath.EndsWith("/pikachu_ctrl_state.tracs", StringComparison.Ordinal));
            Assert.Contains(clips, item => item.OutputRelativePath.EndsWith("/pikachu_ctrl_layer.tracl", StringComparison.Ordinal));
            Assert.Contains(clips, item => item.OutputRelativePath.EndsWith("/pikachu_ctrl_route.tracr", StringComparison.Ordinal));
            Assert.Contains(clips, item => item.OutputRelativePath.EndsWith("/pikachu_ctrl_param.tracp", StringComparison.Ordinal));

            foreach (var clip in clips)
            {
                var clipPath = Path.Combine(clipsRoot, clip.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(clipPath));
            }
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void AssembleBundles_WithArmature_DecodesTranmToDaeAndKeepsControlEffectAsRawSidecars()
    {
        var archive = new TestArchive(
        [
            CreateEntry("characters/pikachu/pikachu_00.trmdl", BuildTrmdl("Pikachu")),
            CreateEntry("characters/pikachu/pikachu_00.trmsh", BuildTrmsh(vertexCount: 3, indexCount: 3, vertexStride: 32, positionOffset: 0, normalOffset: 12, uvOffset: 24, indexFormat: 2)),
            CreateEntry("characters/pikachu/pikachu_00_bf.trmbf", BuildTrmbf(BuildVertexBuffer(), BuildIndexBuffer([0, 1, 2]))),
            CreateEntry("characters/pikachu/pikachu_00.trskl", BuildMinimalTrskl("PikachuRoot")),
            CreateEntry("characters/pikachu/pikachu_00_anim_idle.tranm", [(byte)'C', (byte)'H', (byte)'R', (byte)'0', 0x01]),
            CreateEntry("characters/pikachu/pikachu_00_effect_hit.traef", [0x10, 0x11, 0x12]),
        ]);

        var source = new TrinityArchiveSource("pkm_model.trpak", archive);
        var indexer = new TrinityBinIndexerService();
        var bundles = indexer.IndexArchives([source]);

        var outputDir = Path.Combine(Path.GetTempPath(), $"trinity-assemble-clip-dae-{Guid.NewGuid():N}");
        var modelsRoot = Path.Combine(outputDir, "models");
        var texturesRoot = Path.Combine(outputDir, "textures");
        var clipsRoot = Path.Combine(outputDir, "clips");

        try
        {
            var service = new TrinityModelAssemblyService();
            var report = service.AssembleBundles(bundles, [source], modelsRoot, texturesRoot, clipsRoot, "dae");

            var model = Assert.Single(report.Models);
            Assert.Equal("completed", model.Status);

            var clips = report.Clips
                .Where(item => item.BundleKey == "characters/pikachu/pikachu_00")
                .OrderBy(item => item.OutputRelativePath, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(2, clips.Count);

            var daeClip = Assert.Single(clips.Where(item => item.DetectedExtension == ".dae"));
            Assert.Equal("dae-decoded", daeClip.ExportKind);
            Assert.EndsWith("/pikachu_00_anim_idle.dae", daeClip.OutputRelativePath, StringComparison.Ordinal);

            var rawEffect = Assert.Single(clips.Where(item => item.DetectedExtension == ".traef"));
            Assert.Equal("raw", rawEffect.ExportKind);
            Assert.EndsWith("/pikachu_00_effect_hit.traef", rawEffect.OutputRelativePath, StringComparison.Ordinal);

            var daeClipPath = Path.Combine(clipsRoot, daeClip.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var rawEffectPath = Path.Combine(clipsRoot, rawEffect.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(daeClipPath));
            Assert.True(File.Exists(rawEffectPath));

            var daeText = File.ReadAllText(daeClipPath);
            Assert.Contains("<library_animations>", daeText, StringComparison.Ordinal);
            Assert.Contains("<channel", daeText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void AssembleBundles_TrinityContainerWithUnresolvedGeometry_ReportsPendingWithoutCrash()
    {
        var archive = new TestArchive(
        [
            CreateEntry("characters/pikachu/pikachu_00_mmt.trmmt", BuildContainerPayload(
                "missing/pikachu_body.trmdl",
                "missing/pikachu_body.trmsh",
                "missing/pikachu_body.trmbf",
                "shared/assets/pikachu_idle.tranm")),
            CreateEntry("shared/assets/pikachu_idle.tranm", [(byte)'C', (byte)'H', (byte)'R', (byte)'0', 0x01]),
        ]);

        var source = new TrinityArchiveSource("container_model.trpak", archive);
        var indexer = new TrinityBinIndexerService();
        var bundles = indexer.IndexArchives([source]);

        var outputDir = Path.Combine(Path.GetTempPath(), $"trinity-assemble-container-pending-{Guid.NewGuid():N}");
        var modelsRoot = Path.Combine(outputDir, "models");
        var texturesRoot = Path.Combine(outputDir, "textures");
        var clipsRoot = Path.Combine(outputDir, "clips");

        try
        {
            var service = new TrinityModelAssemblyService();
            var report = service.AssembleBundles(bundles, [source], modelsRoot, texturesRoot, clipsRoot, "dae");

            var pending = Assert.Single(report.Models);
            Assert.Equal("pending_conversion", pending.Status);
            Assert.Contains("Unresolved container refs", pending.DetailMessage, StringComparison.Ordinal);
            Assert.Empty(report.Textures);

            var clip = Assert.Single(report.Clips.Where(item => item.BundleKey == "characters/pikachu/pikachu_00"));
            var clipPath = Path.Combine(clipsRoot, clip.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(clipPath));
            Assert.False(Directory.EnumerateFiles(modelsRoot, "*", SearchOption.AllDirectories).Any());
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    private static byte[] BuildTrmdl(string modelName, string meshPath = "characters/pikachu/pikachu_00.trmsh", string bufferPath = "characters/pikachu/pikachu_00_bf.trmbf")
    {
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

    private static byte[] BuildContainerPayload(params string[] references)
    {
        return Encoding.UTF8.GetBytes(string.Join('\n', references));
    }

    private static byte[] BuildTrmsh(int vertexCount, int indexCount, int vertexStride, int positionOffset, int normalOffset, int uvOffset, int indexFormat)
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

    private static byte[] BuildTrmbf(byte[] vertexBuffer, byte[] indexBuffer)
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

        var vertexVectorPos = WriteByteVector(writer, vertexBuffer);
        var indexVectorPos = WriteByteVector(writer, indexBuffer);
        PatchInt32(stream, vertexRelPos, vertexVectorPos - vertexRelPos);
        PatchInt32(stream, indexRelPos, indexVectorPos - indexRelPos);

        PatchInt32(stream, 0, rootTablePos);
        return stream.ToArray();
    }

    private static byte[] BuildMinimalTrskl(string rootBoneName)
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

    private static int WriteByteVector(BinaryWriter writer, byte[] payload)
    {
        var vectorPos = (int)writer.BaseStream.Position;
        writer.Write(payload.Length);
        writer.Write(payload);
        return vectorPos;
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
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
        stream.Position = end;
    }

    private static ArchiveEntry CreateEntry(string fileName, byte[] payload)
    {
        return new ArchiveEntry
        {
            FileName = fileName,
            OpenRead = () => new MemoryStream(payload, writable: false),
        };
    }

    private sealed class TestArchive : IArchiveFile
    {
        private readonly IReadOnlyList<ArchiveEntry> _files;

        public TestArchive(IReadOnlyList<ArchiveEntry> files)
        {
            _files = files;
        }

        public IEnumerable<ArchiveEntry> Files => _files;
    }
}
