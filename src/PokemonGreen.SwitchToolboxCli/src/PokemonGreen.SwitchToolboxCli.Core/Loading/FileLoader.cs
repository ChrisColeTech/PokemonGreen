using PokemonGreen.SwitchToolboxCli.Core.Abstractions;

namespace PokemonGreen.SwitchToolboxCli.Core.Loading;

public sealed class FileLoader
{
    private readonly FormatRegistry _registry;

    public FileLoader(FormatRegistry registry)
    {
        _registry = registry;
    }

    public (IFileFormat Format, object Value)? Open(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Open(stream, filePath);
    }

    public (IFileFormat Format, object Value)? Open(Stream stream, string? fileName)
    {
        if (!stream.CanSeek)
        {
            var mem = new MemoryStream();
            stream.CopyTo(mem);
            mem.Position = 0;
            stream = mem;
        }

        var start = stream.Position;
        foreach (var format in _registry.Formats)
        {
            stream.Position = start;
            if (!format.Identify(stream, fileName))
            {
                continue;
            }

            stream.Position = start;
            return (format, format.Load(stream, fileName));
        }

        return null;
    }
}
