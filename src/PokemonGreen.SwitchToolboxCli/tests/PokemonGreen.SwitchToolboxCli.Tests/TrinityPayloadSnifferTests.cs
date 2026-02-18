using System.Text;
using PokemonGreen.SwitchToolboxCli.Core.Extraction;
using PokemonGreen.SwitchToolboxCli.Core.Models;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class TrinityPayloadSnifferTests
{
    private readonly TrinityPayloadSniffer _sniffer = new();

    [Fact]
    public void TryClassify_RejectsWeakTrmdlShapeWithoutMeshReferences()
    {
        var payload = BuildWeakTrmdlLikePayload("Pikachu");

        var result = _sniffer.TryClassify(payload, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryClassify_DetectsTrmdl_WhenMeshReferencesAreSane()
    {
        var payload = BuildTrmdl("Pikachu");

        var result = _sniffer.TryClassify(payload, out var classification);

        Assert.True(result);
        Assert.Equal(".trmdl", classification.Extension);
        Assert.Equal(TrinitySniffConfidence.High, classification.Confidence);
    }

    [Fact]
    public void TryClassify_DetectsTrmsh()
    {
        var payload = BuildTrmsh(vertexCount: 3, indexCount: 3, vertexStride: 32, positionOffset: 0, normalOffset: 12, uvOffset: 24, indexFormat: 2);

        var result = _sniffer.TryClassify(payload, out var classification);

        Assert.True(result);
        Assert.Equal(".trmsh", classification.Extension);
    }

    [Fact]
    public void TryClassify_DetectsTrmbf()
    {
        var payload = BuildTrmbf(BuildVertexBuffer(), BuildIndexBuffer([0, 1, 2]));

        var result = _sniffer.TryClassify(payload, out var classification);

        Assert.True(result);
        Assert.Equal(".trmbf", classification.Extension);
    }

    [Fact]
    public void TryClassify_DetectsTrskl()
    {
        var payload = BuildTrskl("Root", "Bone_0");

        var result = _sniffer.TryClassify(payload, out var classification);

        Assert.True(result);
        Assert.Equal(".trskl", classification.Extension);
    }

    [Fact]
    public void TryClassify_DetectsTranm()
    {
        var payload = new byte[] { (byte)'T', (byte)'R', (byte)'A', (byte)'N', (byte)'M', 0x01, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00 };

        var result = _sniffer.TryClassify(payload, out var classification);

        Assert.True(result);
        Assert.Equal(".tranm", classification.Extension);
    }

    [Fact]
    public void TryClassify_DetectsTraef()
    {
        var payload = new byte[] { (byte)'T', (byte)'R', (byte)'A', (byte)'E', (byte)'F', 0x01, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00 };

        var result = _sniffer.TryClassify(payload, out var classification);

        Assert.True(result);
        Assert.Equal(".traef", classification.Extension);
    }

    [Fact]
    public void TryClassify_DetectsTrinityContainerPackageByReferenceScan()
    {
        var payload = Encoding.UTF8.GetBytes("shared/pikachu_body.trmdl\nshared/pikachu_body.trmsh\nshared/pikachu_body.trmbf\nshared/pikachu_idle.tranm");

        var result = _sniffer.TryClassify(payload, out var classification);

        Assert.True(result);
        Assert.Equal(".trmmt", classification.Extension);
        Assert.Equal(TrinityEntryRole.ModelContainer, classification.Role);
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

    private static void PatchInt32(Stream stream, int position, int value)
    {
        var end = stream.Position;
        stream.Position = position;
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(value);
        stream.Position = end;
    }
}
