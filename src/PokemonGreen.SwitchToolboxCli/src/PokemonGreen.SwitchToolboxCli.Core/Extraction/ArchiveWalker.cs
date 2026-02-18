using PokemonGreen.SwitchToolboxCli.Core.Abstractions;
using PokemonGreen.SwitchToolboxCli.Core.Loading;

namespace PokemonGreen.SwitchToolboxCli.Core.Extraction;

public sealed class ArchiveWalker
{
    private readonly FileLoader _loader;

    public ArchiveWalker(FileLoader loader)
    {
        _loader = loader;
    }

    public IEnumerable<(string Path, IFileFormat Format, object Value)> Walk(string rootPath)
    {
        var root = _loader.Open(rootPath);
        if (root is null)
        {
            yield break;
        }

        foreach (var item in WalkValue(Path.GetFileName(rootPath), root.Value.Format, root.Value.Value))
        {
            yield return item;
        }
    }

    private IEnumerable<(string Path, IFileFormat Format, object Value)> WalkValue(string currentPath, IFileFormat format, object value)
    {
        yield return (currentPath, format, value);

        if (value is not IArchiveFile archive)
        {
            yield break;
        }

        foreach (var entry in archive.Files)
        {
            using var entryStream = entry.OpenRead();
            try
            {
                var detected = _loader.Open(entryStream, entry.FileName);
                if (detected is null)
                {
                    continue;
                }

                var entryPath = Path.Combine(currentPath, entry.FileName).Replace('\\', '/');
                foreach (var nested in WalkValue(entryPath, detected.Value.Format, detected.Value.Value))
                {
                    yield return nested;
                }
            }
            finally
            {
                entryStream?.Dispose();
            }
        }
    }
}
