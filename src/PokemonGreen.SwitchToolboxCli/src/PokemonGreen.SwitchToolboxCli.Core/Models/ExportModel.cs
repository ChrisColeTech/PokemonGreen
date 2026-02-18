namespace PokemonGreen.SwitchToolboxCli.Core.Models;

public readonly record struct ExportVector2(float X, float Y);

public readonly record struct ExportVector3(float X, float Y, float Z);

public readonly record struct ExportMatrix4x4(
    float M11,
    float M12,
    float M13,
    float M14,
    float M21,
    float M22,
    float M23,
    float M24,
    float M31,
    float M32,
    float M33,
    float M34,
    float M41,
    float M42,
    float M43,
    float M44)
{
    public static ExportMatrix4x4 Identity { get; } = new(
        1f, 0f, 0f, 0f,
        0f, 1f, 0f, 0f,
        0f, 0f, 1f, 0f,
        0f, 0f, 0f, 1f);

    public IReadOnlyList<float> ToRowMajor() =>
    [
        M11, M12, M13, M14,
        M21, M22, M23, M24,
        M31, M32, M33, M34,
        M41, M42, M43, M44,
    ];
}

public sealed class ExportModel
{
    public required string ModelName { get; init; }
    public required IReadOnlyList<ExportMesh> Meshes { get; init; }
    public ExportArmature? Armature { get; init; }
}

public sealed class ExportMesh
{
    public required string MeshName { get; init; }
    public required IReadOnlyList<ExportVector3> Vertices { get; init; }
    public IReadOnlyList<ExportVector3> Normals { get; init; } = Array.Empty<ExportVector3>();
    public IReadOnlyList<ExportVector2> Uvs { get; init; } = Array.Empty<ExportVector2>();
    public required IReadOnlyList<int> Indices { get; init; }

    public bool HasNormals => Normals.Count == Vertices.Count && Normals.Count > 0;
    public bool HasUvs => Uvs.Count == Vertices.Count && Uvs.Count > 0;
}

public sealed class ExportArmature
{
    public required string Name { get; init; }
    public required IReadOnlyList<ExportBone> Bones { get; init; }

    public bool HasBones => Bones.Count > 0;
}

public sealed class ExportBone
{
    public required string Name { get; init; }
    public required int ParentIndex { get; init; }
    public ExportVector3 Translation { get; init; } = new(0f, 0f, 0f);
    public ExportVector3 RotationEulerRadians { get; init; } = new(0f, 0f, 0f);
    public ExportVector3 Scale { get; init; } = new(1f, 1f, 1f);
    public ExportMatrix4x4 InverseBindMatrix { get; init; } = ExportMatrix4x4.Identity;
}
