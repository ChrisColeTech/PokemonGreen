using System.Text.Json;
using PokemonGreen.SwitchToolboxCli.App.Services;
using PokemonGreen.SwitchToolboxCli.Core.Extraction;
using PokemonGreen.SwitchToolboxCli.Core.Serialization;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Animations;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Models;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Textures;

namespace PokemonGreen.SwitchToolboxCli.App.Commands;

public static class BulkConvertCommand
{
    private static readonly HashSet<string> AllowedModelFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "obj",
        "dae",
    };

    private const string CompletedStatus = "completed";
    private const string FailedStatus = "failed";
    private const string PendingStatus = "pending";
    private const string RunningStatus = "running";
    private const string CheckpointFileName = "bulk-convert-checkpoint.json";

    public static int Run(
        DiscoveryService discovery,
        ArchiveExtractionService extractionService,
        ManifestFileWriter manifestWriter,
        ModelArchiveExportService modelExportService,
        TrinityModelAssemblyService trinityAssemblyService,
        TextureArchiveExportService textureExportService,
        AnimationClipArchiveExportService clipExportService,
        string inputDirectory,
        string outputDirectory,
        string modelFormat,
        int? limit,
        bool resume,
        TextWriter stdout,
        TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(extractionService);
        ArgumentNullException.ThrowIfNull(manifestWriter);
        ArgumentNullException.ThrowIfNull(modelExportService);
        ArgumentNullException.ThrowIfNull(trinityAssemblyService);
        ArgumentNullException.ThrowIfNull(textureExportService);
        ArgumentNullException.ThrowIfNull(clipExportService);

        return Run(
            inputDirectory,
            outputDirectory,
            modelFormat,
            limit,
            resume,
            (filePath, perFileOutputDirectory, format) =>
            {
                var convertErrors = new StringWriter();
                var exitCode = ConvertCommand.Run(
                    discovery,
                    extractionService,
                    manifestWriter,
                    modelExportService,
                    trinityAssemblyService,
                    textureExportService,
                    clipExportService,
                    filePath,
                    perFileOutputDirectory,
                    format,
                    extractArchives: false,
                    stdout: TextWriter.Null,
                    stderr: convertErrors);

                var errorMessage = exitCode == 0
                    ? null
                    : (string.IsNullOrWhiteSpace(convertErrors.ToString())
                        ? $"Convert command failed with exit code {exitCode}."
                        : convertErrors.ToString().Trim());

                return new BulkConvertExecutionResult(exitCode, errorMessage);
            },
            stdout,
            stderr);
    }

    public static int Run(
        string inputDirectory,
        string outputDirectory,
        string modelFormat,
        int? limit,
        bool resume,
        Func<string, string, string, BulkConvertExecutionResult> executeSingleConvert,
        TextWriter stdout,
        TextWriter stderr)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelFormat);
        ArgumentNullException.ThrowIfNull(executeSingleConvert);

        if (!AllowedModelFormats.Contains(modelFormat))
        {
            stderr.WriteLine($"Unsupported model format '{modelFormat}'. Allowed values: obj, dae");
            return 1;
        }

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
            .Select(path => new BulkConvertDiscoveredFile(
                Path.GetFullPath(path),
                Path.GetRelativePath(inputDirectory, path).Replace('\\', '/')))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToList();

        var checkpoint = LoadOrCreateCheckpoint(checkpointPath, inputDirectory, outputDirectory, modelFormat, discoveredFiles, resume, stderr);
        if (checkpoint is null)
        {
            return 1;
        }

        WriteCheckpoint(checkpointPath, checkpoint);

        var entriesByInputPath = checkpoint.Entries
            .ToDictionary(entry => entry.InputPath, StringComparer.OrdinalIgnoreCase);

        var skippedCompleted = 0;
        var pendingWork = new List<BulkConvertCheckpointEntry>();
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

            var result = executeSingleConvert(entry.InputPath, Path.Combine(outputDirectory, entry.OutputSubdirectory), modelFormat);
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
                    ? $"Convert command failed with exit code {result.ExitCode}."
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
            $"Bulk convert complete. Total: {finalEntries.Count}, completed: {completedCount}, failed: {failedCount}, pending: {pendingCount}, running: {runningCount}, skipped: {skippedCompleted}. Checkpoint: {Path.GetFullPath(checkpointPath)}");

        return failedCount > 0 ? 1 : 0;
    }

    private static BulkConvertCheckpoint? LoadOrCreateCheckpoint(
        string checkpointPath,
        string inputDirectory,
        string outputDirectory,
        string modelFormat,
        IReadOnlyList<BulkConvertDiscoveredFile> discoveredFiles,
        bool resume,
        TextWriter stderr)
    {
        var checkpoint = new BulkConvertCheckpoint
        {
            InputDirectory = Path.GetFullPath(inputDirectory),
            OutputDirectory = Path.GetFullPath(outputDirectory),
            ModelFormat = modelFormat.ToLowerInvariant(),
            LastUpdatedUtc = DateTimeOffset.UtcNow,
            Entries = new List<BulkConvertCheckpointEntry>(),
        };

        if (File.Exists(checkpointPath))
        {
            try
            {
                var content = File.ReadAllText(checkpointPath);
                checkpoint = JsonSerializer.Deserialize<BulkConvertCheckpoint>(content) ?? checkpoint;
            }
            catch (Exception ex)
            {
                stderr.WriteLine($"Failed to read checkpoint file '{checkpointPath}': {ex.Message}");
                return null;
            }

            checkpoint.InputDirectory = Path.GetFullPath(inputDirectory);
            checkpoint.OutputDirectory = Path.GetFullPath(outputDirectory);
            checkpoint.ModelFormat = modelFormat.ToLowerInvariant();
            checkpoint.Entries ??= new List<BulkConvertCheckpointEntry>();
        }

        var existingByPath = checkpoint.Entries
            .Where(item => !string.IsNullOrWhiteSpace(item.InputPath))
            .ToDictionary(item => Path.GetFullPath(item.InputPath), StringComparer.OrdinalIgnoreCase);

        var mergedEntries = new List<BulkConvertCheckpointEntry>(discoveredFiles.Count);
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

    private static BulkConvertCheckpointEntry CreatePendingEntry(string relativePath, string absolutePath)
    {
        return new BulkConvertCheckpointEntry
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
        return Path.Combine("converted", OutputPathService.SanitizeRelativePath(relativePath));
    }

    private static bool IsKnownStatus(string? status)
    {
        return string.Equals(status, PendingStatus, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, RunningStatus, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, CompletedStatus, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, FailedStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteCheckpoint(string checkpointPath, BulkConvertCheckpoint checkpoint)
    {
        checkpoint.LastUpdatedUtc = DateTimeOffset.UtcNow;
        var writer = new DeterministicJsonFileWriter();
        writer.Write(checkpointPath, checkpoint);
    }
}

public sealed record BulkConvertExecutionResult(int ExitCode, string? ErrorMessage);

public sealed class BulkConvertCheckpoint
{
    public List<BulkConvertCheckpointEntry> Entries { get; set; } = new();

    public string InputDirectory { get; set; } = string.Empty;

    public DateTimeOffset LastUpdatedUtc { get; set; }

    public string ModelFormat { get; set; } = string.Empty;

    public string OutputDirectory { get; set; } = string.Empty;
}

public sealed class BulkConvertCheckpointEntry
{
    public string? ErrorMessage { get; set; }

    public string InputPath { get; set; } = string.Empty;

    public DateTimeOffset LastUpdatedUtc { get; set; }

    public string OutputSubdirectory { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string Status { get; set; } = "pending";
}

internal sealed record BulkConvertDiscoveredFile(string InputPath, string RelativePath);
