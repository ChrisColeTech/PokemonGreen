using System.Globalization;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokemonGreen.Core.Rendering.Skeletal;

public sealed class SkinnedDaeModel
{
    private static readonly XNamespace ColladaNs = "http://www.collada.org/2005/11/COLLADASchema";

    private readonly List<SkinnedMesh> _meshes = new();

    public VertexBuffer? VertexBuffer { get; private set; }
    public IndexBuffer? IndexBuffer { get; private set; }
    public int PrimitiveCount { get; private set; }

    public void Load(GraphicsDevice graphics, string daePath, SkeletonRig rig)
    {
        XDocument doc = XDocument.Load(daePath);

        Dictionary<string, GeometryData> geometries = ParseGeometries(doc);
        Dictionary<string, ControllerSkinData> skins = ParseControllers(doc, rig);

        _meshes.Clear();
        foreach ((string geometryId, GeometryData geometry) in geometries)
        {
            if (!skins.TryGetValue(geometryId, out ControllerSkinData? skinData))
            {
                continue;
            }

            SkinnedMesh? mesh = BuildMesh(geometry, skinData);
            if (mesh is not null)
            {
                _meshes.Add(mesh);
            }
        }

        RebuildBuffers(graphics, rig.InverseBindTransforms);
    }

    public void UpdatePose(GraphicsDevice graphics, Matrix[] skinPose)
    {
        RebuildBuffers(graphics, skinPose);
    }

    private void RebuildBuffers(GraphicsDevice graphics, Matrix[] skinMatrices)
    {
        List<VertexPositionNormalTexture> allVertices = new();
        List<int> allIndices = new();

        for (int meshIndex = 0; meshIndex < _meshes.Count; meshIndex++)
        {
            SkinnedMesh mesh = _meshes[meshIndex];
            int baseVertex = allVertices.Count;

            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                SkinnedVertex src = mesh.Vertices[i];
                Matrix skin = ComputeSkinMatrix(src, skinMatrices);

                Vector3 pos = Vector3.Transform(src.Position, skin);
                Vector3 nrm = Vector3.Normalize(Vector3.TransformNormal(src.Normal, skin));

                allVertices.Add(new VertexPositionNormalTexture(pos, nrm, src.Uv));
            }

            for (int i = 0; i < mesh.Indices.Length; i++)
            {
                allIndices.Add(baseVertex + mesh.Indices[i]);
            }
        }

        if (allVertices.Count == 0 || allIndices.Count == 0)
        {
            PrimitiveCount = 0;
            return;
        }

        VertexBuffer = new VertexBuffer(graphics, typeof(VertexPositionNormalTexture), allVertices.Count, BufferUsage.WriteOnly);
        VertexBuffer.SetData(allVertices.ToArray());

        IndexBuffer = new IndexBuffer(graphics, IndexElementSize.ThirtyTwoBits, allIndices.Count, BufferUsage.WriteOnly);
        IndexBuffer.SetData(allIndices.ToArray());

        PrimitiveCount = allIndices.Count / 3;
    }

    private static Matrix ComputeSkinMatrix(SkinnedVertex v, Matrix[] skinMatrices)
    {
        Matrix result = Matrix.Identity;
        float total = 0f;

        for (int i = 0; i < 4; i++)
        {
            float w = v.Weights[i];
            if (w <= 0f) continue;

            int bone = v.BoneIndices[i];
            if (bone < 0 || bone >= skinMatrices.Length) continue;

            result += skinMatrices[bone] * w;
            total += w;
        }

        if (total <= 0f)
        {
            return Matrix.Identity;
        }

        return result;
    }

    private static Dictionary<string, GeometryData> ParseGeometries(XDocument doc)
    {
        Dictionary<string, GeometryData> result = new(StringComparer.Ordinal);
        foreach (XElement geometry in doc.Descendants(ColladaNs + "geometry"))
        {
            string? geometryId = geometry.Attribute("id")?.Value;
            XElement? mesh = geometry.Element(ColladaNs + "mesh");
            if (string.IsNullOrWhiteSpace(geometryId) || mesh is null) continue;

            Dictionary<string, float[]> sources = mesh
                .Elements(ColladaNs + "source")
                .Where(x => x.Attribute("id") is not null)
                .ToDictionary(
                    x => x.Attribute("id")!.Value,
                    x => ParseFloats(x.Element(ColladaNs + "float_array")?.Value),
                    StringComparer.Ordinal);

            XElement? vertices = mesh.Element(ColladaNs + "vertices");
            string? positionSource = vertices?.Elements(ColladaNs + "input")
                .FirstOrDefault(x => string.Equals(x.Attribute("semantic")?.Value, "POSITION", StringComparison.Ordinal))
                ?.Attribute("source")?.Value.TrimStart('#');
            if (string.IsNullOrWhiteSpace(positionSource) || !sources.TryGetValue(positionSource, out float[]? positions))
            {
                continue;
            }

            XElement? triangles = mesh.Element(ColladaNs + "triangles");
            if (triangles is null) continue;

            List<InputSpec> inputs = triangles.Elements(ColladaNs + "input")
                .Select(x => new InputSpec
                {
                    Semantic = x.Attribute("semantic")?.Value ?? string.Empty,
                    Source = x.Attribute("source")?.Value?.TrimStart('#') ?? string.Empty,
                    Offset = int.TryParse(x.Attribute("offset")?.Value, out int o) ? o : 0
                }).ToList();

            int stride = inputs.Count == 0 ? 1 : inputs.Max(x => x.Offset) + 1;
            int[] indexData = ParseInts(triangles.Element(ColladaNs + "p")?.Value);
            if (indexData.Length == 0) continue;

            string? normalSource = inputs.FirstOrDefault(x => x.Semantic == "NORMAL")?.Source;
            string? uvSource = inputs.FirstOrDefault(x => x.Semantic == "TEXCOORD")?.Source;
            float[] normals = !string.IsNullOrWhiteSpace(normalSource) && sources.TryGetValue(normalSource, out float[]? n) ? n : Array.Empty<float>();
            float[] uvs = !string.IsNullOrWhiteSpace(uvSource) && sources.TryGetValue(uvSource, out float[]? uv) ? uv : Array.Empty<float>();

            result[geometryId] = new GeometryData
            {
                Positions = positions,
                Normals = normals,
                Uvs = uvs,
                Inputs = inputs,
                Indices = indexData,
                Stride = stride
            };
        }

        return result;
    }

    private static Dictionary<string, ControllerSkinData> ParseControllers(XDocument doc, SkeletonRig rig)
    {
        Dictionary<string, ControllerSkinData> result = new(StringComparer.Ordinal);

        foreach (XElement controller in doc.Descendants(ColladaNs + "controller"))
        {
            XElement? skin = controller.Element(ColladaNs + "skin");
            if (skin is null) continue;

            string? geometryId = skin.Attribute("source")?.Value.TrimStart('#');
            if (string.IsNullOrWhiteSpace(geometryId)) continue;

            Dictionary<string, XElement> sources = skin.Elements(ColladaNs + "source")
                .Where(x => x.Attribute("id") is not null)
                .ToDictionary(x => x.Attribute("id")!.Value, x => x, StringComparer.Ordinal);

            XElement? joints = skin.Element(ColladaNs + "joints");
            XElement? vertexWeights = skin.Element(ColladaNs + "vertex_weights");
            if (joints is null || vertexWeights is null) continue;

            string? jointSourceId = joints.Elements(ColladaNs + "input")
                .FirstOrDefault(x => string.Equals(x.Attribute("semantic")?.Value, "JOINT", StringComparison.Ordinal))
                ?.Attribute("source")?.Value.TrimStart('#');
            string? weightSourceId = vertexWeights.Elements(ColladaNs + "input")
                .FirstOrDefault(x => string.Equals(x.Attribute("semantic")?.Value, "WEIGHT", StringComparison.Ordinal))
                ?.Attribute("source")?.Value.TrimStart('#');

            if (string.IsNullOrWhiteSpace(jointSourceId) || string.IsNullOrWhiteSpace(weightSourceId)) continue;
            if (!sources.TryGetValue(jointSourceId, out XElement? jointSource)) continue;
            if (!sources.TryGetValue(weightSourceId, out XElement? weightSource)) continue;

            string[] jointNames = ParseNames(jointSource.Element(ColladaNs + "Name_array")?.Value);
            float[] weights = ParseFloats(weightSource.Element(ColladaNs + "float_array")?.Value);
            int[] vcount = ParseInts(vertexWeights.Element(ColladaNs + "vcount")?.Value);
            int[] v = ParseInts(vertexWeights.Element(ColladaNs + "v")?.Value);
            if (vcount.Length == 0 || v.Length == 0) continue;

            List<VertexInfluence> influences = new(vcount.Length);
            int cursor = 0;
            for (int vertexIndex = 0; vertexIndex < vcount.Length; vertexIndex++)
            {
                int count = vcount[vertexIndex];
                List<(int Bone, float Weight)> pairs = new();
                for (int i = 0; i < count; i++)
                {
                    if (cursor + 1 >= v.Length) break;

                    int jointIndex = v[cursor++];
                    int weightIndex = v[cursor++];
                    if (jointIndex < 0 || jointIndex >= jointNames.Length) continue;
                    if (weightIndex < 0 || weightIndex >= weights.Length) continue;
                    if (!rig.TryGetBoneIndex(jointNames[jointIndex], out int boneIndex)) continue;

                    float weight = weights[weightIndex];
                    if (weight <= 0f) continue;
                    pairs.Add((boneIndex, weight));
                }

                VertexInfluence influence = VertexInfluence.FromPairs(pairs);
                influences.Add(influence);
            }

            result[geometryId] = new ControllerSkinData
            {
                InfluencesByControlPoint = influences
            };
        }

        return result;
    }

    private static SkinnedMesh? BuildMesh(GeometryData geometry, ControllerSkinData skin)
    {
        Dictionary<(int Pos, int Nrm, int Uv), int> remap = new();
        List<SkinnedVertex> vertices = new();
        List<int> indices = new();

        int posOffset = geometry.Inputs.FirstOrDefault(x => x.Semantic == "VERTEX")?.Offset ?? 0;
        int nrmOffset = geometry.Inputs.FirstOrDefault(x => x.Semantic == "NORMAL")?.Offset ?? posOffset;
        int uvOffset = geometry.Inputs.FirstOrDefault(x => x.Semantic == "TEXCOORD")?.Offset ?? posOffset;

        for (int i = 0; i < geometry.Indices.Length; i += geometry.Stride)
        {
            int posIndex = geometry.Indices[i + posOffset];
            int nrmIndex = geometry.Indices[i + nrmOffset];
            int uvIndex = geometry.Indices[i + uvOffset];

            (int Pos, int Nrm, int Uv) key = (posIndex, nrmIndex, uvIndex);
            if (remap.TryGetValue(key, out int existing))
            {
                indices.Add(existing);
                continue;
            }

            if (posIndex * 3 + 2 >= geometry.Positions.Length)
            {
                continue;
            }

            Vector3 pos = new(
                geometry.Positions[posIndex * 3],
                geometry.Positions[posIndex * 3 + 1],
                geometry.Positions[posIndex * 3 + 2]);

            Vector3 nrm = Vector3.UnitY;
            if (nrmIndex * 3 + 2 < geometry.Normals.Length)
            {
                nrm = new Vector3(
                    geometry.Normals[nrmIndex * 3],
                    geometry.Normals[nrmIndex * 3 + 1],
                    geometry.Normals[nrmIndex * 3 + 2]);
            }

            Vector2 uv = Vector2.Zero;
            if (uvIndex * 2 + 1 < geometry.Uvs.Length)
            {
                uv = new Vector2(geometry.Uvs[uvIndex * 2], 1f - geometry.Uvs[uvIndex * 2 + 1]);
            }

            VertexInfluence influence = posIndex < skin.InfluencesByControlPoint.Count
                ? skin.InfluencesByControlPoint[posIndex]
                : VertexInfluence.Default;

            SkinnedVertex sv = new()
            {
                Position = pos,
                Normal = nrm,
                Uv = uv,
                BoneIndices = influence.BoneIndices,
                Weights = influence.Weights
            };

            int newIndex = vertices.Count;
            vertices.Add(sv);
            remap[key] = newIndex;
            indices.Add(newIndex);
        }

        if (vertices.Count == 0 || indices.Count == 0)
        {
            return null;
        }

        return new SkinnedMesh
        {
            Vertices = vertices.ToArray(),
            Indices = indices.ToArray()
        };
    }

    private static float[] ParseFloats(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<float>();
        string[] parts = value.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        float[] result = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]);
        }

        return result;
    }

    private static int[] ParseInts(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<int>();
        string[] parts = value.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        int[] result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out result[i]);
        }

        return result;
    }

    private static string[] ParseNames(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();
        return value.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private sealed class InputSpec
    {
        public required string Semantic { get; init; }
        public required string Source { get; init; }
        public required int Offset { get; init; }
    }

    private sealed class GeometryData
    {
        public required float[] Positions { get; init; }
        public required float[] Normals { get; init; }
        public required float[] Uvs { get; init; }
        public required List<InputSpec> Inputs { get; init; }
        public required int[] Indices { get; init; }
        public required int Stride { get; init; }
    }

    private sealed class ControllerSkinData
    {
        public required List<VertexInfluence> InfluencesByControlPoint { get; init; }
    }

    private sealed class SkinnedMesh
    {
        public required SkinnedVertex[] Vertices { get; init; }
        public required int[] Indices { get; init; }
    }

    private sealed class SkinnedVertex
    {
        public required Vector3 Position { get; init; }
        public required Vector3 Normal { get; init; }
        public required Vector2 Uv { get; init; }
        public required int[] BoneIndices { get; init; }
        public required float[] Weights { get; init; }
    }

    private readonly struct VertexInfluence
    {
        public static VertexInfluence Default => new(new[] { 0, 0, 0, 0 }, new[] { 1f, 0f, 0f, 0f });

        public VertexInfluence(int[] boneIndices, float[] weights)
        {
            BoneIndices = boneIndices;
            Weights = weights;
        }

        public int[] BoneIndices { get; }
        public float[] Weights { get; }

        public static VertexInfluence FromPairs(List<(int Bone, float Weight)> pairs)
        {
            if (pairs.Count == 0)
            {
                return Default;
            }

            pairs.Sort((a, b) => b.Weight.CompareTo(a.Weight));
            int[] bones = new[] { 0, 0, 0, 0 };
            float[] weights = new[] { 0f, 0f, 0f, 0f };

            float total = 0f;
            int count = Math.Min(4, pairs.Count);
            for (int i = 0; i < count; i++)
            {
                bones[i] = pairs[i].Bone;
                weights[i] = pairs[i].Weight;
                total += pairs[i].Weight;
            }

            if (total <= 0f)
            {
                weights[0] = 1f;
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    weights[i] /= total;
                }
            }

            return new VertexInfluence(bones, weights);
        }
    }
}
