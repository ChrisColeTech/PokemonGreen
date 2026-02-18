using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Core.Extraction;
using PokemonGreen.SwitchToolboxCli.Formats.Export.Models.Trinity;

namespace PokemonGreen.SwitchToolboxCli.Formats.Archives.TRPAK;

public sealed class TrpakFormat : IFileFormat
{
    public string FormatName => "TRPAK";
    public IReadOnlyList<string> Extensions => new[] { ".trpak" };

    public bool Identify(Stream stream, string? fileName)
    {
        if (!stream.CanSeek || stream.Length < 8)
        {
            return false;
        }

        // Verify with a lightweight FlatBuffer table probe.
        return TrpakArchive.TryReadFilesVector(stream, out _, out _);
    }

    public object Load(Stream stream, string? fileName)
    {
        return TrpakArchive.Parse(stream, fileName);
    }
}

public sealed class TrpakArchive : IArchiveFile
{
    private const byte CompressionOodle = 3;
    private const byte CompressionNone = 255;
    private const int MaxDependencyScanBytes = 4 * 1024 * 1024;
    private static readonly TrinityPayloadSniffer TrinityPayloadSniffer = new();
    private static readonly TrinityDependencyGraphBuilder DependencyGraphBuilder = new();
    private static readonly Regex ContainerReferenceRegex = new(
        @"(?i)([A-Za-z0-9_./\\-]+\.(?:trmdl|trmmt|trmdt|trmsh|trmbf|trmtr|trskl|tranm|traef|tracm|tracs|tracl|tracr|tracp|bntx))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly List<ArchiveEntry> _files;

    private TrpakArchive(List<ArchiveEntry> files)
    {
        _files = files;
    }

    public IEnumerable<ArchiveEntry> Files => _files;

    public static TrpakArchive Parse(Stream stream, string? sourcePath)
    {
        var parsedEntries = new List<ParsedTrpakEntry>();
        if (!TryGetRootTable(stream, out var rootTablePos) ||
            !TryGetVectorField(stream, rootTablePos, fieldIndex: 1, out var filesVectorPos, out var filesCount))
        {
            return new TrpakArchive(new List<ArchiveEntry>());
        }

        var rootHashes = ReadUlongVectorField(stream, rootTablePos, fieldIndex: 0);

        var lazyPath = !string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath)
            ? Path.GetFullPath(sourcePath)
            : null;

        bool streamIsFileBacked = lazyPath is not null;
        if (!streamIsFileBacked && stream.CanSeek)
        {
            stream.Position = 0;
        }

        for (var i = 0; i < filesCount; i++)
        {
            var slotPos = filesVectorPos + (i * 4);
            if (!TryReadInt32(stream, slotPos, out var tableRel))
            {
                continue;
            }

            var tablePos = slotPos + tableRel;
            if (!TryGetVectorField(stream, tablePos, fieldIndex: 4, out var dataVectorPos, out var dataLength))
            {
                continue;
            }

            var compression = ReadByteField(stream, tablePos, fieldIndex: 1, CompressionNone);
            var decompressedSize = ReadInt32Field(stream, tablePos, fieldIndex: 3, 0);
            var suffix = compression == CompressionOodle ? ".oodle" : string.Empty;
            var detailMessage = string.Empty;

            var payloadSample = ReadSlice(stream, dataVectorPos, Math.Min(0x110, dataLength));
            if (payloadSample.Length == 0)
            {
                continue;
            }

            bool isOodleCompressed = compression == CompressionOodle;
            int oodleDecompressedSize = 0;
            if (isOodleCompressed)
            {
                oodleDecompressedSize = decompressedSize;

                // Check if Oodle codec is available - sample decompression doesn't work
                // because Oodle needs complete compressed data, not partial samples
                var oodleStatus = TrpakOodleCodec.GetStatus();
                if (oodleStatus.IsAvailable)
                {
                    // Oodle is available - assume decompression will succeed at extraction time
                    // We can't sniff the file type without decompressing, so use .bin extension
                    suffix = string.Empty;
                    detailMessage = $"compression=oodle; lazy=true; oodle_available";
                }
                else
                {
                    // Oodle unavailable - files will remain compressed
                    detailMessage = $"compression=oodle; lazy=true; oodle_unavailable; reason={oodleStatus.Detail}";
                    // Keep .oodle suffix to indicate extraction will fail
                }
            }
 
            var extension = GuessExtension(payloadSample, null);
            var name = $"file_{parsedEntries.Count:000}{suffix}{extension}";
            var rootHash = i < rootHashes.Count ? rootHashes[i] : (ulong?)null;

            byte[]? dependencyScanPayload = null;
            if (IsContainerRootExtension(extension))
            {
                var candidatePayload = ReadSlice(stream, dataVectorPos, Math.Min(dataLength, MaxDependencyScanBytes));
                if (candidatePayload.Length > 0)
                {
                    if (isOodleCompressed && TrpakOodleCodec.TryDecompress(candidatePayload, Math.Min(oodleDecompressedSize, MaxDependencyScanBytes), out var decompressedScan, out _))
                    {
                        dependencyScanPayload = decompressedScan;
                    }
                    else
                    {
                        dependencyScanPayload = candidatePayload;
                    }
                }
            }

            Func<Stream> openRead;
            if (isOodleCompressed && lazyPath is not null)
            {
                var start = dataVectorPos;
                var length = dataLength;
                var decompSize = oodleDecompressedSize;
                openRead = () => OpenOodleFileSegment(lazyPath, start, length, decompSize);
            }
            else if (isOodleCompressed)
            {
                var compressedPayload = ReadSlice(stream, dataVectorPos, dataLength);
                var decompSize = oodleDecompressedSize;
                openRead = () =>
                {
                    if (!TrpakOodleCodec.TryDecompress(compressedPayload, decompSize, out var decompressed, out var decompDetail))
                    {
                        Console.Error.WriteLine($"[OODLE] Full decompression failed: compLen={compressedPayload.Length}, decompSize={decompSize}, reason={decompDetail}");
                        return new MemoryStream(compressedPayload, writable: false);
                    }
                    return new MemoryStream(decompressed, writable: false);
                };
            }
            else if (lazyPath is not null)
            {
                var start = dataVectorPos;
                var length = dataLength;
                openRead = () => OpenFileSegment(lazyPath, start, length);
            }
            else
            {
                var payload = ReadSlice(stream, dataVectorPos, dataLength);
                openRead = () => new MemoryStream(payload, writable: false);
            }

            parsedEntries.Add(new ParsedTrpakEntry
            {
                Index = i,
                FileName = name,
                OpenRead = openRead,
                DetailMessage = string.IsNullOrWhiteSpace(detailMessage) ? null : detailMessage,
                RootHash = rootHash,
                PayloadForDependencyScan = dependencyScanPayload,
                EffectiveExtension = extension,
            });
        }

        ApplyHashGuidedDependencyHints(parsedEntries);

        var output = parsedEntries
            .OrderBy(entry => entry.Index)
            .Select(entry => new ArchiveEntry
            {
                FileName = entry.FileName,
                OpenRead = entry.OpenRead,
                DetailMessage = entry.DetailMessage,
                RootHash = entry.RootHash,
                ExtensionHint = entry.EffectiveExtension,
            })
            .ToList();

        return new TrpakArchive(output);
    }

    internal static bool TryReadFilesVector(Stream stream, out int filesVectorPos, out int filesCount)
    {
        filesVectorPos = 0;
        filesCount = 0;

        if (!TryGetRootTable(stream, out var rootTablePos))
        {
            return false;
        }

        if (!TryGetVectorField(stream, rootTablePos, fieldIndex: 1, out filesVectorPos, out filesCount))
        {
            return false;
        }

        return filesCount > 0;
    }

    private static bool TryGetRootTable(Stream stream, out int rootTablePos)
    {
        rootTablePos = 0;
        if (!TryReadInt32(stream, 0, out var rootOffset))
        {
            return false;
        }

        rootTablePos = rootOffset;
        return rootTablePos >= 4 && rootTablePos < stream.Length;
    }

    private static bool TryGetVectorField(Stream stream, int tablePos, int fieldIndex, out int vectorDataPos, out int vectorLength)
    {
        vectorDataPos = 0;
        vectorLength = 0;

        if (!TryGetFieldAddress(stream, tablePos, fieldIndex, out var fieldAddress) || !TryReadInt32(stream, fieldAddress, out var vectorRel))
        {
            return false;
        }

        var vectorPos = fieldAddress + vectorRel;
        if (!TryReadInt32(stream, vectorPos, out vectorLength) || vectorLength < 0)
        {
            return false;
        }

        vectorDataPos = vectorPos + 4;
        return vectorDataPos >= 0 && vectorDataPos <= stream.Length;
    }

    private static byte ReadByteField(Stream stream, int tablePos, int fieldIndex, byte defaultValue)
    {
        if (!TryGetFieldAddress(stream, tablePos, fieldIndex, out var fieldAddress) || fieldAddress < 0 || fieldAddress >= stream.Length)
        {
            return defaultValue;
        }

        var value = ReadSlice(stream, fieldAddress, 1);
        return value.Length == 1 ? value[0] : defaultValue;
    }

    private static int ReadInt32Field(Stream stream, int tablePos, int fieldIndex, int defaultValue)
    {
        if (!TryGetFieldAddress(stream, tablePos, fieldIndex, out var fieldAddress) || fieldAddress < 0 || fieldAddress > stream.Length - 4)
        {
            return defaultValue;
        }

        return TryReadInt32(stream, fieldAddress, out var value)
            ? value
            : defaultValue;
    }

    private static bool TryGetFieldAddress(Stream stream, int tablePos, int fieldIndex, out int fieldAddress)
    {
        fieldAddress = 0;
        if (!TryReadInt32(stream, tablePos, out var vtableOffset))
        {
            return false;
        }

        var vtablePos = tablePos - vtableOffset;
        if (!TryReadUInt16(stream, vtablePos, out var vtableSize))
        {
            return false;
        }

        var entryPos = vtablePos + 4 + (fieldIndex * 2);
        if (entryPos + 2 > vtablePos + vtableSize || !TryReadUInt16(stream, entryPos, out var fieldOffset) || fieldOffset == 0)
        {
            return false;
        }

        fieldAddress = tablePos + fieldOffset;
        return fieldAddress >= 0 && fieldAddress < stream.Length;
    }

    private static byte[] ReadSlice(Stream stream, int offset, int length)
    {
        if (offset < 0 || length <= 0 || offset > stream.Length - length)
        {
            return Array.Empty<byte>();
        }

        var result = new byte[length];
        var start = stream.Position;
        try
        {
            stream.Position = offset;
            var read = stream.Read(result, 0, length);
            if (read != length)
            {
                return Array.Empty<byte>();
            }
        }
        finally
        {
            stream.Position = start;
        }

        return result;
    }

    private static byte[] ReadSlice(byte[] data, int offset, int length)
    {
        if (offset < 0 || length <= 0 || offset > data.Length - length)
        {
            return Array.Empty<byte>();
        }

        var result = new byte[length];
        Buffer.BlockCopy(data, offset, result, 0, length);
        return result;
    }

    private static bool TryReadInt32(Stream stream, int offset, out int value)
    {
        value = 0;
        if (offset < 0 || offset > stream.Length - 4)
        {
            return false;
        }

        var data = ReadSlice(stream, offset, 4);
        if (data.Length != 4)
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(data);
        return true;
    }

    private static bool TryReadUInt16(Stream stream, int offset, out ushort value)
    {
        value = 0;
        if (offset < 0 || offset > stream.Length - 2)
        {
            return false;
        }

        var data = ReadSlice(stream, offset, 2);
        if (data.Length != 2)
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt16LittleEndian(data);
        return true;
    }

    private static Stream OpenFileSegment(string filePath, int offset, int length)
    {
        var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536, FileOptions.SequentialScan);
        if (offset < 0 || length < 0 || offset > file.Length - length)
        {
            file.Dispose();
            return new MemoryStream(Array.Empty<byte>(), writable: false);
        }

        return new FileSegmentStream(file, offset, length);
    }

    private static Stream OpenOodleFileSegment(string filePath, int offset, int compressedLength, int decompressedSize)
    {
        using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536, FileOptions.SequentialScan);
        if (offset < 0 || compressedLength < 0 || offset > file.Length - compressedLength)
        {
            return new MemoryStream(Array.Empty<byte>(), writable: false);
        }

        var compressed = new byte[compressedLength];
        file.Position = offset;
        var read = file.Read(compressed, 0, compressedLength);
        if (read != compressedLength)
        {
            return new MemoryStream(Array.Empty<byte>(), writable: false);
        }

        if (!TrpakOodleCodec.TryDecompress(compressed, decompressedSize, out var decompressed, out var decompDetail))
        {
            Console.Error.WriteLine($"[OODLE] File segment decompression failed: compLen={compressed.Length}, decompSize={decompressedSize}, reason={decompDetail}");
            return new MemoryStream(compressed, writable: false);
        }

        return new MemoryStream(decompressed, writable: false);
    }

    private sealed class FileSegmentStream : Stream
    {
        private readonly FileStream _baseStream;
        private readonly long _start;
        private readonly long _length;
        private long _position;
        private bool _disposed;

        public FileSegmentStream(FileStream baseStream, long start, long length)
        {
            _baseStream = baseStream;
            _start = start;
            _length = length;
            _position = 0;
            _baseStream.Position = start;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _position = value;
                _baseStream.Position = _start + value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileSegmentStream));

            var remaining = _length - _position;
            if (remaining <= 0)
                return 0;

            var toRead = (int)Math.Min(count, remaining);
            var read = _baseStream.Read(buffer, offset, toRead);
            _position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentException("Invalid seek origin", nameof(origin))
            };

            Position = newPosition;
            return newPosition;
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _baseStream?.Dispose();
                }
                _disposed = true;
            }
            base.Dispose(disposing);
        }
    }

    private static List<ulong> ReadUlongVectorField(Stream stream, int tablePos, int fieldIndex)
    {
        var values = new List<ulong>();
        if (!TryGetVectorField(stream, tablePos, fieldIndex, out var vectorDataPos, out var vectorLength) || vectorLength <= 0)
        {
            return values;
        }

        for (var i = 0; i < vectorLength; i++)
        {
            var valuePos = vectorDataPos + (i * 8);
            if (valuePos < 0 || valuePos > stream.Length - 8)
            {
                continue;
            }

            var bytes = ReadSlice(stream, valuePos, 8);
            if (bytes.Length != 8)
            {
                continue;
            }

            values.Add(BinaryPrimitives.ReadUInt64LittleEndian(bytes));
        }

        return values;
    }

    private static void ApplyHashGuidedDependencyHints(List<ParsedTrpakEntry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        var hashLookup = new Dictionary<ulong, List<ParsedTrpakEntry>>();
        foreach (var entry in entries)
        {
            if (!entry.RootHash.HasValue)
            {
                continue;
            }

            var hash = entry.RootHash.Value;
            if (!hashLookup.TryGetValue(hash, out var grouped))
            {
                grouped = new List<ParsedTrpakEntry>();
                hashLookup.Add(hash, grouped);
            }

            grouped.Add(entry);
        }

        foreach (var modelEntry in entries.OrderBy(item => item.Index))
        {
            if (!IsContainerRootExtension(modelEntry.EffectiveExtension) ||
                modelEntry.PayloadForDependencyScan is null ||
                modelEntry.PayloadForDependencyScan.Length == 0 ||
                !TryReadContainerDependencies(modelEntry, out var dependencies))
            {
                continue;
            }

            var links = DependencyGraphBuilder.ResolveArchiveEntryLinks(
                dependencies
                    .Select(item => new TrinityDependencyReference(item.Path, item.Extension))
                    .ToList(),
                entries
                    .Select((entry, index) => new TrinityGraphArchiveEntry(entry.FileName, entry.RootHash, index))
                    .ToList(),
                consumeMatches: true,
                allowGenericBinHashMatches: true);

            foreach (var link in links)
            {
                if (!link.ResolvedEntryHash.HasValue || !hashLookup.TryGetValue(link.ResolvedEntryHash.Value, out var candidates))
                {
                    continue;
                }

                var target = candidates
                    .OrderBy(item => item.Index)
                    .FirstOrDefault(item =>
                        item.Index != modelEntry.Index &&
                        ShouldApplyDependencyHint(item, link.Extension));
                if (target is null)
                {
                    continue;
                }

                target.EffectiveExtension = link.Extension;
                target.FileName = ReplaceLastExtension(target.FileName, link.Extension);
                target.DetailMessage = AppendDetail(target.DetailMessage, $"hash_linked={link.Extension}:{link.DependencyPath}");
            }
        }
    }

    private static bool ShouldApplyDependencyHint(ParsedTrpakEntry entry, string extension)
    {
        var currentExtension = Path.GetExtension(entry.FileName);
        if (!currentExtension.Equals(".bin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return extension.Equals(".trmsh", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".trmbf", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".trskl", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".trmtr", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tranm", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".traef", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tracm", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tracs", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tracl", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tracr", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tracp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bntx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsContainerRootExtension(string extension)
    {
        return extension.Equals(".trmdl", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".trmmt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".trmdt", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReplaceLastExtension(string fileName, string extension)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(stem))
        {
            return $"unnamed{extension}";
        }

        return stem + extension;
    }

    private static string? AppendDetail(string? detail, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return detail;
        }

        if (string.IsNullOrWhiteSpace(detail))
        {
            return value;
        }

        return detail + "; " + value;
    }

    private static bool TryReadTrmdlDependencies(byte[] payload, out List<(string Path, string Extension)> dependencies)
    {
        dependencies = new List<(string Path, string Extension)>();
        try
        {
            if (!TryGetRootTable(payload, out var rootTablePos))
            {
                return false;
            }

            var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (TryGetVectorField(payload, rootTablePos, fieldIndex: 1, out var meshRefVectorPos, out var meshRefCount))
            {
                var count = Math.Min(meshRefCount, 8192);
                for (var i = 0; i < count; i++)
                {
                    if (!TryGetTableFromVector(payload, meshRefVectorPos, i, out var meshRefTablePos))
                    {
                        continue;
                    }

                    if (TryReadStringField(payload, meshRefTablePos, fieldIndex: 0, out var meshPath))
                    {
                        AddDependency(dependencies, dedupe, meshPath);
                    }

                    if (TryReadStringField(payload, meshRefTablePos, fieldIndex: 1, out var bufferPath))
                    {
                        AddDependency(dependencies, dedupe, bufferPath);
                    }
                }
            }

            ReadDependencyVector(payload, rootTablePos, fieldIndex: 2, dependencies, dedupe);
            ReadDependencyVector(payload, rootTablePos, fieldIndex: 3, dependencies, dedupe);

            return dependencies.Count > 0;
        }
        catch
        {
            dependencies = new List<(string Path, string Extension)>();
            return false;
        }
    }

    private static bool TryReadContainerDependencies(ParsedTrpakEntry entry, out List<(string Path, string Extension)> dependencies)
    {
        dependencies = new List<(string Path, string Extension)>();
        if (entry.PayloadForDependencyScan is null || entry.PayloadForDependencyScan.Length == 0)
        {
            return false;
        }

        var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (entry.EffectiveExtension.Equals(".trmdl", StringComparison.OrdinalIgnoreCase) &&
            TryReadTrmdlDependencies(entry.PayloadForDependencyScan, out var trmdlDependencies))
        {
            foreach (var dependency in trmdlDependencies)
            {
                AddDependency(dependencies, dedupe, dependency.Path);
            }
        }

        foreach (var reference in ScanPayloadReferences(entry.PayloadForDependencyScan))
        {
            AddDependency(dependencies, dedupe, reference);
        }

        return dependencies.Count > 0;
    }

    private static IReadOnlyList<string> ScanPayloadReferences(byte[] payload)
    {
        if (payload.Length == 0)
        {
            return Array.Empty<string>();
        }

        try
        {
            var scanLength = Math.Min(payload.Length, MaxDependencyScanBytes);
            var text = Encoding.UTF8.GetString(payload, 0, scanLength);
            var references = new List<string>();
            var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in ContainerReferenceRegex.Matches(text))
            {
                if (!match.Success)
                {
                    continue;
                }

                var value = NormalizeDependencyPath(match.Groups[1].Value);
                if (string.IsNullOrWhiteSpace(value) || !dedupe.Add(value))
                {
                    continue;
                }

                references.Add(value);
            }

            return references;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static void ReadDependencyVector(
        byte[] payload,
        int tablePos,
        int fieldIndex,
        List<(string Path, string Extension)> dependencies,
        HashSet<string> dedupe)
    {
        if (!TryGetVectorField(payload, tablePos, fieldIndex, out var vectorPos, out var count) || count <= 0)
        {
            return;
        }

        var maxCount = Math.Min(count, 8192);
        for (var i = 0; i < maxCount; i++)
        {
            if (TryReadStringFromVector(payload, vectorPos, i, out var stringValue))
            {
                AddDependency(dependencies, dedupe, stringValue);
                continue;
            }

            if (!TryGetTableFromVector(payload, vectorPos, i, out var tableValue))
            {
                continue;
            }

            for (var field = 0; field < 4; field++)
            {
                if (TryReadStringField(payload, tableValue, field, out var path))
                {
                    AddDependency(dependencies, dedupe, path);
                }
            }
        }
    }

    private static void AddDependency(
        List<(string Path, string Extension)> dependencies,
        HashSet<string> dedupe,
        string candidatePath)
    {
        if (!TryGetSupportedDependencyExtension(candidatePath, out var extension))
        {
            return;
        }

        var normalized = NormalizeDependencyPath(candidatePath);
        if (dedupe.Add(normalized))
        {
            dependencies.Add((normalized, extension));
        }
    }

    private static string NormalizeDependencyPath(string path)
    {
        return path.Replace('\\', '/').Trim().Trim('\0');
    }

    private static bool TryGetSupportedDependencyExtension(string path, out string extension)
    {
        extension = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var lower = path.Replace('\\', '/').ToLowerInvariant();
        if (lower.EndsWith(".trmsh", StringComparison.Ordinal))
        {
            extension = ".trmsh";
            return true;
        }

        if (lower.EndsWith(".trmbf", StringComparison.Ordinal))
        {
            extension = ".trmbf";
            return true;
        }

        if (lower.EndsWith(".trskl", StringComparison.Ordinal))
        {
            extension = ".trskl";
            return true;
        }

        if (lower.EndsWith(".trmtr", StringComparison.Ordinal))
        {
            extension = ".trmtr";
            return true;
        }

        if (lower.EndsWith(".tranm", StringComparison.Ordinal))
        {
            extension = ".tranm";
            return true;
        }

        if (lower.EndsWith(".traef", StringComparison.Ordinal))
        {
            extension = ".traef";
            return true;
        }

        if (lower.EndsWith(".tracm", StringComparison.Ordinal))
        {
            extension = ".tracm";
            return true;
        }

        if (lower.EndsWith(".tracs", StringComparison.Ordinal))
        {
            extension = ".tracs";
            return true;
        }

        if (lower.EndsWith(".tracl", StringComparison.Ordinal))
        {
            extension = ".tracl";
            return true;
        }

        if (lower.EndsWith(".tracr", StringComparison.Ordinal))
        {
            extension = ".tracr";
            return true;
        }

        if (lower.EndsWith(".tracp", StringComparison.Ordinal))
        {
            extension = ".tracp";
            return true;
        }

        if (lower.EndsWith(".bntx", StringComparison.Ordinal))
        {
            extension = ".bntx";
            return true;
        }

        return false;
    }

    private static bool TryGetRootTable(byte[] payload, out int rootTablePos)
    {
        rootTablePos = 0;
        if (payload.Length < 4)
        {
            return false;
        }

        var rootOffset = BinaryPrimitives.ReadInt32LittleEndian(payload);
        if (rootOffset < 4 || rootOffset > payload.Length - 4)
        {
            return false;
        }

        rootTablePos = rootOffset;
        return true;
    }

    private static bool TryGetVectorField(byte[] payload, int tablePos, int fieldIndex, out int vectorDataPos, out int vectorLength)
    {
        vectorDataPos = 0;
        vectorLength = 0;
        if (!TryGetFieldAddress(payload, tablePos, fieldIndex, out var fieldAddress) ||
            !TryReadInt32(payload, fieldAddress, out var vectorRel))
        {
            return false;
        }

        var vectorPos = fieldAddress + vectorRel;
        if (!TryReadInt32(payload, vectorPos, out vectorLength) || vectorLength < 0)
        {
            return false;
        }

        vectorDataPos = vectorPos + 4;
        return vectorDataPos >= 0 && vectorDataPos <= payload.Length;
    }

    private static bool TryGetFieldAddress(byte[] payload, int tablePos, int fieldIndex, out int fieldAddress)
    {
        fieldAddress = 0;
        if (!TryReadInt32(payload, tablePos, out var vtableOffset))
        {
            return false;
        }

        var vtablePos = tablePos - vtableOffset;
        if (!TryReadUInt16(payload, vtablePos, out var vtableSize))
        {
            return false;
        }

        var entryPos = vtablePos + 4 + (fieldIndex * 2);
        if (entryPos + 2 > vtablePos + vtableSize ||
            !TryReadUInt16(payload, entryPos, out var fieldOffset) ||
            fieldOffset == 0)
        {
            return false;
        }

        fieldAddress = tablePos + fieldOffset;
        return fieldAddress >= 0 && fieldAddress < payload.Length;
    }

    private static bool TryGetTableFromVector(byte[] payload, int vectorDataPos, int index, out int tablePos)
    {
        tablePos = 0;
        var slotPos = vectorDataPos + (index * 4);
        if (!TryReadInt32(payload, slotPos, out var tableRel))
        {
            return false;
        }

        tablePos = slotPos + tableRel;
        return tablePos >= 4 && tablePos < payload.Length;
    }

    private static bool TryReadStringFromVector(byte[] payload, int vectorDataPos, int index, out string value)
    {
        value = string.Empty;
        var slotPos = vectorDataPos + (index * 4);
        if (!TryReadInt32(payload, slotPos, out var stringRel))
        {
            return false;
        }

        return TryReadStringAt(payload, slotPos + stringRel, out value);
    }

    private static bool TryReadStringField(byte[] payload, int tablePos, int fieldIndex, out string value)
    {
        value = string.Empty;
        if (!TryGetFieldAddress(payload, tablePos, fieldIndex, out var fieldAddress) ||
            !TryReadInt32(payload, fieldAddress, out var rel))
        {
            return false;
        }

        return TryReadStringAt(payload, fieldAddress + rel, out value);
    }

    private static bool TryReadStringAt(byte[] payload, int stringPos, out string value)
    {
        value = string.Empty;
        if (!TryReadInt32(payload, stringPos, out var length) ||
            length <= 0 ||
            length > 2048 ||
            stringPos + 4 + length > payload.Length)
        {
            return false;
        }

        try
        {
            value = System.Text.Encoding.UTF8.GetString(payload, stringPos + 4, length);
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadInt32(byte[] payload, int offset, out int value)
    {
        value = 0;
        if (offset < 0 || offset > payload.Length - 4)
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, 4));
        return true;
    }

    private static bool TryReadUInt16(byte[] payload, int offset, out ushort value)
    {
        value = 0;
        if (offset < 0 || offset > payload.Length - 2)
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, 2));
        return true;
    }

    private static string GuessExtension(byte[] payloadSample, byte[]? fullPayload)
    {
        if (fullPayload is not null &&
            TrinityPayloadSniffer.TryClassify(fullPayload, out var classifiedPayload) &&
            ShouldUseSniffedExtension(classifiedPayload))
        {
            return classifiedPayload.Extension;
        }

        if (TrinityPayloadSniffer.TryClassify(payloadSample, out var sampleClassification) &&
            ShouldUseSniffedExtension(sampleClassification))
        {
            return sampleClassification.Extension;
        }

        if (StartsWith(payloadSample, "CHR0") || StartsWith(payloadSample, "TRANM"))
        {
            return ".tranm";
        }

        if (StartsWith(payloadSample, "TRAEF"))
        {
            return ".traef";
        }

        if (StartsWith(payloadSample, "CRAG") || StartsWith(payloadSample, "GARC"))
        {
            return ".garc";
        }

        if (StartsWith(payloadSample, "BNTX"))
        {
            return ".bntx";
        }

        if (payloadSample.Length >= 0x104 &&
            payloadSample[0x100] == (byte)'N' && payloadSample[0x101] == (byte)'C' && payloadSample[0x102] == (byte)'C' && payloadSample[0x103] == (byte)'H')
        {
            return ".cxi";
        }

        return ".bin";
    }

    private static bool StartsWith(byte[] data, string text)
    {
        if (data.Length < text.Length)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (data[i] != text[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldUseSniffedExtension(TrinityPayloadClassification classification)
    {
        return classification.Confidence == TrinitySniffConfidence.High ||
               (classification.Confidence == TrinitySniffConfidence.Medium &&
                !classification.Extension.Equals(".trmdl", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ParsedTrpakEntry
    {
        public required int Index { get; init; }
        public required string FileName { get; set; }
        public required Func<Stream> OpenRead { get; init; }
        public string? DetailMessage { get; set; }
        public ulong? RootHash { get; init; }
        public byte[]? PayloadForDependencyScan { get; init; }
        public required string EffectiveExtension { get; set; }
    }
}
