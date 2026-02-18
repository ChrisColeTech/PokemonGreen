namespace PokemonGreen.SwitchToolboxCli.Core.Models;

public sealed record ManifestDetectedItem(string Path, string FormatName);

public sealed record ArchiveExtractionReport(
    string Path,
    string FormatName,
    string OutputDirectory,
    int ExtractedFileCount);

public sealed record ModelManifestItem(
    string ArchivePath,
    string EntryFileName,
    string OutputRelativePath,
    string OutputFormat,
    string ExportKind,
    string? BundleKey = null,
    string BundleStatus = "completed",
    string? DetailMessage = null);

public sealed record TextureManifestItem(
    string ArchivePath,
    string EntryFileName,
    string OutputRelativePath,
    string DetectedExtension);

public sealed record ClipManifestItem(
    string ArchivePath,
    string EntryFileName,
    string OutputRelativePath,
    string DetectedExtension);

public sealed record ConvertManifest(
    string InputPath,
    string OutputPath,
    string ModelFormat,
    string ModelConversionStatus,
    IReadOnlyList<ManifestDetectedItem> DetectedItems,
    IReadOnlyList<ArchiveExtractionReport> Archives,
    IReadOnlyList<ModelManifestItem> Models,
    IReadOnlyList<TextureManifestItem> Textures,
    IReadOnlyList<ClipManifestItem> Clips);

public sealed record BatchManifest(
    string InputPath,
    string OutputPath,
    string ModelFormat,
    string ModelConversionStatus,
    IReadOnlyList<ManifestDetectedItem> DetectedItems,
    IReadOnlyList<ArchiveExtractionReport> Archives,
    IReadOnlyList<ModelManifestItem> Models,
    IReadOnlyList<TextureManifestItem> Textures,
    IReadOnlyList<ClipManifestItem> Clips);
