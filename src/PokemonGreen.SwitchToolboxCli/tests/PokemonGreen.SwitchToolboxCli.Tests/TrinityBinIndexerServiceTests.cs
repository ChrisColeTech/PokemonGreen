using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Core.Extraction;
using PokemonGreen.SwitchToolboxCli.Core.Models;
using System.Text;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class TrinityBinIndexerServiceTests
{
    [Fact]
    public void IndexArchives_GroupsByLogicalModelKey_AndClassifiesEntryRoles()
    {
        var archive = new FakeArchiveFile(
        [
            "characters/pikachu/pikachu_00.trmdl",
            "characters/pikachu/pikachu_00_mesh.trmsh",
            "characters/pikachu/pikachu_00_skel.trskl",
            "characters/pikachu/pikachu_00_material.trmtr",
            "characters/pikachu/pikachu_00_bf.trmbf",
            "characters/pikachu/pikachu_00_anim_idle.tranm",
            "characters/pikachu/pikachu_00_effect_hit.traef",
            "characters/pikachu/pikachu_00_ctrl_map.tracm",
            "characters/pikachu/pikachu_00_ctrl_state.tracs",
            "characters/pikachu/pikachu_00_ctrl_layer.tracl",
            "characters/pikachu/pikachu_00_ctrl_route.tracr",
            "characters/pikachu/pikachu_00_ctrl_param.tracp",
            "characters/pikachu/pikachu_00.obj",
            "characters/pikachu/pikachu_01.trmdl",
        ]);

        var service = new TrinityBinIndexerService();
        var bundles = service.IndexArchives(
        [
            new TrinityArchiveSource("bundle.trpak", archive),
        ]);

        Assert.Equal(2, bundles.Count);

        var primary = Assert.Single(bundles.Where(bundle => bundle.LogicalModelKey == "characters/pikachu/pikachu_00"));
        Assert.Equal(13, primary.Entries.Count);
        Assert.Contains(primary.Entries, entry => entry.Role == TrinityEntryRole.DirectModel && entry.Extension == ".obj");
        Assert.Contains(primary.Entries, entry => entry.Role == TrinityEntryRole.Model && entry.Extension == ".trmdl");
        Assert.Contains(primary.Entries, entry => entry.Role == TrinityEntryRole.Mesh && entry.Extension == ".trmsh");
        Assert.Contains(primary.Entries, entry => entry.Role == TrinityEntryRole.Skeleton && entry.Extension == ".trskl");
        Assert.Contains(primary.Entries, entry => entry.Role == TrinityEntryRole.Material && entry.Extension == ".trmtr");
        Assert.Contains(primary.Entries, entry => entry.Role == TrinityEntryRole.BlendShape && entry.Extension == ".trmbf");
        Assert.Contains(primary.Entries, entry => entry.Role == TrinityEntryRole.AnimationClip && entry.Extension == ".tranm");
        Assert.Contains(primary.Entries, entry => entry.Role == TrinityEntryRole.EffectClip && entry.Extension == ".traef");
        Assert.Contains(primary.Entries, entry => entry.Role == TrinityEntryRole.AnimationClip && entry.Extension == ".tracm");
        Assert.Contains(primary.Entries, entry => entry.Role == TrinityEntryRole.AnimationClip && entry.Extension == ".tracs");
        Assert.Contains(primary.Entries, entry => entry.Role == TrinityEntryRole.AnimationClip && entry.Extension == ".tracl");
        Assert.Contains(primary.Entries, entry => entry.Role == TrinityEntryRole.AnimationClip && entry.Extension == ".tracr");
        Assert.Contains(primary.Entries, entry => entry.Role == TrinityEntryRole.AnimationClip && entry.Extension == ".tracp");
        Assert.Equal(7, primary.ClipReferences.Count);

        var secondary = Assert.Single(bundles.Where(bundle => bundle.LogicalModelKey == "characters/pikachu/pikachu_01"));
        Assert.Single(secondary.Entries);
        Assert.Empty(secondary.ClipReferences);
    }

    [Fact]
    public void IndexArchives_IgnoresUnsupportedExtensions_AndIsDeterministic()
    {
        var archive = new FakeArchiveFile(
        [
            "B/chara_01_unknown.bin",
            "a/chara_02.trmdl",
            "a/chara_02_anim_loop.tranm",
            "a/chara_02_mesh_lod0.trmsh",
        ]);

        var service = new TrinityBinIndexerService();
        var bundles = service.IndexArchives(
        [
            new TrinityArchiveSource("z.trpak", archive),
        ]);

        var bundle = Assert.Single(bundles);
        Assert.Equal("a/chara_02", bundle.LogicalModelKey);
        Assert.Equal(3, bundle.Entries.Count);
        Assert.Equal(
        [
            "a/chara_02.trmdl",
            "a/chara_02_anim_loop.tranm",
            "a/chara_02_mesh_lod0.trmsh",
        ],
            bundle.Entries.Select(entry => entry.EntryPath).ToList());
    }

    [Fact]
    public void IndexArchives_GenericBinPayloads_AreSniffedAndGroupedAsTrinityBundle()
    {
        var archive = new FakeArchiveFile(
        [
            ("characters/pikachu/file_000.bin", BuildTrmdl("Pikachu")),
            ("characters/pikachu/file_001.bin", BuildTrmsh(vertexCount: 3, indexCount: 3, vertexStride: 32, positionOffset: 0, normalOffset: 12, uvOffset: 24, indexFormat: 2)),
            ("characters/pikachu/file_002.bin", BuildTrmbf(BuildVertexBuffer(), BuildIndexBuffer([0, 1, 2]))),
        ]);

        var service = new TrinityBinIndexerService();
        var bundles = service.IndexArchives(
        [
            new TrinityArchiveSource("generic.trpak", archive),
        ]);

        var bundle = Assert.Single(bundles);
        Assert.Equal("characters/pikachu/trinity_generic", bundle.LogicalModelKey);
        Assert.Equal(3, bundle.Entries.Count);
        Assert.Contains(bundle.Entries, entry => entry.Role == TrinityEntryRole.Model && entry.Extension == ".trmdl");
        Assert.Contains(bundle.Entries, entry => entry.Role == TrinityEntryRole.Mesh && entry.Extension == ".trmsh");
        Assert.Contains(bundle.Entries, entry => entry.Role == TrinityEntryRole.BlendShape && entry.Extension == ".trmbf");
    }

    [Fact]
    public void IndexArchives_TrinityContainerRoots_AreClassifiedAsModelContainers()
    {
        var archive = new FakeArchiveFile(
        [
            "characters/pikachu/pikachu_00_mmt.trmmt",
            "characters/pikachu/pikachu_00_mdt.trmdt",
        ]);

        var service = new TrinityBinIndexerService();
        var bundles = service.IndexArchives(
        [
            new TrinityArchiveSource("container.trpak", archive),
        ]);

        var bundle = Assert.Single(bundles);
        Assert.Equal("characters/pikachu/pikachu_00", bundle.LogicalModelKey);
        Assert.Equal(2, bundle.Entries.Count);
        Assert.All(bundle.Entries, entry => Assert.Equal(TrinityEntryRole.ModelContainer, entry.Role));
    }

    private sealed class FakeArchiveFile : IArchiveFile
    {
        public FakeArchiveFile(IReadOnlyList<string> names)
        {
            Files = names.Select(name => new ArchiveEntry
            {
                FileName = name,
                OpenRead = static () => new MemoryStream([0x00], writable: false),
            }).ToList();
        }

        public FakeArchiveFile(IReadOnlyList<(string Name, byte[] Payload)> entries)
        {
            Files = entries.Select(item => new ArchiveEntry
            {
                FileName = item.Name,
                OpenRead = () => new MemoryStream(item.Payload, writable: false),
            }).ToList();
        }

        public IEnumerable<ArchiveEntry> Files { get; }
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

    private static void PatchInt32(Stream stream, int position, int value)
    {
        var end = stream.Position;
        stream.Position = position;
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
        stream.Position = end;
    }
}
