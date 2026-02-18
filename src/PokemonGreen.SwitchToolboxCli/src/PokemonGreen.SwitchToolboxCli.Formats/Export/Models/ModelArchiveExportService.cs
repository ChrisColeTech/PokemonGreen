using System.Text;
using System.Text.Json;
using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Core.Models;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Dae;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Obj;

namespace PokemonGreen.SwitchToolboxCli.Formats.Export.Models;

public sealed class ModelArchiveExportService
{
    private static readonly HashSet<string> SupportedOutputFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "obj",
        "dae",
    };

    private readonly ObjExporter _objExporter;
    private readonly DaeExporter _daeExporter;

    public ModelArchiveExportService()
        : this(new ObjExporter(), new DaeExporter())
    {
    }

    public ModelArchiveExportService(ObjExporter objExporter, DaeExporter daeExporter)
    {
        _objExporter = objExporter;
        _daeExporter = daeExporter;
    }

    public IReadOnlyList<ModelExportedFile> ExportModelLikeEntries(
        IReadOnlyList<ModelArchiveSource> archives,
        string outputDirectory,
        string requestedModelFormat)
    {
        ArgumentNullException.ThrowIfNull(archives);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedModelFormat);

        if (!SupportedOutputFormats.Contains(requestedModelFormat))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedModelFormat), requestedModelFormat, "Expected 'obj' or 'dae'.");
        }

        var outputRoot = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputRoot);

        var usedRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var exported = new List<ModelExportedFile>();

        foreach (var archiveSource in archives.OrderBy(item => item.ArchivePath, StringComparer.Ordinal))
        {
            var archiveDirectory = NormalizeRelativePath(archiveSource.ArchivePath, "archive");
            var sortedEntries = archiveSource.Archive.Files
                .Select((entry, index) => (Entry: entry, Index: index))
                .OrderBy(item => item.Entry.FileName, StringComparer.Ordinal)
                .ThenBy(item => item.Index)
                .ToList();

            foreach (var item in sortedEntries)
            {
                var sourceName = item.Entry.FileName;
                var payload = ReadAllBytes(item.Entry.OpenRead);
                var sourceExtension = Path.GetExtension(sourceName).ToLowerInvariant();

                if (sourceExtension is ".obj" or ".dae")
                {
                    var relativePath = BuildUniqueOutputPath(
                        archiveDirectory,
                        sourceName,
                        $"model_{item.Index:000}{sourceExtension}",
                        sourceExtension,
                        usedRelativePaths,
                        outputRoot);

                    var destinationPath = Path.GetFullPath(Path.Combine(outputRoot, relativePath));
                    File.WriteAllBytes(destinationPath, payload);

                    exported.Add(new ModelExportedFile(
                        archiveSource.ArchivePath.Replace('\\', '/'),
                        sourceName,
                        relativePath.Replace('\\', '/'),
                        sourceExtension.TrimStart('.'),
                        "pass-through"));

                    continue;
                }

                if (!TryParseJsonModel(payload, out var exportModel))
                {
                    continue;
                }

                var requestedExtension = "." + requestedModelFormat.ToLowerInvariant();
                var convertedRelativePath = BuildUniqueOutputPath(
                    archiveDirectory,
                    sourceName,
                    $"{exportModel.ModelName}_{item.Index:000}",
                    requestedExtension,
                    usedRelativePaths,
                    outputRoot,
                    forceExtension: true);

                var convertedDestinationPath = Path.GetFullPath(Path.Combine(outputRoot, convertedRelativePath));
                ExportModel(convertedDestinationPath, exportModel, requestedModelFormat);

                exported.Add(new ModelExportedFile(
                    archiveSource.ArchivePath.Replace('\\', '/'),
                    sourceName,
                    convertedRelativePath.Replace('\\', '/'),
                    requestedModelFormat.ToLowerInvariant(),
                    "converted"));
            }
        }

        return exported;
    }

    private void ExportModel(string destinationPath, ExportModel model, string requestedModelFormat)
    {
        if (requestedModelFormat.Equals("obj", StringComparison.OrdinalIgnoreCase))
        {
            _objExporter.Export(destinationPath, model);
            return;
        }

        _daeExporter.Export(destinationPath, model);
    }

    // Minimal deterministic JSON model schema accepted by this bridge pipeline:
    // {
    //   "modelName": "MyModel",
    //   "meshes": [
    //     {
    //       "meshName": "Mesh0",               // optional, defaults to mesh_###
    //       "vertices": [[x,y,z], ...],          // required
    //       "normals": [[x,y,z], ...],           // optional, count must match vertices when present
    //       "uvs": [[u,v], ...],                 // optional, count must match vertices when present
    //       "indices": [0,1,2, ...]              // required, triangle list
    //     }
    //   ]
    // }
    private static bool TryParseJsonModel(byte[] payload, out ExportModel model)
    {
        model = null!;
        if (payload.Length == 0 || !LooksLikeJson(payload))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!root.TryGetProperty("modelName", out var modelNameElement) ||
                modelNameElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var modelName = modelNameElement.GetString();
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return false;
            }

            if (!root.TryGetProperty("meshes", out var meshesElement) ||
                meshesElement.ValueKind != JsonValueKind.Array ||
                meshesElement.GetArrayLength() == 0)
            {
                return false;
            }

            var meshes = new List<ExportMesh>(meshesElement.GetArrayLength());
            var meshIndex = 0;
            foreach (var meshElement in meshesElement.EnumerateArray())
            {
                if (meshElement.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                if (!meshElement.TryGetProperty("vertices", out var verticesElement) ||
                    !TryParseVector3Array(verticesElement, out var vertices) ||
                    vertices.Count == 0)
                {
                    return false;
                }

                if (!meshElement.TryGetProperty("indices", out var indicesElement) ||
                    !TryParseIndices(indicesElement, vertices.Count, out var indices))
                {
                    return false;
                }

                IReadOnlyList<ExportVector3> normals = Array.Empty<ExportVector3>();
                if (meshElement.TryGetProperty("normals", out var normalsElement) &&
                    !TryParseOptionalVector3Array(normalsElement, vertices.Count, out normals))
                {
                    return false;
                }

                IReadOnlyList<ExportVector2> uvs = Array.Empty<ExportVector2>();
                if (meshElement.TryGetProperty("uvs", out var uvsElement) &&
                    !TryParseOptionalVector2Array(uvsElement, vertices.Count, out uvs))
                {
                    return false;
                }

                var meshName = meshElement.TryGetProperty("meshName", out var meshNameElement) &&
                               meshNameElement.ValueKind == JsonValueKind.String &&
                               !string.IsNullOrWhiteSpace(meshNameElement.GetString())
                    ? meshNameElement.GetString()!
                    : $"mesh_{meshIndex:000}";

                meshes.Add(new ExportMesh
                {
                    MeshName = meshName,
                    Vertices = vertices,
                    Normals = normals,
                    Uvs = uvs,
                    Indices = indices,
                });

                meshIndex++;
            }

            model = new ExportModel
            {
                ModelName = modelName,
                Meshes = meshes,
            };

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool LooksLikeJson(byte[] payload)
    {
        var text = Encoding.UTF8.GetString(payload.AsSpan(0, Math.Min(payload.Length, 128)));
        return text.TrimStart().StartsWith("{", StringComparison.Ordinal);
    }

    private static bool TryParseVector3Array(JsonElement element, out List<ExportVector3> values)
    {
        values = [];
        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (!TryParseVector3(item, out var vector))
            {
                return false;
            }

            values.Add(vector);
        }

        return true;
    }

    private static bool TryParseOptionalVector3Array(JsonElement element, int expectedCount, out IReadOnlyList<ExportVector3> values)
    {
        values = Array.Empty<ExportVector3>();
        if (!TryParseVector3Array(element, out var parsed))
        {
            return false;
        }

        if (parsed.Count != expectedCount)
        {
            return false;
        }

        values = parsed;
        return true;
    }

    private static bool TryParseOptionalVector2Array(JsonElement element, int expectedCount, out IReadOnlyList<ExportVector2> values)
    {
        values = Array.Empty<ExportVector2>();
        if (!TryParseVector2Array(element, out var parsed))
        {
            return false;
        }

        if (parsed.Count != expectedCount)
        {
            return false;
        }

        values = parsed;
        return true;
    }

    private static bool TryParseVector2Array(JsonElement element, out List<ExportVector2> values)
    {
        values = [];
        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (!TryParseVector2(item, out var vector))
            {
                return false;
            }

            values.Add(vector);
        }

        return true;
    }

    private static bool TryParseIndices(JsonElement element, int vertexCount, out IReadOnlyList<int> values)
    {
        values = Array.Empty<int>();
        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var indices = new List<int>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var index))
            {
                return false;
            }

            if (index < 0 || index >= vertexCount)
            {
                return false;
            }

            indices.Add(index);
        }

        if (indices.Count == 0 || (indices.Count % 3) != 0)
        {
            return false;
        }

        values = indices;
        return true;
    }

    private static bool TryParseVector3(JsonElement element, out ExportVector3 value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != 3)
        {
            return false;
        }

        var parts = element.EnumerateArray().ToArray();
        if (!TryReadFloat(parts[0], out var x) ||
            !TryReadFloat(parts[1], out var y) ||
            !TryReadFloat(parts[2], out var z))
        {
            return false;
        }

        value = new ExportVector3(x, y, z);
        return true;
    }

    private static bool TryParseVector2(JsonElement element, out ExportVector2 value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != 2)
        {
            return false;
        }

        var parts = element.EnumerateArray().ToArray();
        if (!TryReadFloat(parts[0], out var x) || !TryReadFloat(parts[1], out var y))
        {
            return false;
        }

        value = new ExportVector2(x, y);
        return true;
    }

    private static bool TryReadFloat(JsonElement element, out float value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Number && element.TryGetSingle(out value);
    }

    private static string BuildUniqueOutputPath(
        string archiveDirectory,
        string sourceName,
        string fallbackName,
        string extension,
        HashSet<string> usedRelativePaths,
        string outputRoot,
        bool forceExtension = false)
    {
        var baseName = string.IsNullOrWhiteSpace(sourceName)
            ? fallbackName
            : sourceName;

        var normalizedEntryPath = NormalizeRelativePath(baseName, fallbackName);
        if (forceExtension)
        {
            normalizedEntryPath = Path.ChangeExtension(normalizedEntryPath, extension);
        }
        else if (!Path.HasExtension(normalizedEntryPath))
        {
            normalizedEntryPath += extension;
        }

        var combinedRelativePath = Path.Combine(archiveDirectory, normalizedEntryPath);
        var uniqueRelativePath = EnsureUniqueRelativePath(combinedRelativePath, usedRelativePaths);
        var destinationPath = Path.GetFullPath(Path.Combine(outputRoot, uniqueRelativePath));
        if (!IsSubPath(destinationPath, outputRoot))
        {
            throw new InvalidOperationException($"Refused to write model outside output root: {sourceName}");
        }

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        return uniqueRelativePath;
    }

    private static byte[] ReadAllBytes(Func<Stream> openRead)
    {
        using var stream = openRead();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static string NormalizeRelativePath(string path, string fallbackName)
    {
        var segments = path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment != "." && segment != "..")
            .Select(SanitizeSegment)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        return segments.Count == 0
            ? fallbackName
            : Path.Combine(segments.ToArray());
    }

    private static string EnsureUniqueRelativePath(string candidate, HashSet<string> usedPaths)
    {
        if (usedPaths.Add(candidate))
        {
            return candidate;
        }

        var extension = Path.GetExtension(candidate);
        var stem = extension.Length == 0
            ? candidate
            : candidate[..^extension.Length];

        var suffix = 1;
        while (true)
        {
            var withSuffix = $"{stem}_{suffix:000}{extension}";
            if (usedPaths.Add(withSuffix))
            {
                return withSuffix;
            }

            suffix++;
        }
    }

    private static bool IsSubPath(string candidatePath, string rootPath)
    {
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;

        return candidatePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(candidatePath, rootPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars).Trim();
    }
}

public sealed record ModelArchiveSource(string ArchivePath, IArchiveFile Archive);

public sealed record ModelExportedFile(
    string ArchivePath,
    string EntryFileName,
    string OutputRelativePath,
    string OutputFormat,
    string ExportKind);
