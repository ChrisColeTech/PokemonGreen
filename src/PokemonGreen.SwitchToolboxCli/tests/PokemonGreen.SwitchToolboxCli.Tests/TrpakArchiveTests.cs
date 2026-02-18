using PokemonGreen.SwitchToolboxCli.Core.Extraction;
using PokemonGreen.SwitchToolboxCli.Core.Loading;
using PokemonGreen.SwitchToolboxCli.Core.Models;
using PokemonGreen.SwitchToolboxCli.Formats.Archives.Pokemon;
using PokemonGreen.SwitchToolboxCli.Formats.Archives.TRPAK;
using PokemonGreen.SwitchToolboxCli.Formats.Common;
using System.Text;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class TrpakArchiveTests
{
    [Fact]
    public void TrpakFormat_LoadsEntries()
    {
        var payload = BuildTrpak(
        [
            (new byte[] { 1, 2, 3, 4 }, 255),
            (new byte[] { 5, 6, 7, 8 }, 255),
        ]);

        var registry = new FormatRegistry();
        registry.Register(new TrpakFormat());
        var loader = new FileLoader(registry);

        using var stream = new MemoryStream(payload);
        var loaded = loader.Open(stream, "sample.trpak");

        Assert.NotNull(loaded);
        Assert.Equal("TRPAK", loaded?.Format.FormatName);

        var archive = Assert.IsType<TrpakArchive>(loaded?.Value);
        Assert.Equal(2, archive.Files.Count());
    }

    [Fact]
    public void ArchiveWalker_DetectsNestedGarcInsideTrpak()
    {
        var garcLike = new byte[0x20];
        garcLike[0] = (byte)'C';
        garcLike[1] = (byte)'R';
        garcLike[2] = (byte)'A';
        garcLike[3] = (byte)'G';

        var payload = BuildTrpak(
        [
            (garcLike, 255),
        ]);

        var tempPath = Path.Combine(Path.GetTempPath(), $"trpak-{Guid.NewGuid():N}.trpak");
        File.WriteAllBytes(tempPath, payload);

        try
        {
            var registry = new FormatRegistry();
            registry.Register(new GarcFormat());
            registry.Register(new TrpakFormat());

            var walker = new ArchiveWalker(new FileLoader(registry));
            var items = walker.Walk(tempPath).ToList();

            Assert.Contains(items, item => item.Format.FormatName == "TRPAK");
            Assert.Contains(items, item => item.Format.FormatName == "GARC");
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void TrpakFormat_OodleUnavailable_FallsBackToRawPayloadWithoutCrash()
    {
        var previousPath = Environment.GetEnvironmentVariable("PG_OO2CORE_PATH");
        Environment.SetEnvironmentVariable("PG_OO2CORE_PATH", Path.Combine(Path.GetTempPath(), $"missing-oo2core-{Guid.NewGuid():N}.dll"));

        try
        {
            var rawPayload = new byte[] { 0x13, 0x37, 0x42, 0x99 };
            var payload = BuildTrpak(
            [
                (rawPayload, 3),
            ]);

            using var stream = new MemoryStream(payload);
            var archive = TrpakArchive.Parse(stream, sourcePath: null);
            var entry = Assert.Single(archive.Files);

            Assert.EndsWith(".oodle.bin", entry.FileName, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("lazy=true", entry.DetailMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("sample_decode_failed", entry.DetailMessage, StringComparison.OrdinalIgnoreCase);

            using var data = entry.OpenRead();
            using var memory = new MemoryStream();
            data.CopyTo(memory);
            Assert.Equal(rawPayload, memory.ToArray());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PG_OO2CORE_PATH", previousPath);
        }
    }

    [Fact]
    public void TrpakFormat_SniffsTrinityPayloadAndAssignsTrmdlExtension()
    {
        var payload = BuildTrpak(
        [
            (BuildTrmdl("Pikachu"), 255),
        ]);

        using var stream = new MemoryStream(payload);
        var archive = TrpakArchive.Parse(stream, sourcePath: null);
        var entry = Assert.Single(archive.Files);

        Assert.EndsWith(".trmdl", entry.FileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TrpakFormat_SnifferAssignsSpecificExtensions_AndAvoidsWeakTrmdlOverAssignment()
    {
        var payload = BuildTrpak(
        [
            (BuildWeakTrmdlLikePayload("WeakOnlyName"), 255),
            (BuildTrmdl("Pikachu"), 255),
            (BuildTrmsh(vertexCount: 3, indexCount: 3, vertexStride: 32, positionOffset: 0, normalOffset: 12, uvOffset: 24, indexFormat: 2), 255),
            (BuildTrmbf(BuildVertexBuffer(), BuildIndexBuffer([0, 1, 2])), 255),
            (BuildTrskl("Root", "Bone_0"), 255),
            ([ (byte)'T', (byte)'R', (byte)'A', (byte)'N', (byte)'M', 0x01, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00 ], 255),
            ([ (byte)'T', (byte)'R', (byte)'A', (byte)'E', (byte)'F', 0x01, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00 ], 255),
        ]);

        using var stream = new MemoryStream(payload);
        var archive = TrpakArchive.Parse(stream, sourcePath: null);
        var names = archive.Files.Select(file => file.FileName).ToList();

        Assert.Equal(7, names.Count);
        Assert.Contains(names, name => name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".trmdl", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".trmsh", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".trmbf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".trskl", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".tranm", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".traef", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TrpakFormat_HashGuidedTrmdlDependencies_ClassifyGenericBins()
    {
        const string meshPath = "characters/pikachu/pikachu_00.trmsh";
        const string bufferPath = "characters/pikachu/pikachu_00_bf.trmbf";
        const string skeletonPath = "characters/pikachu/pikachu_00_skel.trskl";
        const string materialPath = "characters/pikachu/pikachu_00_mat.trmtr";

        var payload = BuildTrpak(
        [
            (BuildTrmdlWithExtraDependencies("Pikachu", meshPath, bufferPath, skeletonPath, materialPath), (byte)255),
            (new byte[] { 1, 1, 1, 1, 1 }, (byte)255),
            (new byte[] { 2, 2, 2, 2, 2 }, (byte)255),
            (new byte[] { 3, 3, 3, 3, 3 }, (byte)255),
            (new byte[] { 4, 4, 4, 4, 4 }, (byte)255),
        ],
        [
            Fnv1a64.Compute("characters/pikachu/pikachu_00.trmdl"),
            Fnv1a64.Compute(meshPath),
            Fnv1a64.Compute(bufferPath),
            Fnv1a64.Compute(skeletonPath),
            Fnv1a64.Compute(materialPath),
        ]);

        using var stream = new MemoryStream(payload);
        var archive = TrpakArchive.Parse(stream, sourcePath: null);
        var names = archive.Files.Select(file => file.FileName).ToList();

        Assert.Equal(5, names.Count);
        Assert.Contains(names, name => name.EndsWith(".trmdl", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".trmsh", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".trmbf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".trskl", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".trmtr", StringComparison.OrdinalIgnoreCase));

        var typedHints = archive.Files
            .Where(entry => !entry.FileName.EndsWith(".trmdl", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.ExtensionHint)
            .ToList();
        Assert.DoesNotContain(typedHints, hint => hint is ".bin");
        Assert.DoesNotContain(archive.Files, entry => entry.RootHash is null);

        var indexer = new TrinityBinIndexerService();
        var bundles = indexer.IndexArchives([new TrinityArchiveSource("synthetic.trpak", archive)]);
        var typedRoles = bundles.SelectMany(bundle => bundle.Entries).Select(entry => entry.Role).ToList();
        Assert.Contains(TrinityEntryRole.Model, typedRoles);
        Assert.Contains(TrinityEntryRole.Mesh, typedRoles);
        Assert.Contains(TrinityEntryRole.BlendShape, typedRoles);
        Assert.Contains(TrinityEntryRole.Skeleton, typedRoles);
        Assert.Contains(TrinityEntryRole.Material, typedRoles);
    }

    [Fact]
    public void TrpakFormat_HashGuidedContainerReferences_ClassifyGenericBinsFromTrmmt()
    {
        const string containerPath = "characters/pikachu/pikachu_00_mmt.trmmt";
        const string blendShapePath = "characters/pikachu/pikachu_00_bf.trmbf";
        const string materialPath = "characters/pikachu/pikachu_00_mat.trmtr";
        const string animPath = "characters/pikachu/pikachu_00_anim_idle.tranm";
        const string controlMapPath = "characters/pikachu/pikachu_00_ctrl_map.tracm";
        const string controlStatePath = "characters/pikachu/pikachu_00_ctrl_state.tracs";
        const string controlLayerPath = "characters/pikachu/pikachu_00_ctrl_layer.tracl";
        const string controlRoutePath = "characters/pikachu/pikachu_00_ctrl_route.tracr";
        const string controlParamPath = "characters/pikachu/pikachu_00_ctrl_param.tracp";

        var payload = BuildTrpak(
        [
            (BuildContainerPayload(blendShapePath, materialPath, animPath, controlMapPath, controlStatePath, controlLayerPath, controlRoutePath, controlParamPath), (byte)255),
            (new byte[] { 0xAA, 0xBB, 0xCC }, (byte)255),
            (new byte[] { 0x11, 0x22, 0x33 }, (byte)255),
            (new byte[] { 0x44, 0x55, 0x66 }, (byte)255),
            (new byte[] { 0x47, 0x48, 0x49 }, (byte)255),
            (new byte[] { 0x51, 0x52, 0x53 }, (byte)255),
            (new byte[] { 0x61, 0x62, 0x63 }, (byte)255),
            (new byte[] { 0x71, 0x72, 0x73 }, (byte)255),
            (new byte[] { 0x81, 0x82, 0x83 }, (byte)255),
        ],
        [
            Fnv1a64.Compute(containerPath),
            Fnv1a64.Compute(blendShapePath),
            Fnv1a64.Compute(materialPath),
            Fnv1a64.Compute(animPath),
            Fnv1a64.Compute(controlMapPath),
            Fnv1a64.Compute(controlStatePath),
            Fnv1a64.Compute(controlLayerPath),
            Fnv1a64.Compute(controlRoutePath),
            Fnv1a64.Compute(controlParamPath),
        ]);

        using var stream = new MemoryStream(payload);
        var archive = TrpakArchive.Parse(stream, sourcePath: null);
        var names = archive.Files.Select(file => file.FileName).ToList();

        Assert.Equal(9, names.Count);
        Assert.Contains(names, name => name.EndsWith(".trmmt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".trmbf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".trmtr", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".tranm", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".tracm", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".tracs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".tracl", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".tracr", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".tracp", StringComparison.OrdinalIgnoreCase));

        var indexer = new TrinityBinIndexerService();
        var bundles = indexer.IndexArchives([new TrinityArchiveSource("synthetic.trpak", archive)]);
        var clipEntries = bundles
            .SelectMany(bundle => bundle.Entries)
            .Where(entry => entry.Role is TrinityEntryRole.AnimationClip or TrinityEntryRole.EffectClip)
            .Select(entry => entry.Extension)
            .ToList();

        Assert.Contains(".tranm", clipEntries);
        Assert.Contains(".tracm", clipEntries);
        Assert.Contains(".tracs", clipEntries);
        Assert.Contains(".tracl", clipEntries);
        Assert.Contains(".tracr", clipEntries);
        Assert.Contains(".tracp", clipEntries);
    }

    [Fact]
    public void TrpakFormat_HashGuidedContainerReferences_UnmatchedReferencesAreIgnoredSafely()
    {
        const string containerPath = "characters/pikachu/pikachu_00_mmt.trmmt";
        const string blendShapePath = "characters/pikachu/pikachu_00_bf.trmbf";
        const string unmatchedEffectPath = "characters/pikachu/pikachu_00_effect_hit.traef";

        var payload = BuildTrpak(
        [
            (BuildContainerPayload(blendShapePath, unmatchedEffectPath), (byte)255),
            (new byte[] { 0xAB, 0xCD, 0xEF }, (byte)255),
            (new byte[] { 0x10, 0x20, 0x30 }, (byte)255),
        ],
        [
            Fnv1a64.Compute(containerPath),
            Fnv1a64.Compute(blendShapePath),
            Fnv1a64.Compute("characters/pikachu/unmatched_payload.bin"),
        ]);

        using var stream = new MemoryStream(payload);
        var archive = TrpakArchive.Parse(stream, sourcePath: null);
        var names = archive.Files.Select(file => file.FileName).ToList();

        Assert.Equal(3, names.Count);
        Assert.Contains(names, name => name.EndsWith(".trmmt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".trmbf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.EndsWith(".traef", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TrpakFormat_HashGuidedDependencies_NoMatchFallsBackToBin()
    {
        var payload = BuildTrpak(
        [
            (BuildTrmdl("Pikachu"), (byte)255),
            (new byte[] { 9, 8, 7, 6, 5 }, (byte)255),
        ],
        [
            Fnv1a64.Compute("characters/pikachu/pikachu_00.trmdl"),
            Fnv1a64.Compute("characters/pikachu/unrelated_payload.bin"),
        ]);

        using var stream = new MemoryStream(payload);
        var archive = TrpakArchive.Parse(stream, sourcePath: null);
        var names = archive.Files.Select(file => file.FileName).ToList();

        Assert.Equal(2, names.Count);
        Assert.Contains(names, name => name.EndsWith(".trmdl", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, name => name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.EndsWith(".trmsh", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.EndsWith(".trmbf", StringComparison.OrdinalIgnoreCase));
    }

    private static byte[] BuildTrpak(IReadOnlyList<(byte[] Data, byte Compression)> files, IReadOnlyList<ulong>? rootHashes = null)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(0); // root table offset placeholder

        var rootVtablePos = (int)stream.Position;
        writer.Write((ushort)8);  // vtable size
        writer.Write((ushort)12); // object size
        writer.Write((ushort)4);  // hashes field offset
        writer.Write((ushort)8);  // files field offset

        var rootTablePos = (int)stream.Position;
        writer.Write(rootTablePos - rootVtablePos); // soffset to vtable
        var hashesRelPos = (int)stream.Position;
        writer.Write(0);
        var filesRelPos = (int)stream.Position;
        writer.Write(0);

        var hashesVectorPos = (int)stream.Position;
        writer.Write(files.Count);
        for (var i = 0; i < files.Count; i++)
        {
            var hash = rootHashes is not null && i < rootHashes.Count
                ? rootHashes[i]
                : (ulong)(0x1000 + i);
            writer.Write(hash);
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
            writer.Write((ushort)14); // vtable size
            writer.Write((ushort)12); // object size
            writer.Write((ushort)0);  // unused
            writer.Write((ushort)4);  // compression at +4
            writer.Write((ushort)0);  // unk1
            writer.Write((ushort)0);  // decompressed size
            writer.Write((ushort)8);  // data vector offset at +8

            var fileTablePos = (int)stream.Position;
            writer.Write(fileTablePos - fileVtablePos); // soffset
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

    private static byte[] BuildTrmdl(string modelName)
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
        var meshPathPos = WriteString(writer, "characters/pikachu/pikachu_00.trmsh");
        var bufferPathPos = WriteString(writer, "characters/pikachu/pikachu_00_bf.trmbf");
        PatchInt32(stream, modelNameRelPos, modelNamePos - modelNameRelPos);
        PatchInt32(stream, meshPathRelPos, meshPathPos - meshPathRelPos);
        PatchInt32(stream, bufferPathRelPos, bufferPathPos - bufferPathRelPos);
        PatchInt32(stream, 0, rootTablePos);
        return stream.ToArray();
    }

    private static byte[] BuildTrmdlWithExtraDependencies(
        string modelName,
        string meshPath,
        string bufferPath,
        string skeletonPath,
        string materialPath)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(0);

        var rootVtablePos = (int)stream.Position;
        writer.Write((ushort)12);
        writer.Write((ushort)20);
        writer.Write((ushort)4);
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)16);

        var rootTablePos = (int)stream.Position;
        writer.Write(rootTablePos - rootVtablePos);
        var modelNameRelPos = (int)stream.Position;
        writer.Write(0);
        var meshRefsRelPos = (int)stream.Position;
        writer.Write(0);
        var skeletonRefsRelPos = (int)stream.Position;
        writer.Write(0);
        var materialRefsRelPos = (int)stream.Position;
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

        var skeletonVectorPos = WriteStringVector(writer, skeletonPath);
        PatchInt32(stream, skeletonRefsRelPos, skeletonVectorPos - skeletonRefsRelPos);

        var materialVectorPos = WriteStringVector(writer, materialPath);
        PatchInt32(stream, materialRefsRelPos, materialVectorPos - materialRefsRelPos);

        var modelNamePos = WriteString(writer, modelName);
        var meshPathPos = WriteString(writer, meshPath);
        var bufferPathPos = WriteString(writer, bufferPath);
        PatchInt32(stream, modelNameRelPos, modelNamePos - modelNameRelPos);
        PatchInt32(stream, meshPathRelPos, meshPathPos - meshPathRelPos);
        PatchInt32(stream, bufferPathRelPos, bufferPathPos - bufferPathRelPos);
        PatchInt32(stream, 0, rootTablePos);
        return stream.ToArray();
    }

    private static byte[] BuildWeakTrmdlLikePayload(string modelName)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(0);

        var rootVtablePos = (int)stream.Position;
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)4);
        writer.Write((ushort)0);

        var rootTablePos = (int)stream.Position;
        writer.Write(rootTablePos - rootVtablePos);
        var modelNameRelPos = (int)stream.Position;
        writer.Write(0);
        writer.Write(0);

        var modelNamePos = WriteString(writer, modelName);
        PatchInt32(stream, modelNameRelPos, modelNamePos - modelNameRelPos);
        PatchInt32(stream, 0, rootTablePos);
        return stream.ToArray();
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

    private static byte[] BuildTrskl(string rootName, string firstBoneName)
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
        var rootNameRelPos = (int)stream.Position;
        writer.Write(0);
        var bonesRelPos = (int)stream.Position;
        writer.Write(0);

        var bonesVectorPos = (int)stream.Position;
        writer.Write(1);
        var boneSlotPos = (int)stream.Position;
        writer.Write(0);
        PatchInt32(stream, bonesRelPos, bonesVectorPos - bonesRelPos);

        var boneVtablePos = (int)stream.Position;
        writer.Write((ushort)8);
        writer.Write((ushort)12);
        writer.Write((ushort)4);
        writer.Write((ushort)8);

        var boneTablePos = (int)stream.Position;
        writer.Write(boneTablePos - boneVtablePos);
        var boneNameRelPos = (int)stream.Position;
        writer.Write(0);
        writer.Write(-1);
        PatchInt32(stream, boneSlotPos, boneTablePos - boneSlotPos);

        var rootNamePos = WriteString(writer, rootName);
        var boneNamePos = WriteString(writer, firstBoneName);
        PatchInt32(stream, rootNameRelPos, rootNamePos - rootNameRelPos);
        PatchInt32(stream, boneNameRelPos, boneNamePos - boneNameRelPos);
        PatchInt32(stream, 0, rootTablePos);
        return stream.ToArray();
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

    private static byte[] BuildContainerPayload(params string[] references)
    {
        var text = string.Join("\0", references) + "\0";
        var prefix = new byte[] { 0x00, 0x01, 0xFF, 0x00 };
        var body = Encoding.UTF8.GetBytes(text);
        var payload = new byte[prefix.Length + body.Length];
        Buffer.BlockCopy(prefix, 0, payload, 0, prefix.Length);
        Buffer.BlockCopy(body, 0, payload, prefix.Length, body.Length);
        return payload;
    }

    private static void PatchInt32(Stream stream, int position, int value)
    {
        var end = stream.Position;
        stream.Position = position;
        using var patchWriter = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        patchWriter.Write(value);
        stream.Position = end;
    }
}
