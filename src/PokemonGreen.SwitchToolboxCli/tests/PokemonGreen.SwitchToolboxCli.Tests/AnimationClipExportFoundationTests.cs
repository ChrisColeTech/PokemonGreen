using System.Text;
using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Animations;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class AnimationClipExportFoundationTests
{
    [Theory]
    [MemberData(nameof(SignatureCases))]
    public void DetectsClipExtensionFromSignatures(byte[] bytes, string expectedExtension)
    {
        var detector = new AnimationClipSignatureDetector();
        var detected = detector.DetectFromSignature(bytes);
        Assert.Equal(expectedExtension, detected);
    }

    [Fact]
    public void JsonIsOnlyDetectedWhenLikelyAnimationPayload()
    {
        var detector = new AnimationClipSignatureDetector();
        var animationJson = Encoding.UTF8.GetBytes("{\"animation\":\"walk\",\"bones\":[],\"frames\":12}");
        var nonAnimationJson = Encoding.UTF8.GetBytes("{\"name\":\"pikachu\",\"hp\":35}");

        Assert.True(detector.TryGetClipExtension("clip.json", animationJson, out var animationExtension));
        Assert.Equal(".json", animationExtension);

        Assert.False(detector.TryGetClipExtension("data.json", nonAnimationJson, out _));
    }

    [Fact]
    public void ExportsDeterministicPathsAndHandlesDuplicateNames()
    {
        var archive = new TestArchive(
        [
            CreateEntry("", [(byte)'S', (byte)'E', (byte)'A', (byte)'N', (byte)'I', (byte)'M']),
            CreateEntry("duplicate", [(byte)'A', (byte)'N', (byte)'I', (byte)'M', 0x00]),
            CreateEntry("duplicate", [(byte)'A', (byte)'N', (byte)'I', (byte)'M', 0x01]),
            CreateEntry("../unsafe/clip", [(byte)'C', (byte)'H', (byte)'R', (byte)'0', 0x00]),
            CreateEntry("anim_payload", Encoding.UTF8.GetBytes("{\"tracks\":[],\"bones\":[],\"frames\":20}")),
            CreateEntry("controls/motion_ctrl.tracm", [0x11, 0x22, 0x33]),
            CreateEntry("controls/state_ctrl.tracs", [0x44, 0x55, 0x66]),
            CreateEntry("controls/layer_ctrl.tracl", [0x77, 0x88, 0x99]),
            CreateEntry("controls/route_ctrl.tracr", [0x10, 0x20, 0x30]),
            CreateEntry("controls/param_ctrl.tracp", [0x40, 0x50, 0x60]),
            CreateEntry("note.txt", [0x00, 0x01, 0x02]),
        ]);

        var exportService = new AnimationClipArchiveExportService();
        var source = new[] { new AnimationClipArchiveSource("root/../pack.garc", archive) };

        var outputA = Path.Combine(Path.GetTempPath(), $"clip-export-a-{Guid.NewGuid():N}");
        var outputB = Path.Combine(Path.GetTempPath(), $"clip-export-b-{Guid.NewGuid():N}");

        try
        {
            var first = exportService.ExportClipLikeEntries(source, outputA);
            var second = exportService.ExportClipLikeEntries(source, outputB);

            Assert.Equal(first.Select(item => item.OutputRelativePath), second.Select(item => item.OutputRelativePath));
            Assert.Equal(10, first.Count);

            Assert.Contains(first, item => item.OutputRelativePath.EndsWith("clip_000.seanim", StringComparison.Ordinal));
            Assert.Contains(first, item => item.OutputRelativePath.EndsWith("duplicate.anim", StringComparison.Ordinal));
            Assert.Contains(first, item => item.OutputRelativePath.EndsWith("duplicate_001.anim", StringComparison.Ordinal));
            Assert.Contains(first, item => item.OutputRelativePath.EndsWith("unsafe/clip.chr0", StringComparison.Ordinal));
            Assert.Contains(first, item => item.OutputRelativePath.EndsWith("anim_payload.json", StringComparison.Ordinal));
            Assert.Contains(first, item => item.OutputRelativePath.EndsWith("controls/motion_ctrl.tracm", StringComparison.Ordinal));
            Assert.Contains(first, item => item.OutputRelativePath.EndsWith("controls/state_ctrl.tracs", StringComparison.Ordinal));
            Assert.Contains(first, item => item.OutputRelativePath.EndsWith("controls/layer_ctrl.tracl", StringComparison.Ordinal));
            Assert.Contains(first, item => item.OutputRelativePath.EndsWith("controls/route_ctrl.tracr", StringComparison.Ordinal));
            Assert.Contains(first, item => item.OutputRelativePath.EndsWith("controls/param_ctrl.tracp", StringComparison.Ordinal));
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
        yield return [new byte[] { (byte)'C', (byte)'H', (byte)'R', (byte)'0', 0x01 }, ".chr0"];
        yield return [new byte[] { (byte)'B', (byte)'F', (byte)'S', (byte)'K', (byte)'A' }, ".bfska"];
        yield return [new byte[] { (byte)'S', (byte)'E', (byte)'A', (byte)'n', (byte)'i', (byte)'m' }, ".seanim"];
        yield return [new byte[] { (byte)'A', (byte)'N', (byte)'I', (byte)'M', 0x00 }, ".anim"];
        yield return [Encoding.UTF8.GetBytes("version 1\nnodes\n0 \"root\" -1\nend\nskeleton\ntime 0\n0 0 0 0 0 0 0\nend\n"), ".smd"];
        yield return [Encoding.UTF8.GetBytes("{\"animation\":\"run\",\"bones\":[],\"frames\":10}"), ".json"];
    }

    [Fact]
    public void GfbanmExtensionIsRecognizedAsClip()
    {
        var detector = new AnimationClipSignatureDetector();
        Assert.True(detector.TryGetClipExtension("walk.gfbanm", [0x00, 0x01], out var extension));
        Assert.Equal(".gfbanm", extension);
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
