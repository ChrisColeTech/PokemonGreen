using System.Text.Json;
using PokemonGreen.SwitchToolboxCli.App.Commands;

namespace PokemonGreen.SwitchToolboxCli.Tests;

public class BulkExtractBinsCommandTests
{
    [Fact]
    public void BulkExtractBinsCommand_CreatesCheckpoint_WithCompletedEntries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"bulk-extract-checkpoint-{Guid.NewGuid():N}");
        var inputDir = Path.Combine(tempDir, "input");
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(Path.Combine(inputDir, "a"));
        File.WriteAllBytes(Path.Combine(inputDir, "alpha.trpak"), [0x01]);
        File.WriteAllBytes(Path.Combine(inputDir, "a", "beta.trpak"), [0x02]);

        try
        {
            var outputTargets = new List<string>();
            var exitCode = BulkExtractBinsCommand.Run(
                inputDir,
                outputDir,
                limit: null,
                resume: false,
                executeSingleExtract: (_, perFileOutputDirectory) =>
                {
                    outputTargets.Add(Path.GetRelativePath(outputDir, perFileOutputDirectory).Replace('\\', '/'));
                    return new BulkExtractBinsExecutionResult(0, null);
                },
                stdout: TextWriter.Null,
                stderr: TextWriter.Null);

            Assert.Equal(0, exitCode);
            Assert.Equal(["extracted", "extracted/a"], outputTargets.OrderBy(item => item, StringComparer.Ordinal).ToList());

            var checkpointPath = Path.Combine(outputDir, "bulk-extract-bins-checkpoint.json");
            Assert.True(File.Exists(checkpointPath));

            var checkpoint = JsonSerializer.Deserialize<BulkExtractBinsCheckpoint>(File.ReadAllText(checkpointPath));
            Assert.NotNull(checkpoint);
            Assert.Equal(2, checkpoint!.Entries.Count);
            Assert.All(checkpoint.Entries, entry => Assert.Equal("completed", entry.Status));

            var alphaEntry = Assert.Single(checkpoint.Entries.Where(entry => Path.GetFileName(entry.InputPath) == "alpha.trpak"));
            Assert.Equal("extracted", alphaEntry.OutputSubdirectory.Replace('\\', '/'));

            var betaEntry = Assert.Single(checkpoint.Entries.Where(entry => Path.GetFileName(entry.InputPath) == "beta.trpak"));
            Assert.Equal("extracted/a", betaEntry.OutputSubdirectory.Replace('\\', '/'));
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
    public void BulkExtractBinsCommand_WithResumeTrue_SkipsCompletedEntries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"bulk-extract-resume-{Guid.NewGuid():N}");
        var inputDir = Path.Combine(tempDir, "input");
        var outputDir = Path.Combine(tempDir, "out");

        Directory.CreateDirectory(inputDir);
        var alphaPath = Path.GetFullPath(Path.Combine(inputDir, "alpha.trpak"));
        var betaPath = Path.GetFullPath(Path.Combine(inputDir, "beta.trpak"));
        File.WriteAllBytes(alphaPath, [0x01]);
        File.WriteAllBytes(betaPath, [0x02]);

        Directory.CreateDirectory(outputDir);
        var checkpointPath = Path.Combine(outputDir, "bulk-extract-bins-checkpoint.json");
        var preloaded = new BulkExtractBinsCheckpoint
        {
            InputDirectory = inputDir,
            OutputDirectory = outputDir,
            LastUpdatedUtc = DateTimeOffset.UtcNow,
            Entries =
            [
                new BulkExtractBinsCheckpointEntry
                {
                    InputPath = alphaPath,
                    RelativePath = "alpha.trpak",
                    OutputSubdirectory = "extracted",
                    Status = "completed",
                    LastUpdatedUtc = DateTimeOffset.UtcNow,
                },
                new BulkExtractBinsCheckpointEntry
                {
                    InputPath = betaPath,
                    RelativePath = "beta.trpak",
                    OutputSubdirectory = "extracted",
                    Status = "pending",
                    LastUpdatedUtc = DateTimeOffset.UtcNow,
                },
            ],
        };
        File.WriteAllText(checkpointPath, JsonSerializer.Serialize(preloaded));

        try
        {
            var executedInputs = new List<string>();
            var exitCode = BulkExtractBinsCommand.Run(
                inputDir,
                outputDir,
                limit: null,
                resume: true,
                executeSingleExtract: (inputPath, _) =>
                {
                    executedInputs.Add(Path.GetFileName(inputPath));
                    return new BulkExtractBinsExecutionResult(0, null);
                },
                stdout: TextWriter.Null,
                stderr: TextWriter.Null);

            Assert.Equal(0, exitCode);
            Assert.Equal(["beta.trpak"], executedInputs);

            var checkpoint = JsonSerializer.Deserialize<BulkExtractBinsCheckpoint>(File.ReadAllText(checkpointPath));
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
    public void BulkExtractBinsCommand_ContinuesOnError_AndReturnsFailureExitCode()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"bulk-extract-continue-{Guid.NewGuid():N}");
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
            var exitCode = BulkExtractBinsCommand.Run(
                inputDir,
                outputDir,
                limit: null,
                resume: false,
                executeSingleExtract: (inputPath, _) =>
                {
                    var fileName = Path.GetFileName(inputPath);
                    executedInputs.Add(fileName);
                    if (string.Equals(fileName, "beta.trpak", StringComparison.OrdinalIgnoreCase))
                    {
                        return new BulkExtractBinsExecutionResult(7, "synthetic extract failure");
                    }

                    return new BulkExtractBinsExecutionResult(0, null);
                },
                stdout: stdout,
                stderr: TextWriter.Null);

            Assert.Equal(1, exitCode);
            Assert.Equal(3, executedInputs.Count);
            Assert.Contains("failed: 1", stdout.ToString());

            var checkpointPath = Path.Combine(outputDir, "bulk-extract-bins-checkpoint.json");
            var checkpoint = JsonSerializer.Deserialize<BulkExtractBinsCheckpoint>(File.ReadAllText(checkpointPath));
            Assert.NotNull(checkpoint);

            var failedEntry = Assert.Single(checkpoint!.Entries.Where(entry => entry.Status == "failed"));
            Assert.Equal("beta.trpak", Path.GetFileName(failedEntry.InputPath));
            Assert.Equal("synthetic extract failure", failedEntry.ErrorMessage);
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
