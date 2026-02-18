using PokemonGreen.SwitchToolboxCli.Core.Models;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Dae;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Obj;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class ExporterFoundationTests
{
    [Fact]
    public void ObjExporter_WritesTriangleMeshFile()
    {
        var model = BuildTriangleModel();
        var outputPath = Path.Combine(Path.GetTempPath(), $"obj-export-{Guid.NewGuid():N}.obj");

        try
        {
            var exporter = new ObjExporter();
            exporter.Export(outputPath, model);

            Assert.True(File.Exists(outputPath));

            var content = File.ReadAllText(outputPath);
            Assert.Contains("o TriangleMesh", content, StringComparison.Ordinal);
            Assert.Contains("v 0 0 0", content, StringComparison.Ordinal);
            Assert.Contains("f 1/1/1 2/2/2 3/3/3", content, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void DaeExporter_WritesTriangleMeshFile()
    {
        var model = BuildTriangleModel();
        var outputPath = Path.Combine(Path.GetTempPath(), $"dae-export-{Guid.NewGuid():N}.dae");

        try
        {
            var exporter = new DaeExporter();
            exporter.Export(outputPath, model);

            Assert.True(File.Exists(outputPath));

            var content = File.ReadAllText(outputPath);
            Assert.Contains("<COLLADA", content, StringComparison.Ordinal);
            Assert.Contains("<library_geometries>", content, StringComparison.Ordinal);
            Assert.Contains("geometry id=\"TriangleMesh-geometry\"", content, StringComparison.Ordinal);
            Assert.Contains("<triangles count=\"1\">", content, StringComparison.Ordinal);
            Assert.Contains("<p>0 0 0 1 1 1 2 2 2</p>", content, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    private static ExportModel BuildTriangleModel()
    {
        return new ExportModel
        {
            ModelName = "TriangleModel",
            Meshes =
            [
                new ExportMesh
                {
                    MeshName = "TriangleMesh",
                    Vertices =
                    [
                        new ExportVector3(0f, 0f, 0f),
                        new ExportVector3(1f, 0f, 0f),
                        new ExportVector3(0f, 1f, 0f),
                    ],
                    Normals =
                    [
                        new ExportVector3(0f, 0f, 1f),
                        new ExportVector3(0f, 0f, 1f),
                        new ExportVector3(0f, 0f, 1f),
                    ],
                    Uvs =
                    [
                        new ExportVector2(0f, 0f),
                        new ExportVector2(1f, 0f),
                        new ExportVector2(0f, 1f),
                    ],
                    Indices = [0, 1, 2],
                },
            ],
        };
    }
}
