using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Textures;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class TextureExportFoundationTests
{
    [Theory]
    [MemberData(nameof(SignatureCases))]
    public void DetectsTextureExtensionFromSignature(byte[] bytes, string expectedExtension)
    {
        var detector = new TextureSignatureDetector();
        var detected = detector.DetectExtension(bytes);
        Assert.Equal(expectedExtension, detected);
    }

    [Fact]
    public void ExportsDeterministicPathsAndHandlesDuplicateNames()
    {
        var archive = new TestArchive(
        [
            CreateEntry("", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            CreateEntry("duplicate", [(byte)'D', (byte)'D', (byte)'S', (byte)' ']),
            CreateEntry("duplicate", [(byte)'D', (byte)'D', (byte)'S', (byte)' ']),
            CreateEntry("../unsafe/texture", [0xFF, 0xD8, 0xFF]),
            CreateEntry("known-name.jpeg", [0x00, 0x01, 0x02]),
            CreateEntry("not-texture", [0x00, 0x01, 0x02]),
        ]);

        var exportService = new TextureArchiveExportService();
        var source = new[] { new TextureArchiveSource("root/../pack.garc", archive) };

        var outputA = Path.Combine(Path.GetTempPath(), $"texture-export-a-{Guid.NewGuid():N}");
        var outputB = Path.Combine(Path.GetTempPath(), $"texture-export-b-{Guid.NewGuid():N}");

        try
        {
            var first = exportService.ExportTextureLikeEntries(source, outputA);
            var second = exportService.ExportTextureLikeEntries(source, outputB);

            Assert.Equal(first.Select(item => item.OutputRelativePath), second.Select(item => item.OutputRelativePath));
            Assert.Equal(5, first.Count);

            Assert.Contains(first, item => item.OutputRelativePath.EndsWith("entry_000.png", StringComparison.Ordinal));
            Assert.Contains(first, item => item.OutputRelativePath.EndsWith("duplicate.dds", StringComparison.Ordinal));
            Assert.Contains(first, item => item.OutputRelativePath.EndsWith("duplicate_001.dds", StringComparison.Ordinal));
            Assert.Contains(first, item => item.OutputRelativePath.EndsWith("unsafe/texture.jpg", StringComparison.Ordinal));
            Assert.Contains(first, item => item.OutputRelativePath.EndsWith("known-name.jpeg", StringComparison.Ordinal));
            Assert.DoesNotContain(first, item => item.OutputRelativePath.Contains("..", StringComparison.Ordinal));

            foreach (var export in first)
            {
                var fullPath = Path.Combine(outputA, export.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(fullPath));
            }
        }
        finally
        {
            if (Directory.Exists(outputA))
            {
                Directory.Delete(outputA, recursive: true);
            }

            if (Directory.Exists(outputB))
            {
                Directory.Delete(outputB, recursive: true);
            }
        }
    }

    public static IEnumerable<object[]> SignatureCases()
    {
        yield return [new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, ".png"];
        yield return [new byte[] { (byte)'D', (byte)'D', (byte)'S', (byte)' ' }, ".dds"];
        yield return [new byte[] { 0x13, 0xAB, 0xA1, 0x5C }, ".astc"];
        yield return [new byte[] { (byte)'B', (byte)'M', 0x10, 0x00 }, ".bmp"];
        yield return [new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, ".jpg"];
        yield return [new byte[] { (byte)'I', (byte)'I', 0x2A, 0x00 }, ".tif"];
        yield return [new byte[] { (byte)'M', (byte)'M', 0x00, 0x2A }, ".tif"];
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
