using System.Text.Json;
using PokemonGreen.SwitchToolboxCli.App.Services;
using PokemonGreen.SwitchToolboxCli.Core.Extraction;
using PokemonGreen.SwitchToolboxCli.Core.Serialization;

namespace PokemonGreen.SwitchToolboxCli.App.Commands;

public static class BulkExtractBinsCommand
{
    private const string CompletedStatus = "completed";
    private const string FailedStatus = "failed";
    private const string PendingStatus = "pending";
    private const string RunningStatus = "running";
    private const string CheckpointFileName = "bulk-extract-bins-checkpoint.json";

    public static int Run(
        DiscoveryService discovery,
        ArchiveExtractionService extractionService,
        string inputDirectory,
        string outputDirectory,
        int? limit,
        bool resume,
        TextWriter stdout,
        TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(extractionService);

        return Run(
            inputDirectory,
            outputDirectory,
            limit,
            resume,
            (filePath, perFileOutputDirectory) =>
            {
                var extractionErrors = new StringWriter();
                var exitCode = ExtractBinsCommand.Run(
                    discovery,
                    extractionService,
                    filePath,
                    perFileOutputDirectory,
                    stdout: TextWriter.Null,
                    stderr: extractionErrors);

                var errorMessage = exitCode == 0
                    ? null
                    : (string.IsNullOrWhiteSpace(extractionErrors.ToString())
                        ? $"Extract-bins command failed with exit code {exitCode}."
                        : extractionErrors.ToString().Trim());

                return new BulkExtractBinsExecutionResult(exitCode, errorMessage);
            },
            stdout,
            stderr);
    }

    public static int Run(
        string inputDirectory,
        string outputDirectory,
        int? limit,
        bool resume,
        Func<string, string, BulkExtractBinsExecutionResult> executeSingleExtract,
        TextWriter stdout,
        TextWriter stderr)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(executeSingleExtract);

        if (!Directory.Exists(inputDirectory))
        {
            stderr.WriteLine($"Input directory does not exist: {inputDirectory}");
            return 1;
        }

        if (limit is <= 0)
        {
            stderr.WriteLine("Invalid value for --limit. Expected a positive integer.");
            return 1;
        }

        Directory.CreateDirectory(outputDirectory);
        var checkpointPath = Path.Combine(outputDirectory, CheckpointFileName);

        var discoveredFiles = Directory.EnumerateFiles(inputDirectory, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".trpak", StringComparison.OrdinalIgnoreCase))
            .Select(path => new BulkExtractBinsDiscoveredFile(
                Path.GetFullPath(path),
                Path.GetRelativePath(inputDirectory, path).Replace('\\', '/')))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToList();

        var checkpoint = LoadOrCreateCheckpoint(checkpointPath, inputDirectory, outputDirectory, discoveredFiles, resume, stderr);
        if (checkpoint is null)
        {
            return 1;
        }

        WriteCheckpoint(checkpointPath, checkpoint);

        var entriesByInputPath = checkpoint.Entries
            .ToDictionary(entry => entry.InputPath, StringComparer.OrdinalIgnoreCase);

        var skippedCompleted = 0;
        var pendingWork = new List<BulkExtractBinsCheckpointEntry>();
        foreach (var discovered in discoveredFiles)
        {
            if (!entriesByInputPath.TryGetValue(discovered.InputPath, out var entry))
            {
                continue;
            }

            if (resume && string.Equals(entry.Status, CompletedStatus, StringComparison.OrdinalIgnoreCase))
            {
                skippedCompleted++;
                continue;
            }

            pendingWork.Add(entry);
        }

        if (limit.HasValue)
        {
            pendingWork = pendingWork.Take(limit.Value).ToList();
        }

        foreach (var entry in pendingWork)
        {
            entry.Status = RunningStatus;
            entry.ErrorMessage = null;
            entry.LastUpdatedUtc = DateTimeOffset.UtcNow;
            WriteCheckpoint(checkpointPath, checkpoint);

            var result = executeSingleExtract(entry.InputPath, Path.Combine(outputDirectory, entry.OutputSubdirectory));
            entry.LastUpdatedUtc = DateTimeOffset.UtcNow;
            if (result.ExitCode == 0)
            {
                entry.Status = CompletedStatus;
                entry.ErrorMessage = null;
            }
            else
            {
                entry.Status = FailedStatus;
                entry.ErrorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? $"Extract-bins command failed with exit code {result.ExitCode}."
                    : result.ErrorMessage;
            }

            WriteCheckpoint(checkpointPath, checkpoint);
        }

        var finalEntries = discoveredFiles
            .Select(item => entriesByInputPath[item.InputPath])
            .ToList();

        var completedCount = finalEntries.Count(item => string.Equals(item.Status, CompletedStatus, StringComparison.OrdinalIgnoreCase));
        var failedCount = finalEntries.Count(item => string.Equals(item.Status, FailedStatus, StringComparison.OrdinalIgnoreCase));
        var runningCount = finalEntries.Count(item => string.Equals(item.Status, RunningStatus, StringComparison.OrdinalIgnoreCase));
        var pendingCount = finalEntries.Count(item => string.Equals(item.Status, PendingStatus, StringComparison.OrdinalIgnoreCase));

        stdout.WriteLine(
            $"Bulk extract-bins complete. Total: {finalEntries.Count}, completed: {completedCount}, failed: {failedCount}, pending: {pendingCount}, running: {runningCount}, skipped: {skippedCompleted}. Checkpoint: {Path.GetFullPath(checkpointPath)}");

        return failedCount > 0 ? 1 : 0;
    }

    private static BulkExtractBinsCheckpoint? LoadOrCreateCheckpoint(
        string checkpointPath,
        string inputDirectory,
        string outputDirectory,
        IReadOnlyList<BulkExtractBinsDiscoveredFile> discoveredFiles,
        bool resume,
        TextWriter stderr)
    {
        var checkpoint = new BulkExtractBinsCheckpoint
        {
            InputDirectory = Path.GetFullPath(inputDirectory),
            OutputDirectory = Path.GetFullPath(outputDirectory),
            LastUpdatedUtc = DateTimeOffset.UtcNow,
            Entries = new List<BulkExtractBinsCheckpointEntry>(),
        };

        if (File.Exists(checkpointPath))
        {
            try
            {
                var content = File.ReadAllText(checkpointPath);
                checkpoint = JsonSerializer.Deserialize<BulkExtractBinsCheckpoint>(content) ?? checkpoint;
            }
            catch (Exception ex)
            {
                stderr.WriteLine($"Failed to read checkpoint file '{checkpointPath}': {ex.Message}");
                return null;
            }

            checkpoint.InputDirectory = Path.GetFullPath(inputDirectory);
            checkpoint.OutputDirectory = Path.GetFullPath(outputDirectory);
            checkpoint.Entries ??= new List<BulkExtractBinsCheckpointEntry>();
        }

        var existingByPath = checkpoint.Entries
            .Where(item => !string.IsNullOrWhiteSpace(item.InputPath))
            .ToDictionary(item => Path.GetFullPath(item.InputPath), StringComparer.OrdinalIgnoreCase);

        var mergedEntries = new List<BulkExtractBinsCheckpointEntry>(discoveredFiles.Count);
        foreach (var discovered in discoveredFiles)
        {
            if (!existingByPath.TryGetValue(discovered.InputPath, out var existing))
            {
                mergedEntries.Add(CreatePendingEntry(discovered.RelativePath, discovered.InputPath));
                continue;
            }

            existing.InputPath = discovered.InputPath;
            existing.RelativePath = discovered.RelativePath;
            existing.OutputSubdirectory = BuildOutputSubdirectory(discovered.RelativePath);
            existing.LastUpdatedUtc = DateTimeOffset.UtcNow;

            if (!resume)
            {
                existing.Status = PendingStatus;
                existing.ErrorMessage = null;
            }
            else if (!IsKnownStatus(existing.Status))
            {
                existing.Status = PendingStatus;
            }

            mergedEntries.Add(existing);
        }

        checkpoint.Entries = mergedEntries;
        checkpoint.LastUpdatedUtc = DateTimeOffset.UtcNow;
        return checkpoint;
    }

    private static BulkExtractBinsCheckpointEntry CreatePendingEntry(string relativePath, string absolutePath)
    {
        return new BulkExtractBinsCheckpointEntry
        {
            InputPath = absolutePath,
            RelativePath = relativePath,
            OutputSubdirectory = BuildOutputSubdirectory(relativePath),
            Status = PendingStatus,
            ErrorMessage = null,
            LastUpdatedUtc = DateTimeOffset.UtcNow,
        };
    }

    private static string BuildOutputSubdirectory(string relativePath)
    {
        var sanitizedRelativePath = OutputPathService.SanitizeRelativePath(relativePath);
        var parentDirectory = Path.GetDirectoryName(sanitizedRelativePath);
        return string.IsNullOrWhiteSpace(parentDirectory)
            ? "extracted"
            : Path.Combine("extracted", parentDirectory);
    }

    private static bool IsKnownStatus(string? status)
    {
        return string.Equals(status, PendingStatus, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, RunningStatus, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, CompletedStatus, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, FailedStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteCheckpoint(string checkpointPath, BulkExtractBinsCheckpoint checkpoint)
    {
        checkpoint.LastUpdatedUtc = DateTimeOffset.UtcNow;
        var writer = new DeterministicJsonFileWriter();
        writer.Write(checkpointPath, checkpoint);
    }
}

public sealed record BulkExtractBinsExecutionResult(int ExitCode, string? ErrorMessage);

public sealed class BulkExtractBinsCheckpoint
{
    public List<BulkExtractBinsCheckpointEntry> Entries { get; set; } = new();

    public string InputDirectory { get; set; } = string.Empty;

    public DateTimeOffset LastUpdatedUtc { get; set; }

    public string OutputDirectory { get; set; } = string.Empty;
}

public sealed class BulkExtractBinsCheckpointEntry
{
    public string? ErrorMessage { get; set; }

    public string InputPath { get; set; } = string.Empty;

    public DateTimeOffset LastUpdatedUtc { get; set; }

    public string OutputSubdirectory { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string Status { get; set; } = "pending";
}

internal sealed record BulkExtractBinsDiscoveredFile(string InputPath, string RelativePath);
