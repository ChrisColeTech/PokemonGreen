using System.Collections.Concurrent;
using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Core.Loading;

namespace PokemonGreen.SwitchToolboxCli.Core.Extraction;

/// <summary>
/// Extracts nested archives using parallel processing and temp files.
/// Archives are spooled to temp files before processing to minimize memory usage.
/// </summary>
public sealed class StreamingNestedExtractionService
{
    private readonly FileLoader _loader;
    private readonly ArchiveExtractionService _extractionService;
    private readonly int _maxParallelism;

    public StreamingNestedExtractionService(
        FileLoader loader,
        ArchiveExtractionService extractionService,
        int maxParallelism = 4)
    {
        _loader = loader;
        _extractionService = extractionService;
        _maxParallelism = Math.Max(1, maxParallelism);
    }

    public StreamingExtractionResult ExtractAll(
        string inputPath,
        string outputDirectory,
        TextWriter? stdout = null,
        TextWriter? stderr = null)
    {
        stdout ??= TextWriter.Null;
        stderr ??= TextWriter.Null;

        var result = new StreamingExtractionResult();

        var loaded = _loader.Open(inputPath);
        if (loaded is null)
        {
            stderr.WriteLine($"No supported format detected: {inputPath}");
            return result;
        }

        if (loaded.Value.Value is not IArchiveFile rootArchive)
        {
            stderr.WriteLine($"Detected format {loaded.Value.Format.FormatName} is not an archive: {inputPath}");
            return result;
        }

        Directory.CreateDirectory(outputDirectory);
        var rootName = Path.GetFileName(inputPath);
        var rootOutputDir = Path.Combine(outputDirectory, SanitizeName(rootName));

        stdout.WriteLine($"Processing {rootName} ({loaded.Value.Format.FormatName})...");
        stdout.WriteLine($"Using {_maxParallelism} parallel workers with temp file spooling");

        // Create temp directory for spooling
        var tempDir = Path.Combine(Path.GetTempPath(), $"trpak_extract_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Phase 1: Spool all entries to temp files (sequential to avoid memory pressure)
            var entries = rootArchive.Files.ToList();
            var totalEntries = entries.Count;
            var spooledEntries = new List<SpooledEntry>();

            stdout.WriteLine($"Phase 1: Spooling {totalEntries} entries to temp files...");

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var tempPath = Path.Combine(tempDir, $"{i:00000}_{SanitizeName(entry.FileName)}");

                try
                {
                    using var entryStream = entry.OpenRead();
                    using var tempFile = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
                    entryStream.CopyTo(tempFile);

                    spooledEntries.Add(new SpooledEntry(i, entry.FileName, tempPath));
                }
                catch (Exception ex)
                {
                    stderr.WriteLine($"  Error spooling {entry.FileName}: {ex.Message}");
                    Interlocked.Increment(ref result._failedCount);
                }

                // Progress every 500 entries
                if ((i + 1) % 500 == 0)
                {
                    stdout.WriteLine($"  Spooled {i + 1}/{totalEntries}");
                }
            }

            stdout.WriteLine($"Phase 1 complete: {spooledEntries.Count} entries spooled");

            // Release root archive memory
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Phase 2: Process temp files in parallel
            stdout.WriteLine($"Phase 2: Extracting archives in parallel ({_maxParallelism} workers)...");

            var processedCount = 0;
            var lockObj = new object();

            Parallel.ForEach(
                spooledEntries,
                new ParallelOptions { MaxDegreeOfParallelism = _maxParallelism },
                spooled =>
                {
                    try
                    {
                        ProcessSpooledEntry(spooled, rootOutputDir, result, stderr);
                    }
                    catch (Exception ex)
                    {
                        lock (lockObj)
                        {
                            stderr.WriteLine($"  Error processing {spooled.OriginalName}: {ex.Message}");
                        }
                        Interlocked.Increment(ref result._failedCount);
                    }
                    finally
                    {
                        // Delete temp file immediately after processing
                        try { File.Delete(spooled.TempPath); } catch { }

                        var current = Interlocked.Increment(ref processedCount);
                        if (current % 200 == 0 || current == spooledEntries.Count)
                        {
                            lock (lockObj)
                            {
                                stdout.WriteLine($"  Processed {current}/{spooledEntries.Count} ({result.ProcessedArchivesCount} archives, {result.ExtractedFilesCount} files)");
                            }
                        }
                    }
                });

            stdout.WriteLine($"Phase 2 complete");
        }
        finally
        {
            // Cleanup temp directory
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch { }
        }

        return result;
    }

    private void ProcessSpooledEntry(
        SpooledEntry spooled,
        string outputDirectory,
        StreamingExtractionResult result,
        TextWriter stderr)
    {
        // Load from temp file (file-backed, not memory)
        var detected = _loader.Open(spooled.TempPath);

        if (detected is not null && detected.Value.Value is IArchiveFile nestedArchive)
        {
            // This is a nested archive - extract its contents
            var nestedOutputDir = Path.Combine(outputDirectory, SanitizeName(spooled.OriginalName));

            var extracted = _extractionService.ExtractToDirectory(nestedArchive, nestedOutputDir);
            Interlocked.Add(ref result._extractedFilesCount, extracted.Count);
            Interlocked.Increment(ref result._processedArchivesCount);
        }
        else
        {
            // Not an archive - copy to output
            var outputPath = Path.Combine(outputDirectory, SanitizeName(spooled.OriginalName));
            var outputDir = Path.GetDirectoryName(outputPath);

            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            File.Copy(spooled.TempPath, outputPath, overwrite: true);
            Interlocked.Increment(ref result._extractedFilesCount);
        }
    }

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "unnamed" : sanitized.Trim();
    }

    private sealed record SpooledEntry(int Index, string OriginalName, string TempPath);
}

public sealed class StreamingExtractionResult
{
    internal int _processedArchivesCount;
    internal int _extractedFilesCount;
    internal int _failedCount;

    public int ProcessedArchivesCount => _processedArchivesCount;
    public int ExtractedFilesCount => _extractedFilesCount;
    public int FailedCount => _failedCount;
}
