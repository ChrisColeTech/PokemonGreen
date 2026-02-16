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

    public void Draw(GraphicsDevice device, Effect effect)
    {
        var alphaTest = effect as AlphaTestEffect;

        foreach (var mesh in Meshes)
        {
            if (mesh.Texture != null)
            {
                if (alphaTest != null)
                    alphaTest.Texture = mesh.Texture;
                else
                    effect.Parameters["Texture"]?.SetValue(mesh.Texture);
            }

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
            PostProcessSteps.FlipUVs |
            PostProcessSteps.PreTransformVertices);

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
                string? texturePath = null;

                // Try Assimp's standard diffuse texture path
                if (material.HasTextureDiffuse && !string.IsNullOrEmpty(material.TextureDiffuse.FilePath))
                {
                    var candidate = Path.Combine(directory, material.TextureDiffuse.FilePath);
                    if (File.Exists(candidate))
                        texturePath = candidate;
                }

                // Fallback: match material name to texture files on disk
                // Ohana3DS exports DAE with empty library_images; textures are loose PNGs
                if (texturePath == null && !string.IsNullOrEmpty(material.Name))
                {
                    string matName = material.Name.Replace("_mat", "");
                    string folderName = Path.GetFileName(directory) ?? "";
                    texturePath = FindTextureForMaterial(directory, folderName, matName);
                }

                if (texturePath != null)
                {
                    using var stream = File.OpenRead(texturePath);
                    texture = Texture2D.FromStream(graphicsDevice, stream);
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

    /// <summary>
    /// Match a material name to a texture file on disk.
    /// Ohana3DS exports textures as loose PNGs but doesn't reference them in the DAE.
    /// Naming conventions:
    ///   Material "Body00" → texture "pm0004_00_Body1.tga.png" (index 0 → suffix 1)
    ///   Material "Body01" → texture "pm0004_00_Body2.tga.png" (index 1 → suffix 2)
    ///   Material "Eye"    → texture "pm0004_00_Eye1.tga.png"
    ///   Material "LEye"   → texture "pm0025_00_Eye1.tga.png"  (L/R prefix stripped)
    /// </summary>
    private static string? FindTextureForMaterial(string directory, string folderName, string matName)
    {
        // Split material name into base + trailing digits: Body00 → ("Body", "00"), Eye → ("Eye", "")
        string baseName = matName.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        string digitStr = matName.Substring(baseName.Length);
        if (string.IsNullOrEmpty(baseName))
            baseName = matName;

        // Material digit index → texture file suffix (0-based → 1-based)
        int texSuffix = 1;
        if (digitStr.Length > 0 && int.TryParse(digitStr, out int matIndex))
            texSuffix = matIndex + 1;

        // 1. Exact match with folder prefix + texture number: pm0004_00_Body1*.png
        var match = SearchTexture(directory, $"{folderName}_{baseName}{texSuffix}");
        if (match != null) return match;

        // 2. Folder prefix + base name (any suffix): pm0004_00_Body*.png
        match = SearchTexture(directory, $"{folderName}_{baseName}");
        if (match != null) return match;

        // 3. Generic name with texture number: FireStenA1*.png
        match = SearchTexture(directory, $"{baseName}{texSuffix}");
        if (match != null) return match;

        // 4. Generic base name: FireStenA*.png
        match = SearchTexture(directory, $"{baseName}");
        if (match != null) return match;

        // 5. Handle L/R prefixed eye materials: LEye → Eye, REye → Eye
        if (baseName.Length > 1 && (baseName[0] == 'L' || baseName[0] == 'R') && char.IsUpper(baseName[1]))
        {
            string stripped = baseName.Substring(1);
            match = SearchTexture(directory, $"{folderName}_{stripped}{texSuffix}");
            if (match != null) return match;
            match = SearchTexture(directory, $"{folderName}_{stripped}");
            if (match != null) return match;
            match = SearchTexture(directory, $"{stripped}");
            if (match != null) return match;
        }

        // 6. Exact material name as prefix
        match = SearchTexture(directory, matName);
        return match;
    }

    private static string? SearchTexture(string directory, string prefix)
    {
        foreach (var file in Directory.EnumerateFiles(directory, $"{prefix}*.png"))
        {
            string name = Path.GetFileName(file);
            if (name.Contains("Nor", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Mask", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Dummy", StringComparison.OrdinalIgnoreCase))
                continue;
            return file;
        }
        return null;
    }
}
