using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokemonGreen.Core.Rendering;

public class DaeModel
{
    public VertexBuffer VertexBuffer { get; private set; }
    public IndexBuffer IndexBuffer { get; private set; }
    public int PrimitiveCount { get; private set; }
    public Texture2D Texture { get; set; }
    public List<DaeMesh> Meshes { get; } = new();

    public void Load(GraphicsDevice graphics, string daePath)
    {
        var xdoc = XDocument.Load(daePath);
        XNamespace ns = "http://www.collada.org/2005/11/COLLADASchema";

        var geometryElements = xdoc.Descendants(ns + "geometry");
        foreach (var geom in geometryElements)
        {
            var mesh = ParseMesh(graphics, geom, ns);
            if (mesh != null)
                Meshes.Add(mesh);
        }

        if (Meshes.Count > 0)
        {
            CombineMeshes(graphics);
        }
    }

    private DaeMesh ParseMesh(GraphicsDevice graphics, XElement geom, XNamespace ns)
    {
        var meshElem = geom.Element(ns + "mesh");
        if (meshElem == null) return null;

        var sources = new Dictionary<string, float[]>();
        foreach (var source in meshElem.Elements(ns + "source"))
        {
            var id = source.Attribute("id")?.Value;
            var floatArray = source.Element(ns + "float_array");
            if (id != null && floatArray != null)
            {
                var values = ParseFloats(floatArray.Value);
                sources[id] = values;
            }
        }

        var verticesElem = meshElem.Element(ns + "vertices");
        string positionSourceId = null;
        if (verticesElem != null)
        {
            var input = verticesElem.Element(ns + "input");
            if (input != null)
            {
                var sem = input.Attribute("semantic")?.Value;
                var src = input.Attribute("source")?.Value?.TrimStart('#');
                if (sem == "POSITION")
                    positionSourceId = src;
            }
        }

        if (positionSourceId == null) return null;

        var trianglesElem = meshElem.Element(ns + "triangles");
        if (trianglesElem == null) return null;

        int triangleCount = int.Parse(trianglesElem.Attribute("count")?.Value ?? "0");
        if (triangleCount == 0) return null;

        string normalSourceId = null;
        string uvSourceId = null;

        foreach (var input in trianglesElem.Elements(ns + "input"))
        {
            var sem = input.Attribute("semantic")?.Value;
            var src = input.Attribute("source")?.Value?.TrimStart('#');

            if (sem == "NORMAL") normalSourceId = src;
            else if (sem == "TEXCOORD") uvSourceId = src;
        }

        var indicesElem = trianglesElem.Element(ns + "p");
        if (indicesElem == null) return null;

        var indices = ParseInts(indicesElem.Value);

        var positions = sources.TryGetValue(positionSourceId, out var pos) ? pos : null;
        var normals = normalSourceId != null && sources.TryGetValue(normalSourceId, out var nrm) ? nrm : null;
        var uvs = uvSourceId != null && sources.TryGetValue(uvSourceId, out var uv) ? uv : null;

        if (positions == null) return null;

        var vertices = new List<VertexPositionNormalTexture>();
        var finalIndices = new List<int>();
        var vertexMap = new Dictionary<string, int>();

        int stride = 0;
        foreach (var input in trianglesElem.Elements(ns + "input"))
        {
            var off = input.Attribute("offset")?.Value;
            if (off != null)
            {
                int o = int.Parse(off);
                if (o >= stride) stride = o + 1;
            }
        }
        if (stride == 0) stride = 1;

        for (int i = 0; i < indices.Length; i += stride)
        {
            int posIdx = indices[i];
            int normalIdx = posIdx;
            int uvIdx = posIdx;

            foreach (var input in trianglesElem.Elements(ns + "input"))
            {
                var sem = input.Attribute("semantic")?.Value;
                var off = int.Parse(input.Attribute("offset")?.Value ?? "0");

                if (sem == "NORMAL") normalIdx = indices[i + off];
                else if (sem == "TEXCOORD") uvIdx = indices[i + off];
            }

            string key = $"{posIdx}_{normalIdx}_{uvIdx}";

            if (vertexMap.TryGetValue(key, out int existingIdx))
            {
                finalIndices.Add(existingIdx);
            }
            else
            {
                var vertex = new VertexPositionNormalTexture();

                if (posIdx * 3 + 2 < positions.Length)
                {
                    vertex.Position = new Vector3(
                        positions[posIdx * 3],
                        positions[posIdx * 3 + 1],
                        positions[posIdx * 3 + 2]
                    );
                }

                if (normals != null && normalIdx * 3 + 2 < normals.Length)
                {
                    vertex.Normal = new Vector3(
                        normals[normalIdx * 3],
                        normals[normalIdx * 3 + 1],
                        normals[normalIdx * 3 + 2]
                    );
                }

                if (uvs != null && uvIdx * 2 + 1 < uvs.Length)
                {
                    vertex.TextureCoordinate = new Vector2(
                        uvs[uvIdx * 2],
                        1f - uvs[uvIdx * 2 + 1]
                    );
                }

                int newIdx = vertices.Count;
                vertices.Add(vertex);
                vertexMap[key] = newIdx;
                finalIndices.Add(newIdx);
            }
        }

        return new DaeMesh
        {
            Name = geom.Attribute("id")?.Value ?? "mesh",
            Vertices = vertices.ToArray(),
            Indices = finalIndices.ToArray()
        };
    }

    private void CombineMeshes(GraphicsDevice graphics)
    {
        var allVertices = new List<VertexPositionNormalTexture>();
        var allIndices = new List<int>();

        foreach (var mesh in Meshes)
        {
            int baseIndex = allVertices.Count;
            allVertices.AddRange(mesh.Vertices);

            foreach (var idx in mesh.Indices)
            {
                allIndices.Add(baseIndex + idx);
            }
        }

        if (allVertices.Count == 0) return;

        VertexBuffer = new VertexBuffer(graphics, typeof(VertexPositionNormalTexture), allVertices.Count, BufferUsage.None);
        VertexBuffer.SetData(allVertices.ToArray());

        IndexBuffer = new IndexBuffer(graphics, IndexElementSize.ThirtyTwoBits, allIndices.Count, BufferUsage.None);
        IndexBuffer.SetData(allIndices.ToArray());

        PrimitiveCount = allIndices.Count / 3;
    }

    private static float[] ParseFloats(string value)
    {
        var parts = value.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]);
        }
        return result;
    }

    private static int[] ParseInts(string value)
    {
        var parts = value.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            int.TryParse(parts[i], out result[i]);
        }
        return result;
    }
}

public class DaeMesh
{
    public string Name { get; set; }
    public VertexPositionNormalTexture[] Vertices { get; set; }
    public int[] Indices { get; set; }
}
