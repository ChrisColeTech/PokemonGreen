using System.Buffers.Binary;
using System.Text;
using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Formats.Common;

namespace PokemonGreen.SwitchToolboxCli.Formats.Archives.TRPFS;

public sealed class TrpfsFormat : IFileFormat
{
    private static readonly byte[] OnePackMagic = Encoding.ASCII.GetBytes("ONEPACK\0");

    public string FormatName => "TRPFS";
    public IReadOnlyList<string> Extensions => new[] { ".trpfs", ".trpfd" };

    public bool Identify(Stream stream, string? fileName)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty);
        if (ext.Equals(".trpfs", StringComparison.OrdinalIgnoreCase))
        {
            return LooksLikeTrpfs(stream);
        }

        if (ext.Equals(".trpfd", StringComparison.OrdinalIgnoreCase))
        {
            return LooksLikeTrpfd(stream);
        }

        return LooksLikeTrpfs(stream);
    }

    public object Load(Stream stream, string? fileName)
    {
        var sourcePath = !string.IsNullOrWhiteSpace(fileName) && File.Exists(fileName)
            ? Path.GetFullPath(fileName)
            : null;

        string? trpfsPath = null;
        string? trpfdPath = null;

        var ext = Path.GetExtension(fileName ?? string.Empty);
        if (ext.Equals(".trpfs", StringComparison.OrdinalIgnoreCase))
        {
            trpfsPath = sourcePath;
            trpfdPath = sourcePath is null ? null : Path.ChangeExtension(sourcePath, ".trpfd");
        }
        else if (ext.Equals(".trpfd", StringComparison.OrdinalIgnoreCase))
        {
            trpfdPath = sourcePath;
            trpfsPath = sourcePath is null ? null : Path.ChangeExtension(sourcePath, ".trpfs");
        }

        if (!string.IsNullOrWhiteSpace(trpfdPath) && !File.Exists(trpfdPath))
        {
            trpfdPath = null;
        }

        if (!string.IsNullOrWhiteSpace(trpfsPath) && !File.Exists(trpfsPath))
        {
            trpfsPath = null;
        }

        if (trpfsPath is null)
        {
            return new TrpfsArchive(new List<ArchiveEntry>());
        }

        return TrpfsArchive.Parse(trpfsPath, trpfdPath);
    }

    private static bool LooksLikeTrpfs(Stream stream)
    {
        if (!stream.CanSeek || stream.Length < 16)
        {
            return false;
        }

        var header = ReadSlice(stream, 0, 16);
        if (header.Length != 16)
        {
            return false;
        }

        if (!header.AsSpan(0, 8).SequenceEqual(OnePackMagic))
        {
            return false;
        }

        var rootOffset = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(8, 8));
        return rootOffset >= 0 && rootOffset < stream.Length;
    }

    private static bool LooksLikeTrpfd(Stream stream)
    {
        if (!stream.CanSeek || stream.Length < 32)
        {
            return false;
        }

        var data = ReadAll(stream);
        return TryReadStringVector(data, rootFieldIndex: 1, out _);
    }

    private static byte[] ReadSlice(Stream stream, long offset, int length)
    {
        if (offset < 0 || length <= 0 || offset > stream.Length - length)
        {
            return Array.Empty<byte>();
        }

        var start = stream.Position;
        try
        {
            stream.Position = offset;
            var output = new byte[length];
            var read = stream.Read(output, 0, length);
            return read == length ? output : Array.Empty<byte>();
        }
        finally
        {
            stream.Position = start;
        }
    }

    private static byte[] ReadAll(Stream stream)
    {
        var start = stream.CanSeek ? stream.Position : 0;
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        try
        {
            using var mem = new MemoryStream();
            stream.CopyTo(mem);
            return mem.ToArray();
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = start;
            }
        }
    }

    private static bool TryReadStringVector(byte[] data, int rootFieldIndex, out List<string> values)
    {
        values = new List<string>();
        if (!TryGetRootTable(data, out var rootTable) || !TryGetVector(data, rootTable, rootFieldIndex, out var vectorPos, out var vectorLength))
        {
            return false;
        }

        for (var i = 0; i < vectorLength; i++)
        {
            var elementPos = vectorPos + (i * 4);
            if (!TryReadInt32(data, elementPos, out var rel))
            {
                continue;
            }

            var stringPos = elementPos + rel;
            if (!TryReadInt32(data, stringPos, out var stringLength) || stringLength < 0 || stringPos + 4 > data.Length - stringLength)
            {
                continue;
            }

            values.Add(Encoding.UTF8.GetString(data, stringPos + 4, stringLength));
        }

        return values.Count > 0;
    }

    private static bool TryGetRootTable(byte[] data, out int rootTable)
    {
        rootTable = 0;
        return TryReadInt32(data, 0, out rootTable) && rootTable >= 4 && rootTable < data.Length;
    }

    private static bool TryGetVector(byte[] data, int tablePos, int fieldIndex, out int vectorPos, out int vectorLength)
    {
        vectorPos = 0;
        vectorLength = 0;
        if (!TryGetFieldAddress(data, tablePos, fieldIndex, out var fieldAddress) || !TryReadInt32(data, fieldAddress, out var vectorRel))
        {
            return false;
        }

        var pos = fieldAddress + vectorRel;
        if (!TryReadInt32(data, pos, out vectorLength) || vectorLength < 0)
        {
            return false;
        }

        vectorPos = pos + 4;
        return vectorPos >= 0 && vectorPos <= data.Length;
    }

    private static bool TryGetFieldAddress(byte[] data, int tablePos, int fieldIndex, out int fieldAddress)
    {
        fieldAddress = 0;
        if (!TryReadInt32(data, tablePos, out var vtableOffset))
        {
            return false;
        }

        var vtablePos = tablePos - vtableOffset;
        if (!TryReadUInt16(data, vtablePos, out var vtableSize))
        {
            return false;
        }

        var entryPos = vtablePos + 4 + (fieldIndex * 2);
        if (entryPos + 2 > vtablePos + vtableSize || !TryReadUInt16(data, entryPos, out var fieldOffset) || fieldOffset == 0)
        {
            return false;
        }

        fieldAddress = tablePos + fieldOffset;
        return fieldAddress >= 0 && fieldAddress < data.Length;
    }

    private static bool TryReadInt32(byte[] data, int offset, out int value)
    {
        value = 0;
        if (offset < 0 || offset > data.Length - 4)
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
        return true;
    }

    private static bool TryReadUInt16(byte[] data, int offset, out ushort value)
    {
        value = 0;
        if (offset < 0 || offset > data.Length - 2)
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
        return true;
    }
}

public sealed class TrpfsArchive : IArchiveFile
{
    private readonly List<ArchiveEntry> _files;

    public TrpfsArchive(List<ArchiveEntry> files)
    {
        _files = files;
    }

    public IEnumerable<ArchiveEntry> Files => _files;

    public static TrpfsArchive Parse(string trpfsPath, string? trpfdPath)
    {
        var fsMeta = ParseTrpfsMetadata(trpfsPath);
        if (fsMeta is null)
        {
            return new TrpfsArchive(new List<ArchiveEntry>());
        }

        var hashToPath = trpfdPath is null ? new Dictionary<ulong, string>() : ParseTrpfdPathLookup(trpfdPath);
        var entries = new List<ArchiveEntry>(fsMeta.Hashes.Count);

        var count = Math.Min(fsMeta.Hashes.Count, fsMeta.Offsets.Count);
        for (var i = 0; i < count; i++)
        {
            var start = fsMeta.Offsets[i];
            var end = i + 1 < count ? fsMeta.Offsets[i + 1] : fsMeta.MetadataOffset;
            if (end <= start || start < 0 || end > fsMeta.FileSize)
            {
                continue;
            }

            var length = end - start;
            var hash = fsMeta.Hashes[i];

            var name = hashToPath.TryGetValue(hash, out var path)
                ? NormalizePath(path)
                : $"hash_{hash:X16}.bin";

            if (Path.GetExtension(name).Length == 0)
            {
                name += ".bin";
            }

            entries.Add(new ArchiveEntry
            {
                FileName = name,
                OpenRead = () => OpenFileSegment(trpfsPath, start, length),
            });
        }

        return new TrpfsArchive(entries);
    }

    private static TrpfsMetadata? ParseTrpfsMetadata(string trpfsPath)
    {
        using var stream = File.OpenRead(trpfsPath);
        if (stream.Length < 16)
        {
            return null;
        }

        var header = new byte[16];
        stream.ReadExactly(header);
        if (!header.AsSpan(0, 8).SequenceEqual(Encoding.ASCII.GetBytes("ONEPACK\0")))
        {
            return null;
        }

        var metadataOffset = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(8, 8));
        if (metadataOffset < 0 || metadataOffset >= stream.Length)
        {
            return null;
        }

        stream.Position = metadataOffset;
        var metadata = new byte[stream.Length - metadataOffset];
        stream.ReadExactly(metadata);

        if (!TryGetRootTable(metadata, out var rootTable))
        {
            return null;
        }

        var hashes = ReadUlongVector(metadata, rootTable, 0);
        var offsets = ReadUlongVector(metadata, rootTable, 1)
            .Select(value => (long)value)
            .ToList();

        if (hashes.Count == 0 || offsets.Count == 0)
        {
            return null;
        }

        return new TrpfsMetadata(hashes, offsets, metadataOffset, stream.Length);
    }

    private static Dictionary<ulong, string> ParseTrpfdPathLookup(string trpfdPath)
    {
        var data = File.ReadAllBytes(trpfdPath);
        if (!TryGetRootTable(data, out var rootTable))
        {
            return new Dictionary<ulong, string>();
        }

        var paths = ReadStringVector(data, rootTable, 1);
        var lookup = new Dictionary<ulong, string>();
        foreach (var path in paths)
        {
            var hash = Fnv1a64.Compute(path);
            if (!lookup.ContainsKey(hash))
            {
                lookup.Add(hash, path);
            }
        }

        return lookup;
    }

    private static List<ulong> ReadUlongVector(byte[] data, int tablePos, int fieldIndex)
    {
        var values = new List<ulong>();
        if (!TryGetVector(data, tablePos, fieldIndex, out var vectorPos, out var vectorLength))
        {
            return values;
        }

        for (var i = 0; i < vectorLength; i++)
        {
            var elemPos = vectorPos + (i * 8);
            if (elemPos < 0 || elemPos > data.Length - 8)
            {
                continue;
            }

            values.Add(BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(elemPos, 8)));
        }

        return values;
    }

    private static List<string> ReadStringVector(byte[] data, int tablePos, int fieldIndex)
    {
        var values = new List<string>();
        if (!TryGetVector(data, tablePos, fieldIndex, out var vectorPos, out var vectorLength))
        {
            return values;
        }

        for (var i = 0; i < vectorLength; i++)
        {
            var elemPos = vectorPos + (i * 4);
            if (!TryReadInt32(data, elemPos, out var rel))
            {
                continue;
            }

            var strPos = elemPos + rel;
            if (!TryReadInt32(data, strPos, out var len) || len < 0 || strPos + 4 > data.Length - len)
            {
                continue;
            }

            values.Add(Encoding.UTF8.GetString(data, strPos + 4, len));
        }

        return values;
    }

    private static bool TryGetRootTable(byte[] data, out int rootTable)
    {
        rootTable = 0;
        return TryReadInt32(data, 0, out rootTable) && rootTable >= 4 && rootTable < data.Length;
    }

    private static bool TryGetVector(byte[] data, int tablePos, int fieldIndex, out int vectorPos, out int vectorLength)
    {
        vectorPos = 0;
        vectorLength = 0;
        if (!TryGetFieldAddress(data, tablePos, fieldIndex, out var fieldAddress) || !TryReadInt32(data, fieldAddress, out var rel))
        {
            return false;
        }

        var pos = fieldAddress + rel;
        if (!TryReadInt32(data, pos, out vectorLength) || vectorLength < 0)
        {
            return false;
        }

        vectorPos = pos + 4;
        return vectorPos >= 0 && vectorPos <= data.Length;
    }

    private static bool TryGetFieldAddress(byte[] data, int tablePos, int fieldIndex, out int fieldAddress)
    {
        fieldAddress = 0;
        if (!TryReadInt32(data, tablePos, out var vtableOffset))
        {
            return false;
        }

        var vtablePos = tablePos - vtableOffset;
        if (!TryReadUInt16(data, vtablePos, out var vtableSize))
        {
            return false;
        }

        var entryPos = vtablePos + 4 + (fieldIndex * 2);
        if (entryPos + 2 > vtablePos + vtableSize || !TryReadUInt16(data, entryPos, out var fieldOffset) || fieldOffset == 0)
        {
            return false;
        }

        fieldAddress = tablePos + fieldOffset;
        return fieldAddress >= 0 && fieldAddress < data.Length;
    }

    private static bool TryReadInt32(byte[] data, int offset, out int value)
    {
        value = 0;
        if (offset < 0 || offset > data.Length - 4)
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
        return true;
    }

    private static bool TryReadUInt16(byte[] data, int offset, out ushort value)
    {
        value = 0;
        if (offset < 0 || offset > data.Length - 2)
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
        return true;
    }

    private static byte[] ReadSegment(string path, long offset, int length)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, FileOptions.SequentialScan);
        if (offset < 0 || length <= 0 || offset > stream.Length - length)
        {
            return Array.Empty<byte>();
        }

        stream.Position = offset;
        var data = new byte[length];
        var read = stream.Read(data, 0, length);
        return read == length ? data : Array.Empty<byte>();
    }

    private static Stream OpenFileSegment(string path, long offset, long length)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536, FileOptions.SequentialScan);
        if (offset < 0 || length <= 0 || offset + length > stream.Length)
        {
            stream.Dispose();
            return new MemoryStream(Array.Empty<byte>(), writable: false);
        }

        return new FileSegmentStream(stream, offset, length);
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

    private static string NormalizePath(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "unnamed.bin";
        }

        var path = raw.Replace('\\', '/').TrimStart('/');
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part != "." && part != "..")
            .ToList();

        return parts.Count == 0 ? "unnamed.bin" : string.Join('/', parts);
    }

    private static string GuessExtension(byte[] payload)
    {
        if (payload.Length >= 4)
        {
            if (payload[0] == (byte)'C' && payload[1] == (byte)'R' && payload[2] == (byte)'A' && payload[3] == (byte)'G')
            {
                return ".garc";
            }

            if (payload[0] == (byte)'B' && payload[1] == (byte)'N' && payload[2] == (byte)'T' && payload[3] == (byte)'X')
            {
                return ".bntx";
            }
        }

        if (payload.Length >= 0x104 &&
            payload[0x100] == (byte)'N' && payload[0x101] == (byte)'C' && payload[0x102] == (byte)'C' && payload[0x103] == (byte)'H')
        {
            return ".cxi";
        }

        return ".bin";
    }

    private sealed record TrpfsMetadata(List<ulong> Hashes, List<long> Offsets, long MetadataOffset, long FileSize);
}
