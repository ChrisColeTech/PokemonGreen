using System.Text;
using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Models;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class ModelExportFoundationTests
{
    [Fact]
    public void JsonModel_ExportsToObj_WhenRequestedFormatIsObj()
    {
        var archive = new TestArchive(
        [
            CreateEntry("model_payload.json", Encoding.UTF8.GetBytes(CreateJsonModelPayload())),
        ]);

        var outputDir = Path.Combine(Path.GetTempPath(), $"model-export-obj-{Guid.NewGuid():N}");

        try
        {
            var service = new ModelArchiveExportService();
            var exported = service.ExportModelLikeEntries([
                new ModelArchiveSource("root/sample.garc", archive),
            ], outputDir, "obj");

            Assert.Single(exported);
            Assert.Equal("root/sample.garc/model_payload.obj", exported[0].OutputRelativePath);
            Assert.Equal("converted", exported[0].ExportKind);

            var filePath = Path.Combine(outputDir, exported[0].OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(filePath));

            var content = File.ReadAllText(filePath);
            Assert.Contains("o Main", content, StringComparison.Ordinal);
            Assert.Contains("f 1/1/1 2/2/2 3/3/3", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void JsonModel_ExportsToDae_WhenRequestedFormatIsDae()
    {
        var archive = new TestArchive(
        [
            CreateEntry("model_payload.bin", Encoding.UTF8.GetBytes(CreateJsonModelPayload())),
        ]);

        var outputDir = Path.Combine(Path.GetTempPath(), $"model-export-dae-{Guid.NewGuid():N}");

        try
        {
            var service = new ModelArchiveExportService();
            var exported = service.ExportModelLikeEntries([
                new ModelArchiveSource("sample.garc", archive),
            ], outputDir, "dae");

            Assert.Single(exported);
            Assert.Equal("sample.garc/model_payload.dae", exported[0].OutputRelativePath);
            Assert.Equal("converted", exported[0].ExportKind);

            var filePath = Path.Combine(outputDir, exported[0].OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(filePath));

            var content = File.ReadAllText(filePath);
            Assert.Contains("<COLLADA", content, StringComparison.Ordinal);
            Assert.Contains("geometry id=\"Main-geometry\"", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ExistingObjAndDaeFiles_AreCopiedToModelsFolder()
    {
        var archive = new TestArchive(
        [
            CreateEntry("existing/model.obj", Encoding.UTF8.GetBytes("o Existing\nv 0 0 0\n")),
            CreateEntry("../unsafe/model.dae", Encoding.UTF8.GetBytes("<COLLADA></COLLADA>")),
        ]);

        var outputDir = Path.Combine(Path.GetTempPath(), $"model-export-pass-{Guid.NewGuid():N}");

        try
        {
            var service = new ModelArchiveExportService();
            var exported = service.ExportModelLikeEntries([
                new ModelArchiveSource("root/../sample.garc", archive),
            ], outputDir, "obj");

            Assert.Equal(2, exported.Count);
            Assert.Contains(exported, item => item.OutputRelativePath.EndsWith("existing/model.obj", StringComparison.Ordinal));
            Assert.Contains(exported, item => item.OutputRelativePath.EndsWith("unsafe/model.dae", StringComparison.Ordinal));
            Assert.All(exported, item => Assert.Equal("pass-through", item.ExportKind));
            Assert.DoesNotContain(exported, item => item.OutputRelativePath.Contains("..", StringComparison.Ordinal));

            foreach (var item in exported)
            {
                var fullPath = Path.Combine(outputDir, item.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(fullPath));
            }
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    private static string CreateJsonModelPayload()
    {
        return "{\"modelName\":\"JsonModel\",\"meshes\":[{\"meshName\":\"Main\",\"vertices\":[[0,0,0],[1,0,0],[0,1,0]],\"normals\":[[0,0,1],[0,0,1],[0,0,1]],\"uvs\":[[0,0],[1,0],[0,1]],\"indices\":[0,1,2]}]}";
    }

    private static ArchiveEntry CreateEntry(string fileName, byte[] data)
    {
        return new ArchiveEntry
        {
            FileName = fileName,
            OpenRead = () => new MemoryStream(data, writable: false),
        };
    }

    private sealed class TestArchive : IArchiveFile
    {
        private readonly IReadOnlyList<ArchiveEntry> _files;

        public TestArchive(IReadOnlyList<ArchiveEntry> files)
        {
            _files = files;
        }

        public IEnumerable<ArchiveEntry> Files => _files;
    }
}
