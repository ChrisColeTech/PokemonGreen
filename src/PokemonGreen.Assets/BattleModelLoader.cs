#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Assimp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokemonGreen.Assets;

/// <summary>
/// A single mesh ready for GPU rendering.
/// </summary>
public class BattleMeshData : IDisposable
{
    public VertexBuffer VertexBuffer { get; }
    public IndexBuffer IndexBuffer { get; }
    public Texture2D? Texture { get; }
    public int PrimitiveCount { get; }

    public BattleMeshData(GraphicsDevice device, VertexPositionNormalTexture[] vertices, int[] indices, Texture2D? texture)
    {
        VertexBuffer = new VertexBuffer(device, typeof(VertexPositionNormalTexture), vertices.Length, BufferUsage.WriteOnly);
        VertexBuffer.SetData(vertices);

        IndexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
        IndexBuffer.SetData(indices);

        Texture = texture;
        PrimitiveCount = indices.Length / 3;
    }

    public void Dispose()
    {
        VertexBuffer.Dispose();
        IndexBuffer.Dispose();
        Texture?.Dispose();
    }
}

/// <summary>
/// A complete loaded model (background or platform) with all its meshes.
/// </summary>
public class BattleModelData : IDisposable
{
    public List<BattleMeshData> Meshes { get; } = new();
    public int TotalVertices { get; set; }
    public int TotalIndices { get; set; }
    public int TexturedMeshCount { get; set; }
    public Vector3 BoundsMin { get; set; } = new(float.MaxValue);
    public Vector3 BoundsMax { get; set; } = new(float.MinValue);

    public void Draw(GraphicsDevice device, BasicEffect effect)
    {
        foreach (var mesh in Meshes)
        {
            if (mesh.Texture != null)
                effect.Texture = mesh.Texture;

            device.SetVertexBuffer(mesh.VertexBuffer);
            device.Indices = mesh.IndexBuffer;

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawIndexedPrimitives(Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount);
            }
        }
    }

    public void Dispose()
    {
        foreach (var mesh in Meshes)
            mesh.Dispose();
    }
}

/// <summary>
/// Loads .dae (Collada) 3D models using AssimpNet for battle scene rendering.
/// </summary>
public static class BattleModelLoader
{
    public static BattleModelData Load(string daeFilePath, GraphicsDevice graphicsDevice)
    {
        var result = new BattleModelData();
        string directory = Path.GetDirectoryName(daeFilePath) ?? "";

        using var importer = new AssimpContext();
        var scene = importer.ImportFile(daeFilePath,
            PostProcessSteps.Triangulate |
            PostProcessSteps.GenerateSmoothNormals |
            PostProcessSteps.FlipUVs);

        if (scene == null || scene.SceneFlags.HasFlag(SceneFlags.Incomplete))
            throw new InvalidOperationException($"Failed to load model: {daeFilePath}");

        for (int m = 0; m < scene.MeshCount; m++)
        {
            var mesh = scene.Meshes[m];

            // Extract vertices
            var vertices = new VertexPositionNormalTexture[mesh.VertexCount];
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                var pos = mesh.Vertices[i];
                var normal = mesh.HasNormals ? mesh.Normals[i] : new Vector3D(0, 1, 0);
                var uv = mesh.HasTextureCoords(0) ? mesh.TextureCoordinateChannels[0][i] : new Vector3D(0, 0, 0);

                vertices[i] = new VertexPositionNormalTexture(
                    new Vector3(pos.X, pos.Y, pos.Z),
                    new Vector3(normal.X, normal.Y, normal.Z),
                    new Vector2(uv.X, uv.Y));
            }

            // Extract indices
            var indices = new List<int>();
            for (int f = 0; f < mesh.FaceCount; f++)
            {
                var face = mesh.Faces[f];
                for (int fi = 0; fi < face.IndexCount; fi++)
                    indices.Add(face.Indices[fi]);
            }

            // Load texture from material
            Texture2D? texture = null;
            if (mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.MaterialCount)
            {
                var material = scene.Materials[mesh.MaterialIndex];
                if (material.HasTextureDiffuse)
                {
                    string texturePath = Path.Combine(directory, material.TextureDiffuse.FilePath);
                    if (File.Exists(texturePath))
                    {
                        using var stream = File.OpenRead(texturePath);
                        texture = Texture2D.FromStream(graphicsDevice, stream);
                    }
                }
            }

            // Track bounds from CPU-side vertex data before GPU upload
            foreach (var v in vertices)
            {
                if (v.Position.X < result.BoundsMin.X) result.BoundsMin = new Vector3(v.Position.X, result.BoundsMin.Y, result.BoundsMin.Z);
                if (v.Position.Y < result.BoundsMin.Y) result.BoundsMin = new Vector3(result.BoundsMin.X, v.Position.Y, result.BoundsMin.Z);
                if (v.Position.Z < result.BoundsMin.Z) result.BoundsMin = new Vector3(result.BoundsMin.X, result.BoundsMin.Y, v.Position.Z);
                if (v.Position.X > result.BoundsMax.X) result.BoundsMax = new Vector3(v.Position.X, result.BoundsMax.Y, result.BoundsMax.Z);
                if (v.Position.Y > result.BoundsMax.Y) result.BoundsMax = new Vector3(result.BoundsMax.X, v.Position.Y, result.BoundsMax.Z);
                if (v.Position.Z > result.BoundsMax.Z) result.BoundsMax = new Vector3(result.BoundsMax.X, result.BoundsMax.Y, v.Position.Z);
            }

            if (texture != null) result.TexturedMeshCount++;

            var meshData = new BattleMeshData(graphicsDevice, vertices, indices.ToArray(), texture);
            result.Meshes.Add(meshData);
            result.TotalVertices += vertices.Length;
            result.TotalIndices += indices.Count;
        }

        return result;
    }
}
