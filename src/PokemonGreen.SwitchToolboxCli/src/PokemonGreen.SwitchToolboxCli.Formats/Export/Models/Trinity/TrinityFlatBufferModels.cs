using FlatSharp.Attributes;

namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Models.Trinity;

[FlatBufferTable]
public class TrinityModelRoot
{
    [FlatBufferItem(0)]
    public virtual string? ModelName { get; set; }

    [FlatBufferItem(1)]
    public virtual IList<TrinityModelMeshReference>? Meshes { get; set; }

    [FlatBufferItem(2)]
    public virtual IList<string>? SkeletonReferences { get; set; }

    [FlatBufferItem(3)]
    public virtual IList<string>? MaterialReferences { get; set; }
}

[FlatBufferTable]
public class TrinityModelMeshReference
{
    [FlatBufferItem(0)]
    public virtual string? MeshPath { get; set; }

    [FlatBufferItem(1)]
    public virtual string? BufferPath { get; set; }
}

[FlatBufferEnum(typeof(int))]
public enum TrinityIndexFormat
{
    Byte = 0,
    Short = 1,
    Int = 2,
}

[FlatBufferEnum(typeof(int))]
public enum TrinityVertexUsage
{
    None = 0,
    Position = 1,
    Normal = 2,
    Tangent = 3,
    Binormal = 4,
    Color = 5,
    TexCoord = 6,
    BlendIndex = 7,
    BlendWeights = 8,
}

[FlatBufferEnum(typeof(int))]
public enum TrinityVertexFormat
{
    None = 0,
    R8G8B8A8UnsignedNormalized = 20,
    W8X8Y8Z8Unsigned = 22,
    W16X16Y16Z16UnsignedNormalized = 39,
    W16X16Y16Z16Float = 43,
    X32Y32Float = 48,
    X32Y32Z32Float = 51,
    W32X32Y32Z32Unsigned = 52,
    W32X32Y32Z32Float = 54,
}

[FlatBufferTable]
public class TrinityVertexElement
{
    [FlatBufferItem(0)]
    public virtual int VertexElementSizeIndex { get; set; }

    [FlatBufferItem(1)]
    public virtual TrinityVertexUsage VertexUsage { get; set; }

    [FlatBufferItem(2)]
    public virtual int VertexElementLayer { get; set; }

    [FlatBufferItem(3)]
    public virtual TrinityVertexFormat VertexFormat { get; set; }

    [FlatBufferItem(4)]
    public virtual int VertexElementOffset { get; set; }
}

[FlatBufferTable]
public class TrinityVertexElementSize
{
    [FlatBufferItem(0)]
    public virtual int ElementSize { get; set; }
}

[FlatBufferTable]
public class TrinityVertexDeclaration
{
    [FlatBufferItem(0)]
    public virtual IList<TrinityVertexElement>? VertexElements { get; set; }

    [FlatBufferItem(1)]
    public virtual IList<TrinityVertexElementSize>? VertexElementSizes { get; set; }
}

[FlatBufferTable]
public class TrinityMeshPart
{
    [FlatBufferItem(0)]
    public virtual int IndexCount { get; set; }

    [FlatBufferItem(1)]
    public virtual int IndexOffset { get; set; }

    [FlatBufferItem(2)]
    public virtual int UnknownField02 { get; set; }

    [FlatBufferItem(3)]
    public virtual string? MaterialName { get; set; }

    [FlatBufferItem(4)]
    public virtual int VertexDeclarationIndex { get; set; }
}

[FlatBufferTable]
public class TrinityMesh
{
    [FlatBufferItem(0)]
    public virtual string? Name { get; set; }

    [FlatBufferItem(2)]
    public virtual TrinityIndexFormat IndexType { get; set; }

    [FlatBufferItem(3)]
    public virtual IList<TrinityVertexDeclaration>? VertexDeclarations { get; set; }

    [FlatBufferItem(4)]
    public virtual IList<TrinityMeshPart>? Parts { get; set; }
}

[FlatBufferTable]
public class TrinityMeshRoot
{
    [FlatBufferItem(0)]
    public virtual int Version { get; set; }

    [FlatBufferItem(1)]
    public virtual IList<TrinityMesh>? Meshes { get; set; }

    [FlatBufferItem(2)]
    public virtual string? BufferFilePath { get; set; }
}

[FlatBufferTable]
public class TrinityBuffer
{
    [FlatBufferItem(0)]
    public virtual IList<byte>? Bytes { get; set; }
}

[FlatBufferTable]
public class TrinityModelBuffer
{
    [FlatBufferItem(0)]
    public virtual IList<TrinityBuffer>? IndexBuffers { get; set; }

    [FlatBufferItem(1)]
    public virtual IList<TrinityBuffer>? VertexBuffers { get; set; }
}

[FlatBufferTable]
public class TrinityBufferRoot
{
    [FlatBufferItem(1)]
    public virtual IList<TrinityModelBuffer>? MeshBuffers { get; set; }
}

[FlatBufferTable]
public class TrinityLegacyMeshRoot
{
    [FlatBufferItem(0)]
    public virtual IList<TrinityLegacyMesh>? Meshes { get; set; }
}

[FlatBufferTable]
public class TrinityLegacyMesh
{
    [FlatBufferItem(0)]
    public virtual string? Name { get; set; }

    [FlatBufferItem(1)]
    public virtual IList<TrinityLegacyMeshPart>? Parts { get; set; }
}

[FlatBufferTable]
public class TrinityLegacyMeshPart
{
    [FlatBufferItem(0)]
    public virtual int BufferSetIndex { get; set; }

    [FlatBufferItem(1)]
    public virtual int VertexCount { get; set; }

    [FlatBufferItem(2)]
    public virtual int IndexCount { get; set; }

    [FlatBufferItem(3)]
    public virtual int PositionOffset { get; set; }

    [FlatBufferItem(4)]
    public virtual int NormalOffset { get; set; }

    [FlatBufferItem(5)]
    public virtual int UvOffset { get; set; }

    [FlatBufferItem(6)]
    public virtual int VertexStride { get; set; }

    [FlatBufferItem(7)]
    public virtual int IndexFormat { get; set; }

    [FlatBufferItem(8)]
    public virtual int IndexStart { get; set; }
}

[FlatBufferTable]
public class TrinityLegacyBufferRoot
{
    [FlatBufferItem(0)]
    public virtual IList<TrinityLegacyBufferSet>? BufferSets { get; set; }
}

[FlatBufferTable]
public class TrinityLegacyBufferSet
{
    [FlatBufferItem(0)]
    public virtual IList<byte>? VertexBuffer { get; set; }

    [FlatBufferItem(1)]
    public virtual IList<byte>? IndexBuffer { get; set; }
}

[FlatBufferTable]
public class TrinitySkeletonRoot
{
    [FlatBufferItem(0)]
    public virtual uint Version { get; set; }

    [FlatBufferItem(1)]
    public virtual IList<TrinityTransformNode>? TransformNodes { get; set; }

    [FlatBufferItem(2)]
    public virtual IList<TrinityJointInfo>? JointInfos { get; set; }
}

[FlatBufferTable]
public class TrinityTransformNode
{
    [FlatBufferItem(0)]
    public virtual string? Name { get; set; }

    [FlatBufferItem(1)]
    public virtual TrinitySrt? Transform { get; set; }

    [FlatBufferItem(4)]
    public virtual int ParentNodeIndex { get; set; }

    [FlatBufferItem(5)]
    public virtual int JointInfoIndex { get; set; }
}

[FlatBufferTable]
public class TrinitySrt
{
    [FlatBufferItem(0)]
    public virtual TrinityVector3f? Scale { get; set; }

    [FlatBufferItem(1)]
    public virtual TrinityVector3f? Rotate { get; set; }

    [FlatBufferItem(2)]
    public virtual TrinityVector3f? Translate { get; set; }
}

[FlatBufferTable]
public class TrinityJointInfo
{
    [FlatBufferItem(2)]
    public virtual TrinityMatrix4x3f? InverseBindPoseMatrix { get; set; }
}

[FlatBufferTable]
public class TrinityMatrix4x3f
{
    [FlatBufferItem(0)]
    public virtual TrinityVector3f? AxisX { get; set; }

    [FlatBufferItem(1)]
    public virtual TrinityVector3f? AxisY { get; set; }

    [FlatBufferItem(2)]
    public virtual TrinityVector3f? AxisZ { get; set; }

    [FlatBufferItem(3)]
    public virtual TrinityVector3f? AxisW { get; set; }
}

[FlatBufferStruct]
public class TrinityVector3f
{
    [FlatBufferItem(0)]
    public float X { get; set; }

    [FlatBufferItem(1)]
    public float Y { get; set; }

    [FlatBufferItem(2)]
    public float Z { get; set; }
}
