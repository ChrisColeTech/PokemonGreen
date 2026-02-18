using System.Buffers.Binary;
using FlatSharp;
using PokemonGreen.SwitchToolboxCli.Core.Models;

namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Models.Trinity;

public sealed class TrinityStaticMeshDecoder
{
    private static readonly FlatBufferSerializer Serializer = new();

    public bool TryDecode(
        byte[] trmdlPayload,
        byte[] trmshPayload,
        byte[] trmbfPayload,
        string fallbackModelName,
        out ExportModel model,
        out string detail)
    {
        model = null!;
        detail = string.Empty;

        if (!TryParse(trmdlPayload, "TRMDL", out TrinityModelRoot trmdl, out detail))
        {
            return false;
        }

        if (TryDecodeModern(trmshPayload, trmbfPayload, trmdl, fallbackModelName, out model, out detail))
        {
            return true;
        }

        return TryDecodeLegacy(trmshPayload, trmbfPayload, trmdl, fallbackModelName, out model, out detail);
    }

    public bool TryReadModelReferences(byte[] trmdlPayload, out TrinityModelRoot modelRoot)
    {
        if (TryParse(trmdlPayload, "TRMDL", out TrinityModelRoot parsed, out _))
        {
            modelRoot = parsed;
            return true;
        }

        modelRoot = null!;
        return false;
    }

    private static bool TryDecodeModern(
        byte[] trmshPayload,
        byte[] trmbfPayload,
        TrinityModelRoot trmdl,
        string fallbackModelName,
        out ExportModel model,
        out string detail)
    {
        model = null!;
        detail = string.Empty;

        if (!TryParse(trmshPayload, "TRMSH", out TrinityMeshRoot trmsh, out detail) ||
            !TryParse(trmbfPayload, "TRMBF", out TrinityBufferRoot trmbf, out detail))
        {
            return false;
        }

        var meshes = trmsh.Meshes;
        if (meshes is null || meshes.Count == 0)
        {
            detail = "TRMSH decode failed: mesh vector is empty.";
            return false;
        }

        var meshBuffers = trmbf.MeshBuffers;
        if (meshBuffers is null || meshBuffers.Count == 0)
        {
            detail = "TRMBF decode failed: mesh buffer vector is empty.";
            return false;
        }

        var decodeErrors = new List<string>();
        for (var meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
        {
            var mesh = meshes[meshIndex];
            if (mesh is null)
            {
                continue;
            }

            var parts = mesh.Parts;
            if (parts is null || parts.Count == 0)
            {
                decodeErrors.Add($"mesh[{meshIndex}] has no parts");
                continue;
            }

            if (meshIndex < 0 || meshIndex >= meshBuffers.Count)
            {
                decodeErrors.Add($"mesh[{meshIndex}] has no matching TRMBF buffer set");
                continue;
            }

            var modelBuffer = meshBuffers[meshIndex];
            if (modelBuffer?.VertexBuffers is null || modelBuffer.IndexBuffers is null)
            {
                decodeErrors.Add($"mesh[{meshIndex}] has missing vertex/index buffers");
                continue;
            }

            var indexBufferBytes = modelBuffer.IndexBuffers
                .Select(item => item?.Bytes)
                .FirstOrDefault(bytes => bytes is { Count: > 0 });
            if (indexBufferBytes is null)
            {
                decodeErrors.Add($"mesh[{meshIndex}] has no non-empty index buffer");
                continue;
            }

            var indexBuffer = indexBufferBytes as byte[] ?? indexBufferBytes.ToArray();

            for (var partIndex = 0; partIndex < parts.Count; partIndex++)
            {
                var part = parts[partIndex];
                if (part is null)
                {
                    continue;
                }

                if (!TryDecodePart(mesh, part, modelBuffer, indexBuffer, out var exportMesh, out var partError))
                {
                    decodeErrors.Add($"mesh[{meshIndex}] part[{partIndex}] {partError}");
                    continue;
                }

                var modelName = !string.IsNullOrWhiteSpace(trmdl.ModelName)
                    ? trmdl.ModelName
                    : fallbackModelName;

                model = new ExportModel
                {
                    ModelName = modelName,
                    Meshes = [exportMesh],
                };

                detail = string.Empty;
                return true;
            }
        }

        detail = decodeErrors.Count == 0
            ? "TRMSH decode failed: no valid mesh part candidates were found."
            : "TRMSH decode failed: " + string.Join("; ", decodeErrors.Take(6));
        return false;
    }

    private static bool TryDecodePart(
        TrinityMesh mesh,
        TrinityMeshPart part,
        TrinityModelBuffer modelBuffer,
        byte[] indexBuffer,
        out ExportMesh exportMesh,
        out string detail)
    {
        exportMesh = null!;
        detail = string.Empty;

        var declarations = mesh.VertexDeclarations;
        if (declarations is null || declarations.Count == 0)
        {
            detail = "has no vertex declarations.";
            return false;
        }

        var declarationIndex = part.VertexDeclarationIndex;
        if (declarationIndex < 0 || declarationIndex >= declarations.Count)
        {
            declarationIndex = 0;
        }

        var declaration = declarations[declarationIndex];
        if (declaration?.VertexElements is null || declaration.VertexElements.Count == 0)
        {
            detail = "has an empty vertex element list.";
            return false;
        }

        var positionElement = declaration.VertexElements
            .FirstOrDefault(element => element?.VertexUsage == TrinityVertexUsage.Position);
        if (positionElement is null)
        {
            detail = "is missing POSITION vertex element.";
            return false;
        }

        if (!TryGetElementBufferAndStride(positionElement, declaration, modelBuffer, out var positionBuffer, out var positionStride, out var positionError))
        {
            detail = positionError;
            return false;
        }

        var vertexCount = positionBuffer.Length / positionStride;
        if (vertexCount <= 0)
        {
            detail = "has no vertices in POSITION stream.";
            return false;
        }

        var normalElement = declaration.VertexElements
            .FirstOrDefault(element => element?.VertexUsage == TrinityVertexUsage.Normal);
        byte[] normalBuffer = Array.Empty<byte>();
        var normalStride = 0;
        var hasNormals = normalElement is not null &&
                         TryGetElementBufferAndStride(normalElement, declaration, modelBuffer, out normalBuffer, out normalStride, out _);

        var uvElement = declaration.VertexElements
            .FirstOrDefault(element => element?.VertexUsage == TrinityVertexUsage.TexCoord);
        byte[] uvBuffer = Array.Empty<byte>();
        var uvStride = 0;
        var hasUvs = uvElement is not null &&
                     TryGetElementBufferAndStride(uvElement, declaration, modelBuffer, out uvBuffer, out uvStride, out _);

        var vertices = new List<ExportVector3>(vertexCount);
        var normals = new List<ExportVector3>(vertexCount);
        var uvs = new List<ExportVector2>(vertexCount);

        for (var i = 0; i < vertexCount; i++)
        {
            var posOffset = (i * positionStride) + positionElement.VertexElementOffset;
            if (!TryReadVector3(positionBuffer, posOffset, positionElement.VertexFormat, isNormal: false, out var position))
            {
                detail = $"could not decode POSITION for vertex {i}.";
                return false;
            }

            vertices.Add(position);

            if (hasNormals && normalElement is not null)
            {
                var nOffset = (i * normalStride) + normalElement.VertexElementOffset;
                if (TryReadVector3(normalBuffer, nOffset, normalElement.VertexFormat, isNormal: true, out var normal))
                {
                    normals.Add(normal);
                }
            }

            if (hasUvs && uvElement is not null)
            {
                var uvOffset = (i * uvStride) + uvElement.VertexElementOffset;
                if (TryReadVector2(uvBuffer, uvOffset, uvElement.VertexFormat, out var uv))
                {
                    uvs.Add(uv);
                }
            }
        }

        if (normals.Count != vertexCount)
        {
            normals.Clear();
        }

        if (uvs.Count != vertexCount)
        {
            uvs.Clear();
        }

        var indexElementSize = mesh.IndexType switch
        {
            TrinityIndexFormat.Byte => 1,
            TrinityIndexFormat.Short => 2,
            TrinityIndexFormat.Int => 4,
            _ => 0,
        };
        if (indexElementSize == 0)
        {
            detail = $"uses unsupported index format {(int)mesh.IndexType}.";
            return false;
        }

        var indexStart = Math.Max(0, part.IndexOffset);
        var indexCount = part.IndexCount;
        if (indexCount <= 0)
        {
            detail = "has no indices.";
            return false;
        }

        if ((indexCount % 3) != 0)
        {
            detail = $"index count {indexCount} is not a triangle list.";
            return false;
        }

        var requiredBytes = (indexStart + indexCount) * indexElementSize;
        if (requiredBytes > indexBuffer.Length)
        {
            detail = $"index buffer is too small for count={indexCount}, start={indexStart}.";
            return false;
        }

        var indices = new List<int>(indexCount);
        for (var i = 0; i < indexCount; i++)
        {
            var offset = (indexStart + i) * indexElementSize;
            var index = indexElementSize switch
            {
                1 => indexBuffer[offset],
                2 => BinaryPrimitives.ReadUInt16LittleEndian(indexBuffer.AsSpan(offset, 2)),
                4 => BinaryPrimitives.ReadInt32LittleEndian(indexBuffer.AsSpan(offset, 4)),
                _ => -1,
            };

            if (index < 0 || index >= vertexCount)
            {
                detail = $"index {index} is outside vertex range 0..{vertexCount - 1}.";
                return false;
            }

            indices.Add(index);
        }

        var meshName = !string.IsNullOrWhiteSpace(mesh.Name)
            ? mesh.Name
            : "mesh_000";

        exportMesh = new ExportMesh
        {
            MeshName = meshName,
            Vertices = vertices,
            Normals = normals,
            Uvs = uvs,
            Indices = indices,
        };

        return true;
    }

    private static bool TryGetElementBufferAndStride(
        TrinityVertexElement element,
        TrinityVertexDeclaration declaration,
        TrinityModelBuffer modelBuffer,
        out byte[] buffer,
        out int stride,
        out string detail)
    {
        buffer = Array.Empty<byte>();
        stride = 0;
        detail = string.Empty;

        var elementSizes = declaration.VertexElementSizes;
        if (elementSizes is null || elementSizes.Count == 0)
        {
            detail = "is missing vertex element size table.";
            return false;
        }

        var sizeIndex = element.VertexElementSizeIndex;
        if (sizeIndex < 0 || sizeIndex >= elementSizes.Count)
        {
            detail = $"has invalid element size index {sizeIndex}.";
            return false;
        }

        var sizeEntry = elementSizes[sizeIndex];
        stride = sizeEntry?.ElementSize ?? 0;
        if (stride <= 0 || stride > 2048)
        {
            detail = $"has invalid stride {stride}.";
            return false;
        }

        var layer = element.VertexElementLayer;
        if (modelBuffer.VertexBuffers is null || layer < 0 || layer >= modelBuffer.VertexBuffers.Count)
        {
            detail = $"references missing vertex buffer layer {layer}.";
            return false;
        }

        var layerBuffer = modelBuffer.VertexBuffers[layer]?.Bytes;
        if (layerBuffer is null || layerBuffer.Count == 0)
        {
            detail = $"references empty vertex buffer layer {layer}.";
            return false;
        }

        buffer = layerBuffer as byte[] ?? layerBuffer.ToArray();
        return true;
    }

    private static bool TryDecodeLegacy(
        byte[] trmshPayload,
        byte[] trmbfPayload,
        TrinityModelRoot trmdl,
        string fallbackModelName,
        out ExportModel model,
        out string detail)
    {
        model = null!;
        detail = string.Empty;

        if (!TryParse(trmshPayload, "TRMSH", out TrinityLegacyMeshRoot trmsh, out detail) ||
            !TryParse(trmbfPayload, "TRMBF", out TrinityLegacyBufferRoot trmbf, out detail))
        {
            return false;
        }

        var mesh = trmsh.Meshes?.FirstOrDefault();
        if (mesh is null)
        {
            detail = "TRMSH decode failed: mesh vector is empty.";
            return false;
        }

        var part = mesh.Parts?.FirstOrDefault();
        if (part is null)
        {
            detail = "TRMSH decode failed: first mesh has no parts.";
            return false;
        }

        var bufferSetIndex = Math.Max(0, part.BufferSetIndex);
        if (trmbf.BufferSets is null || trmbf.BufferSets.Count <= bufferSetIndex)
        {
            detail = $"TRMBF decode failed: buffer set index {bufferSetIndex} is missing.";
            return false;
        }

        var bufferSet = trmbf.BufferSets[bufferSetIndex];
        if (bufferSet?.VertexBuffer is null || bufferSet.IndexBuffer is null)
        {
            detail = "TRMBF decode failed: vertex or index buffer vector is missing.";
            return false;
        }

        var vertexBuffer = bufferSet.VertexBuffer as byte[] ?? bufferSet.VertexBuffer.ToArray();
        var indexBuffer = bufferSet.IndexBuffer as byte[] ?? bufferSet.IndexBuffer.ToArray();

        var stride = part.VertexStride > 0 ? part.VertexStride : 12;
        var positionOffset = Math.Max(0, part.PositionOffset);
        if (stride < positionOffset + 12)
        {
            detail = $"TRMSH decode failed: invalid vertex stride {stride} for position offset {positionOffset}.";
            return false;
        }

        var inferredVertexCount = vertexBuffer.Length / stride;
        var vertexCount = part.VertexCount > 0 ? part.VertexCount : inferredVertexCount;
        if (vertexCount <= 0 || inferredVertexCount < vertexCount)
        {
            detail = $"TRMBF decode failed: vertex buffer is too small for {vertexCount} vertices at stride {stride}.";
            return false;
        }

        var vertices = new List<ExportVector3>(vertexCount);
        var normals = new List<ExportVector3>(vertexCount);
        var uvs = new List<ExportVector2>(vertexCount);

        var normalOffset = part.NormalOffset;
        var uvOffset = part.UvOffset;
        var canReadNormals = normalOffset >= 0 && stride >= normalOffset + 12;
        var canReadUvs = uvOffset >= 0 && stride >= uvOffset + 8;

        for (var i = 0; i < vertexCount; i++)
        {
            var vertexStart = i * stride;
            if (!TryReadVector3(vertexBuffer, vertexStart + positionOffset, TrinityVertexFormat.X32Y32Z32Float, isNormal: false, out var vertex))
            {
                detail = $"TRMBF decode failed: could not read position for vertex {i}.";
                return false;
            }

            vertices.Add(vertex);

            if (canReadNormals &&
                TryReadVector3(vertexBuffer, vertexStart + normalOffset, TrinityVertexFormat.X32Y32Z32Float, isNormal: true, out var normal))
            {
                normals.Add(normal);
            }

            if (canReadUvs &&
                TryReadVector2(vertexBuffer, vertexStart + uvOffset, TrinityVertexFormat.X32Y32Float, out var uv))
            {
                uvs.Add(uv);
            }
        }

        if (normals.Count != vertexCount)
        {
            normals.Clear();
        }

        if (uvs.Count != vertexCount)
        {
            uvs.Clear();
        }

        var indexElementSize = part.IndexFormat == 4 ? 4 : 2;
        var indexStart = Math.Max(0, part.IndexStart);
        var inferredIndexCount = (indexBuffer.Length / indexElementSize) - indexStart;
        var indexCount = part.IndexCount > 0 ? part.IndexCount : inferredIndexCount;
        if (indexCount <= 0)
        {
            detail = "TRMBF decode failed: index buffer does not contain any triangle indices.";
            return false;
        }

        var requiredIndexBytes = (indexStart + indexCount) * indexElementSize;
        if (requiredIndexBytes > indexBuffer.Length)
        {
            detail = $"TRMBF decode failed: index buffer is too small for {indexCount} indices.";
            return false;
        }

        if ((indexCount % 3) != 0)
        {
            detail = $"TRMSH decode failed: index count {indexCount} is not a triangle list.";
            return false;
        }

        var indices = new List<int>(indexCount);
        for (var i = 0; i < indexCount; i++)
        {
            var offset = (indexStart + i) * indexElementSize;
            var index = indexElementSize == 4
                ? BinaryPrimitives.ReadInt32LittleEndian(indexBuffer.AsSpan(offset, 4))
                : BinaryPrimitives.ReadUInt16LittleEndian(indexBuffer.AsSpan(offset, 2));

            if (index < 0 || index >= vertexCount)
            {
                detail = $"TRMBF decode failed: index {index} is outside vertex range 0..{vertexCount - 1}.";
                return false;
            }

            indices.Add(index);
        }

        var modelName = !string.IsNullOrWhiteSpace(trmdl.ModelName)
            ? trmdl.ModelName
            : fallbackModelName;
        var meshName = !string.IsNullOrWhiteSpace(mesh.Name)
            ? mesh.Name
            : "mesh_000";

        model = new ExportModel
        {
            ModelName = modelName,
            Meshes =
            [
                new ExportMesh
                {
                    MeshName = meshName,
                    Vertices = vertices,
                    Normals = normals,
                    Uvs = uvs,
                    Indices = indices,
                },
            ],
        };

        detail = string.Empty;
        return true;
    }

    private static bool TryParse<T>(byte[] payload, string label, out T value, out string detail)
        where T : class
    {
        value = null!;
        detail = string.Empty;

        if (payload.Length < 4)
        {
            detail = $"{label} decode failed: payload is too small ({payload.Length} bytes).";
            return false;
        }

        try
        {
            var parsed = Serializer.Parse<T>(payload);
            if (parsed is null)
            {
                detail = $"{label} decode failed: parsed object was null.";
                return false;
            }

            value = parsed;
            return true;
        }
        catch (Exception ex)
        {
            detail = $"{label} decode failed: {ex.Message}";
            return false;
        }
    }

    private static bool TryReadVector3(byte[] data, int offset, TrinityVertexFormat format, bool isNormal, out ExportVector3 value)
    {
        value = default;
        switch (format)
        {
            case TrinityVertexFormat.X32Y32Z32Float:
                return TryReadFloatVector3(data, offset, out value);
            case TrinityVertexFormat.W32X32Y32Z32Float:
                return TryReadFloatVector3(data, offset + 4, out value);
            case TrinityVertexFormat.W16X16Y16Z16Float:
                return TryReadHalfVector3(data, offset, out value);
            case TrinityVertexFormat.W16X16Y16Z16UnsignedNormalized:
                return TryReadNorm16Vector3(data, offset, isNormal, out value);
            case TrinityVertexFormat.R8G8B8A8UnsignedNormalized:
            case TrinityVertexFormat.W8X8Y8Z8Unsigned:
                return TryReadNorm8Vector3(data, offset, isNormal, out value);
            default:
                return false;
        }
    }

    private static bool TryReadVector2(byte[] data, int offset, TrinityVertexFormat format, out ExportVector2 value)
    {
        value = default;
        switch (format)
        {
            case TrinityVertexFormat.X32Y32Float:
                if (!HasBytes(data, offset, 8))
                {
                    return false;
                }

                value = new ExportVector2(
                    BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset, 4)),
                    BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset + 4, 4)));
                return true;
            case TrinityVertexFormat.W16X16Y16Z16Float:
                if (!HasBytes(data, offset, 4))
                {
                    return false;
                }

                value = new ExportVector2(ReadHalf(data, offset), ReadHalf(data, offset + 2));
                return true;
            case TrinityVertexFormat.W16X16Y16Z16UnsignedNormalized:
                if (!HasBytes(data, offset, 4))
                {
                    return false;
                }

                value = new ExportVector2(ReadUnorm16(data, offset), ReadUnorm16(data, offset + 2));
                return true;
            case TrinityVertexFormat.R8G8B8A8UnsignedNormalized:
            case TrinityVertexFormat.W8X8Y8Z8Unsigned:
                if (!HasBytes(data, offset, 2))
                {
                    return false;
                }

                value = new ExportVector2(ReadUnorm8(data, offset), ReadUnorm8(data, offset + 1));
                return true;
            default:
                return false;
        }
    }

    private static bool TryReadFloatVector3(byte[] data, int offset, out ExportVector3 value)
    {
        value = default;
        if (!HasBytes(data, offset, 12))
        {
            return false;
        }

        value = new ExportVector3(
            BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset, 4)),
            BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset + 4, 4)),
            BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset + 8, 4)));
        return true;
    }

    private static bool TryReadHalfVector3(byte[] data, int offset, out ExportVector3 value)
    {
        value = default;
        if (!HasBytes(data, offset, 6))
        {
            return false;
        }

        value = new ExportVector3(
            ReadHalf(data, offset),
            ReadHalf(data, offset + 2),
            ReadHalf(data, offset + 4));
        return true;
    }

    private static bool TryReadNorm16Vector3(byte[] data, int offset, bool signed, out ExportVector3 value)
    {
        value = default;
        if (!HasBytes(data, offset, 6))
        {
            return false;
        }

        value = signed
            ? new ExportVector3(ReadSnorm16(data, offset), ReadSnorm16(data, offset + 2), ReadSnorm16(data, offset + 4))
            : new ExportVector3(ReadUnorm16(data, offset), ReadUnorm16(data, offset + 2), ReadUnorm16(data, offset + 4));
        return true;
    }

    private static bool TryReadNorm8Vector3(byte[] data, int offset, bool signed, out ExportVector3 value)
    {
        value = default;
        if (!HasBytes(data, offset, 3))
        {
            return false;
        }

        value = signed
            ? new ExportVector3(ReadSnorm8(data, offset), ReadSnorm8(data, offset + 1), ReadSnorm8(data, offset + 2))
            : new ExportVector3(ReadUnorm8(data, offset), ReadUnorm8(data, offset + 1), ReadUnorm8(data, offset + 2));
        return true;
    }

    private static bool HasBytes(byte[] data, int offset, int length)
    {
        return offset >= 0 && offset <= data.Length - length;
    }

    private static float ReadHalf(byte[] data, int offset)
    {
        var raw = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
        return (float)BitConverter.UInt16BitsToHalf(raw);
    }

    private static float ReadUnorm16(byte[] data, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2)) / 65535f;
    }

    private static float ReadSnorm16(byte[] data, int offset)
    {
        return (ReadUnorm16(data, offset) * 2f) - 1f;
    }

    private static float ReadUnorm8(byte[] data, int offset)
    {
        return data[offset] / 255f;
    }

    private static float ReadSnorm8(byte[] data, int offset)
    {
        return (ReadUnorm8(data, offset) * 2f) - 1f;
    }
}
