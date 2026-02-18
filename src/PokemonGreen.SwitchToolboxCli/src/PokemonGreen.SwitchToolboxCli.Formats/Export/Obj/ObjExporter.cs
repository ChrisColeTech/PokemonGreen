using PokemonGreen.SwitchToolboxCli.Core.Models;
using System.Globalization;
using System.Text;

namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Obj;

public sealed class ObjExporter
{
    public void Export(string filePath, ExportModel model)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(model);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var writer = new StringBuilder();
        writer.AppendLine($"# OBJ export for {model.ModelName}");

        var vertexBaseIndex = 1;
        foreach (var mesh in model.Meshes)
        {
            WriteMesh(writer, mesh, vertexBaseIndex);
            vertexBaseIndex += mesh.Vertices.Count;
        }

        File.WriteAllText(filePath, writer.ToString(), Encoding.UTF8);
    }

    private static void WriteMesh(StringBuilder writer, ExportMesh mesh, int vertexBaseIndex)
    {
        writer.AppendLine($"o {mesh.MeshName}");
        writer.AppendLine($"g {mesh.MeshName}");

        foreach (var vertex in mesh.Vertices)
        {
            writer.AppendLine($"v {Format(vertex.X)} {Format(vertex.Y)} {Format(vertex.Z)}");
        }

        if (mesh.HasUvs)
        {
            foreach (var uv in mesh.Uvs)
            {
                writer.AppendLine($"vt {Format(uv.X)} {Format(uv.Y)}");
            }
        }

        if (mesh.HasNormals)
        {
            foreach (var normal in mesh.Normals)
            {
                writer.AppendLine($"vn {Format(normal.X)} {Format(normal.Y)} {Format(normal.Z)}");
            }
        }

        for (var i = 0; i <= mesh.Indices.Count - 3; i += 3)
        {
            var a = mesh.Indices[i] + vertexBaseIndex;
            var b = mesh.Indices[i + 1] + vertexBaseIndex;
            var c = mesh.Indices[i + 2] + vertexBaseIndex;

            if (mesh.HasUvs && mesh.HasNormals)
            {
                writer.AppendLine($"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}");
            }
            else if (mesh.HasUvs)
            {
                writer.AppendLine($"f {a}/{a} {b}/{b} {c}/{c}");
            }
            else if (mesh.HasNormals)
            {
                writer.AppendLine($"f {a}//{a} {b}//{b} {c}//{c}");
            }
            else
            {
                writer.AppendLine($"f {a} {b} {c}");
            }
        }
    }

    private static string Format(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
