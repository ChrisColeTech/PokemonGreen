using System.Buffers.Binary;
using PokemonGreen.SwitchToolboxCli.Core.Models;

namespace PokemonGreen.SwitchToolboxCli.Core.Extraction;

public enum TrinitySniffConfidence
{
    Low,
    Medium,
    High,
}

public sealed record TrinityPayloadClassification(
    string Extension,
    TrinityEntryRole Role,
    TrinitySniffConfidence Confidence,
    string Reason);

public sealed class TrinityPayloadSniffer
{
    private const int MaxModelMeshRefs = 2048;
    private const int MaxMeshCount = 4096;
    private const int MaxMeshParts = 16384;
    private const int MaxBufferSets = 4096;
    private const int MaxSkeletonBones = 8192;

    public bool TryClassify(ReadOnlySpan<byte> payload, out TrinityPayloadClassification classification)
    {
        classification = null!;
        if (payload.Length < 8)
        {
            return false;
        }

        if (TryMatchTrmsh(payload))
        {
            classification = new TrinityPayloadClassification(
                ".trmsh",
                TrinityEntryRole.Mesh,
                TrinitySniffConfidence.High,
                "TRMSH parse+safety checks matched mesh and part layout.");
            return true;
        }

        if (TryMatchTrmbf(payload))
        {
            classification = new TrinityPayloadClassification(
                ".trmbf",
                TrinityEntryRole.BlendShape,
                TrinitySniffConfidence.High,
                "TRMBF parse+safety checks matched buffer set vectors.");
            return true;
        }

        if (TryMatchTrskl(payload))
        {
            classification = new TrinityPayloadClassification(
                ".trskl",
                TrinityEntryRole.Skeleton,
                TrinitySniffConfidence.High,
                "TRSKL parse+safety checks matched skeleton table layout.");
            return true;
        }

        if (TryMatchTrmdl(payload))
        {
            classification = new TrinityPayloadClassification(
                ".trmdl",
                TrinityEntryRole.Model,
                TrinitySniffConfidence.High,
                "TRMDL parse+safety checks matched model mesh references.");
            return true;
        }

        if (TryMatchContainerPackage(payload))
        {
            classification = new TrinityPayloadClassification(
                ".trmmt",
                TrinityEntryRole.ModelContainer,
                TrinitySniffConfidence.Medium,
                "Container reference scan matched Trinity component paths.");
            return true;
        }

        if (TryMatchTranm(payload))
        {
            classification = new TrinityPayloadClassification(
                ".tranm",
                TrinityEntryRole.AnimationClip,
                TrinitySniffConfidence.High,
                "TRANM/CHR0 signature matched animation payload sanity checks.");
            return true;
        }

        if (TryMatchTraef(payload))
        {
            classification = new TrinityPayloadClassification(
                ".traef",
                TrinityEntryRole.EffectClip,
                TrinitySniffConfidence.High,
                "TRAEF signature matched effect payload sanity checks.");
            return true;
        }

        return false;
    }

    private static bool TryMatchTrmdl(ReadOnlySpan<byte> payload)
    {
        if (!TryGetRootTable(payload, out var rootTablePos))
        {
            return false;
        }

        if (!TryGetVectorField(payload, rootTablePos, fieldIndex: 1, out var meshRefVectorPos, out var meshRefCount) ||
            meshRefCount <= 0 ||
            meshRefCount > MaxModelMeshRefs ||
            !TryGetTableFromVector(payload, meshRefVectorPos, 0, out var meshRefTablePos))
        {
            return false;
        }

        var hasMeshPath = TryReadStringField(payload, meshRefTablePos, fieldIndex: 0, out var meshPath) &&
                          IsLikelyPath(meshPath, maxLength: 512);
        var hasBufferPath = TryReadStringField(payload, meshRefTablePos, fieldIndex: 1, out var bufferPath) &&
                            IsLikelyPath(bufferPath, maxLength: 512);
        if (!hasMeshPath && !hasBufferPath)
        {
            return false;
        }

        if (!TryValidateOptionalReferenceVector(payload, rootTablePos, fieldIndex: 2) ||
            !TryValidateOptionalReferenceVector(payload, rootTablePos, fieldIndex: 3))
        {
            return false;
        }

        if (TryReadStringField(payload, rootTablePos, fieldIndex: 0, out var modelName) &&
            !IsSaneIdentifier(modelName, maxLength: 256))
        {
            return false;
        }

        return true;
    }

    private static bool TryMatchTrmsh(ReadOnlySpan<byte> payload)
    {
        if (!TryGetRootTable(payload, out var rootTablePos) ||
            !TryGetVectorField(payload, rootTablePos, fieldIndex: 0, out var meshVectorPos, out var meshCount) ||
            meshCount <= 0 ||
            meshCount > MaxMeshCount ||
            !TryGetTableFromVector(payload, meshVectorPos, 0, out var meshTablePos) ||
            !TryGetVectorField(payload, meshTablePos, fieldIndex: 1, out var partsVectorPos, out var partCount) ||
            partCount <= 0 ||
            partCount > MaxMeshParts ||
            !TryGetTableFromVector(payload, partsVectorPos, 0, out var partTablePos))
        {
            return false;
        }

        var hasVertexCount = TryReadInt32Field(payload, partTablePos, fieldIndex: 1, out var vertexCount) &&
                             vertexCount is > 0 and < 2_000_000;
        var hasIndexCount = TryReadInt32Field(payload, partTablePos, fieldIndex: 2, out var indexCount) &&
                            indexCount is > 0 and < 8_000_000 &&
                            (indexCount % 3) == 0;
        var hasVertexStride = TryReadInt32Field(payload, partTablePos, fieldIndex: 6, out var vertexStride) &&
                              vertexStride is >= 12 and <= 512;
        var hasPositionOffset = TryReadInt32Field(payload, partTablePos, fieldIndex: 3, out var positionOffset) &&
                                positionOffset >= 0 &&
                                hasVertexStride &&
                                positionOffset + 12 <= vertexStride;
        var hasIndexFormat = TryReadInt32Field(payload, partTablePos, fieldIndex: 7, out var indexFormat) &&
                             (indexFormat == 2 || indexFormat == 4);
        var normalOffsetIsSane = !TryReadInt32Field(payload, partTablePos, fieldIndex: 4, out var normalOffset) ||
                                 (normalOffset >= 0 && normalOffset + 12 <= vertexStride);
        var uvOffsetIsSane = !TryReadInt32Field(payload, partTablePos, fieldIndex: 5, out var uvOffset) ||
                             (uvOffset >= 0 && uvOffset + 8 <= vertexStride);

        return hasVertexCount && hasIndexCount && hasVertexStride && hasPositionOffset && hasIndexFormat && normalOffsetIsSane && uvOffsetIsSane;
    }

    private static bool TryMatchTrmbf(ReadOnlySpan<byte> payload)
    {
        if (!TryGetRootTable(payload, out var rootTablePos) ||
            !TryGetVectorField(payload, rootTablePos, fieldIndex: 0, out var bufferSetVectorPos, out var setCount) ||
            setCount <= 0 ||
            setCount > MaxBufferSets ||
            !TryGetTableFromVector(payload, bufferSetVectorPos, 0, out var bufferSetTablePos))
        {
            return false;
        }

        var hasVertexVector = TryGetVectorField(payload, bufferSetTablePos, fieldIndex: 0, out _, out var vertexByteCount) &&
                              vertexByteCount is > 0 and <= 128_000_000;
        var hasIndexVector = TryGetVectorField(payload, bufferSetTablePos, fieldIndex: 1, out _, out var indexByteCount) &&
                             indexByteCount is >= 6 and <= 64_000_000 &&
                             (indexByteCount % 2) == 0;
        return hasVertexVector && hasIndexVector;
    }

    private static bool TryMatchTrskl(ReadOnlySpan<byte> payload)
    {
        if (!TryGetRootTable(payload, out var rootTablePos))
        {
            return false;
        }

        var hasRootName = !TryReadStringField(payload, rootTablePos, fieldIndex: 0, out var name) ||
                          IsSaneIdentifier(name, maxLength: 256);
        if (!hasRootName)
        {
            return false;
        }

        if (!TryGetVectorField(payload, rootTablePos, fieldIndex: 1, out var boneVectorPos, out var boneCount) ||
            boneCount <= 0 ||
            boneCount > MaxSkeletonBones ||
            !TryGetTableFromVector(payload, boneVectorPos, 0, out var firstBoneTablePos))
        {
            return false;
        }

        if (TryReadStringField(payload, firstBoneTablePos, fieldIndex: 0, out var boneName) &&
            !IsSaneIdentifier(boneName, maxLength: 256))
        {
            return false;
        }

        if (TryReadInt32Field(payload, firstBoneTablePos, fieldIndex: 1, out var parentIndex) &&
            (parentIndex < -1 || parentIndex >= boneCount))
        {
            return false;
        }

        return true;
    }

    private static bool TryMatchTranm(ReadOnlySpan<byte> payload)
    {
        if (StartsWithAscii(payload, "TRANM"))
        {
            return payload.Length >= 12 && HasAnyNonZero(payload.Slice(5, Math.Min(7, payload.Length - 5)));
        }

        if (StartsWithAscii(payload, "CHR0"))
        {
            return payload.Length >= 8 && HasAnyNonZero(payload.Slice(4, 4));
        }

        return false;
    }

    private static bool TryMatchContainerPackage(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 16)
        {
            return false;
        }

        var text = System.Text.Encoding.UTF8.GetString(payload);
        var extensionMatches = 0;
        extensionMatches += ContainsExtension(text, ".trmdl") ? 1 : 0;
        extensionMatches += ContainsExtension(text, ".trmsh") ? 1 : 0;
        extensionMatches += ContainsExtension(text, ".trmbf") ? 1 : 0;
        extensionMatches += ContainsExtension(text, ".trmtr") ? 1 : 0;
        extensionMatches += ContainsExtension(text, ".trskl") ? 1 : 0;
        extensionMatches += ContainsExtension(text, ".bntx") ? 1 : 0;
        extensionMatches += ContainsExtension(text, ".tranm") ? 1 : 0;
        extensionMatches += ContainsExtension(text, ".traef") ? 1 : 0;
        extensionMatches += ContainsExtension(text, ".tracm") ? 1 : 0;
        extensionMatches += ContainsExtension(text, ".tracs") ? 1 : 0;
        extensionMatches += ContainsExtension(text, ".tracl") ? 1 : 0;
        extensionMatches += ContainsExtension(text, ".tracr") ? 1 : 0;
        extensionMatches += ContainsExtension(text, ".tracp") ? 1 : 0;
        return extensionMatches >= 2;
    }

    private static bool ContainsExtension(string text, string extension)
    {
        return text.IndexOf(extension, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool TryMatchTraef(ReadOnlySpan<byte> payload)
    {
        return StartsWithAscii(payload, "TRAEF") &&
               payload.Length >= 8 &&
               HasAnyNonZero(payload.Slice(5, Math.Min(8, payload.Length - 5)));
    }

    private static bool TryValidateOptionalReferenceVector(ReadOnlySpan<byte> payload, int tablePos, int fieldIndex)
    {
        if (!TryGetFieldAddress(payload, tablePos, fieldIndex, out _))
        {
            return true;
        }

        if (!TryGetVectorField(payload, tablePos, fieldIndex, out var vectorPos, out var count) || count < 0 || count > MaxModelMeshRefs)
        {
            return false;
        }

        if (count == 0)
        {
            return true;
        }

        if (TryReadStringFromVector(payload, vectorPos, 0, out var stringRef))
        {
            return IsLikelyPath(stringRef, maxLength: 512) || IsSaneIdentifier(stringRef, maxLength: 256);
        }

        return TryGetTableFromVector(payload, vectorPos, 0, out _);
    }

    private static bool TryReadStringFromVector(ReadOnlySpan<byte> payload, int vectorDataPos, int index, out string value)
    {
        value = string.Empty;
        var slotPos = vectorDataPos + (index * 4);
        if (!TryReadInt32(payload, slotPos, out var stringRel))
        {
            return false;
        }

        var stringPos = slotPos + stringRel;
        return TryReadStringAt(payload, stringPos, out value);
    }

    private static bool TryGetRootTable(ReadOnlySpan<byte> payload, out int rootTablePos)
    {
        rootTablePos = 0;
        if (payload.Length < 4)
        {
            return false;
        }

        var rootOffset = BinaryPrimitives.ReadInt32LittleEndian(payload);
        if (!IsValidTable(payload, rootOffset))
        {
            return false;
        }

        rootTablePos = rootOffset;
        return true;
    }

    private static bool TryGetFieldAddress(ReadOnlySpan<byte> payload, int tablePos, int fieldIndex, out int fieldAddress)
    {
        fieldAddress = 0;
        if (!TryReadInt32(payload, tablePos, out var vtableOffset))
        {
            return false;
        }

        var vtablePos = tablePos - vtableOffset;
        if (!TryReadUInt16(payload, vtablePos, out var vtableSize))
        {
            return false;
        }

        var entryPos = vtablePos + 4 + (fieldIndex * 2);
        if (entryPos + 2 > vtablePos + vtableSize ||
            !TryReadUInt16(payload, entryPos, out var fieldOffset) ||
            fieldOffset == 0)
        {
            return false;
        }

        fieldAddress = tablePos + fieldOffset;
        return fieldAddress >= 0 && fieldAddress < payload.Length;
    }

    private static bool TryGetVectorField(
        ReadOnlySpan<byte> payload,
        int tablePos,
        int fieldIndex,
        out int vectorDataPos,
        out int vectorLength)
    {
        vectorDataPos = 0;
        vectorLength = 0;

        if (!TryGetFieldAddress(payload, tablePos, fieldIndex, out var fieldAddress) ||
            !TryReadInt32(payload, fieldAddress, out var vectorRel))
        {
            return false;
        }

        var vectorPos = fieldAddress + vectorRel;
        if (!TryReadInt32(payload, vectorPos, out vectorLength) || vectorLength < 0)
        {
            return false;
        }

        vectorDataPos = vectorPos + 4;
        return vectorDataPos >= 0 && vectorDataPos <= payload.Length;
    }

    private static bool TryGetTableFromVector(ReadOnlySpan<byte> payload, int vectorDataPos, int index, out int tablePos)
    {
        tablePos = 0;
        var slotPos = vectorDataPos + (index * 4);
        if (!TryReadInt32(payload, slotPos, out var tableRel))
        {
            return false;
        }

        tablePos = slotPos + tableRel;
        return IsValidTable(payload, tablePos);
    }

    private static bool TryReadInt32Field(ReadOnlySpan<byte> payload, int tablePos, int fieldIndex, out int value)
    {
        value = 0;
        if (!TryGetFieldAddress(payload, tablePos, fieldIndex, out var fieldAddress))
        {
            return false;
        }

        return TryReadInt32(payload, fieldAddress, out value);
    }

    private static bool TryReadStringField(ReadOnlySpan<byte> payload, int tablePos, int fieldIndex, out string value)
    {
        value = string.Empty;
        if (!TryGetFieldAddress(payload, tablePos, fieldIndex, out var fieldAddress) ||
            !TryReadInt32(payload, fieldAddress, out var rel))
        {
            return false;
        }

        var stringPos = fieldAddress + rel;
        return TryReadStringAt(payload, stringPos, out value);
    }

    private static bool TryReadStringAt(ReadOnlySpan<byte> payload, int stringPos, out string value)
    {
        value = string.Empty;
        if (!TryReadInt32(payload, stringPos, out var length) ||
            length <= 0 ||
            length > 2048 ||
            stringPos + 4 + length > payload.Length)
        {
            return false;
        }

        var bytes = payload.Slice(stringPos + 4, length).ToArray();
        try
        {
            value = System.Text.Encoding.UTF8.GetString(bytes);
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSaneIdentifier(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
        {
            return false;
        }

        return value.All(ch => !char.IsControl(ch));
    }

    private static bool IsLikelyPath(string value, int maxLength)
    {
        if (!IsSaneIdentifier(value, maxLength))
        {
            return false;
        }

        return value.Contains('/') || value.Contains('\\') || value.Contains('.');
    }

    private static bool HasAnyNonZero(ReadOnlySpan<byte> payload)
    {
        foreach (var value in payload)
        {
            if (value != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadInt32(ReadOnlySpan<byte> payload, int offset, out int value)
    {
        value = 0;
        if (offset < 0 || offset > payload.Length - 4)
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, 4));
        return true;
    }

    private static bool TryReadUInt16(ReadOnlySpan<byte> payload, int offset, out ushort value)
    {
        value = 0;
        if (offset < 0 || offset > payload.Length - 2)
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset, 2));
        return true;
    }

    private static bool IsValidTable(ReadOnlySpan<byte> payload, int tablePos)
    {
        if (tablePos < 4 || tablePos > payload.Length - 4)
        {
            return false;
        }

        if (!TryReadInt32(payload, tablePos, out var vtableOffset))
        {
            return false;
        }

        var vtablePos = tablePos - vtableOffset;
        return TryReadUInt16(payload, vtablePos, out var vtableSize) && vtableSize >= 4;
    }

    private static bool StartsWithAscii(ReadOnlySpan<byte> payload, string signature)
    {
        if (payload.Length < signature.Length)
        {
            return false;
        }

        for (var i = 0; i < signature.Length; i++)
        {
            if (payload[i] != signature[i])
            {
                return false;
            }
        }

        return true;
    }
}
