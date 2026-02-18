using System.Text.Json;
using PokemonGreen.SwitchToolboxCli.App.Commands;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class BulkConvertCommandTests
{
    [Fact]
    public void BulkConvertCommand_CreatesCheckpoint_WithCompletedEntries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"bulk-checkpoint-{Guid.NewGuid():N}");
        var inputDir = Path.Combine(tempDir, "input");
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(Path.Combine(inputDir, "a"));
        File.WriteAllBytes(Path.Combine(inputDir, "alpha.trpak"), [0x01]);
        File.WriteAllBytes(Path.Combine(inputDir, "a", "beta.trpak"), [0x02]);

        try
        {
            var exitCode = BulkConvertCommand.Run(
                inputDir,
                outputDir,
                "obj",
                limit: null,
                resume: false,
                executeSingleConvert: (_, _, _) => new BulkConvertExecutionResult(0, null),
                stdout: TextWriter.Null,
                stderr: TextWriter.Null);

            Assert.Equal(0, exitCode);

            var checkpointPath = Path.Combine(outputDir, "bulk-convert-checkpoint.json");
            Assert.True(File.Exists(checkpointPath));

            var checkpoint = JsonSerializer.Deserialize<BulkConvertCheckpoint>(File.ReadAllText(checkpointPath));
            Assert.NotNull(checkpoint);
            Assert.Equal(2, checkpoint!.Entries.Count);
            Assert.All(checkpoint.Entries, entry => Assert.Equal("completed", entry.Status));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void BulkConvertCommand_WithResumeTrue_SkipsCompletedEntries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"bulk-resume-{Guid.NewGuid():N}");
        var inputDir = Path.Combine(tempDir, "input");
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(inputDir);
        var alphaPath = Path.GetFullPath(Path.Combine(inputDir, "alpha.trpak"));
        var betaPath = Path.GetFullPath(Path.Combine(inputDir, "beta.trpak"));
        File.WriteAllBytes(alphaPath, [0x01]);
        File.WriteAllBytes(betaPath, [0x02]);

        Directory.CreateDirectory(outputDir);
        var checkpointPath = Path.Combine(outputDir, "bulk-convert-checkpoint.json");
        var preloaded = new BulkConvertCheckpoint
        {
            InputDirectory = inputDir,
            OutputDirectory = outputDir,
            ModelFormat = "obj",
            LastUpdatedUtc = DateTimeOffset.UtcNow,
            Entries =
            [
                new BulkConvertCheckpointEntry
                {
                    InputPath = alphaPath,
                    RelativePath = "alpha.trpak",
                    OutputSubdirectory = Path.Combine("converted", "alpha.trpak"),
                    Status = "completed",
                    LastUpdatedUtc = DateTimeOffset.UtcNow,
                },
                new BulkConvertCheckpointEntry
                {
                    InputPath = betaPath,
                    RelativePath = "beta.trpak",
                    OutputSubdirectory = Path.Combine("converted", "beta.trpak"),
                    Status = "pending",
                    LastUpdatedUtc = DateTimeOffset.UtcNow,
                },
            ],
        };
        File.WriteAllText(checkpointPath, JsonSerializer.Serialize(preloaded));

        try
        {
            var executedInputs = new List<string>();
            var exitCode = BulkConvertCommand.Run(
                inputDir,
                outputDir,
                "obj",
                limit: null,
                resume: true,
                executeSingleConvert: (inputPath, _, _) =>
                {
                    executedInputs.Add(Path.GetFileName(inputPath));
                    return new BulkConvertExecutionResult(0, null);
                },
                stdout: TextWriter.Null,
                stderr: TextWriter.Null);

            Assert.Equal(0, exitCode);
            Assert.Equal(["beta.trpak"], executedInputs);

            var checkpoint = JsonSerializer.Deserialize<BulkConvertCheckpoint>(File.ReadAllText(checkpointPath));
            Assert.NotNull(checkpoint);
            Assert.All(checkpoint!.Entries, entry => Assert.Equal("completed", entry.Status));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void BulkConvertCommand_ContinuesOnError_AndReturnsFailureExitCode()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"bulk-continue-{Guid.NewGuid():N}");
        var inputDir = Path.Combine(tempDir, "input");
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(inputDir);
        File.WriteAllBytes(Path.Combine(inputDir, "alpha.trpak"), [0x01]);
        File.WriteAllBytes(Path.Combine(inputDir, "beta.trpak"), [0x02]);
        File.WriteAllBytes(Path.Combine(inputDir, "gamma.trpak"), [0x03]);

        try
        {
            var executedInputs = new List<string>();
            var stdout = new StringWriter();
            var exitCode = BulkConvertCommand.Run(
                inputDir,
                outputDir,
                "dae",
                limit: null,
                resume: false,
                executeSingleConvert: (inputPath, _, _) =>
                {
                    var fileName = Path.GetFileName(inputPath);
                    executedInputs.Add(fileName);
                    if (string.Equals(fileName, "beta.trpak", StringComparison.OrdinalIgnoreCase))
                    {
                        return new BulkConvertExecutionResult(9, "synthetic failure");
                    }

                    return new BulkConvertExecutionResult(0, null);
                },
                stdout: stdout,
                stderr: TextWriter.Null);

            Assert.Equal(1, exitCode);
            Assert.Equal(3, executedInputs.Count);
            Assert.Contains("failed: 1", stdout.ToString());

            var checkpointPath = Path.Combine(outputDir, "bulk-convert-checkpoint.json");
            var checkpoint = JsonSerializer.Deserialize<BulkConvertCheckpoint>(File.ReadAllText(checkpointPath));
            Assert.NotNull(checkpoint);

            var failedEntry = Assert.Single(checkpoint!.Entries.Where(entry => entry.Status == "failed"));
            Assert.Equal("beta.trpak", Path.GetFileName(failedEntry.InputPath));
            Assert.Equal("synthetic failure", failedEntry.ErrorMessage);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
